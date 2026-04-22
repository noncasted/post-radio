using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Meta.Audio;

// Manual SOCKS5 client for SocketsHttpHandler.ConnectCallback. We don't rely on
// .NET's built-in SOCKS support via WebProxy because it has had compatibility
// quirks across runtime versions. This implementation performs the RFC 1928
// no-auth handshake inline and hands off the raw TCP stream to the TLS layer.
public static class Socks5ConnectCallback
{
    public static Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>> Create(string proxyUri, ILogger logger)
    {
        var uri = new Uri(proxyUri);
        var proxyHost = uri.Host;
        var proxyPort = uri.Port;

        return async (context, cancellationToken) =>
        {
            var targetHost = context.DnsEndPoint.Host;
            var targetPort = context.DnsEndPoint.Port;

            logger.LogInformation("[Socks5] connecting to proxy {ProxyHost}:{ProxyPort} -> target {TargetHost}:{TargetPort}",
                proxyHost, proxyPort, targetHost, targetPort);

            // Resolve the proxy host to a concrete IP so we can pick the right AddressFamily
            // and avoid dual-stack ConnectAsync hangs when the listener is IPv4-only
            // (e.g. `ssh -D 127.0.0.1:1080` binds only to 127.0.0.1, not ::1).
            var proxyAddress = await ResolveProxyAddress(proxyHost, cancellationToken);
            var socket = new Socket(proxyAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

            try
            {
                var stepStart = DateTime.UtcNow;
                await socket.ConnectAsync(new IPEndPoint(proxyAddress, proxyPort), cancellationToken);
                logger.LogInformation("[Socks5] step=tcp_connect ok in {Ms}ms (af={AddressFamily}, localEp={Local}, remoteEp={Remote})",
                    (long)(DateTime.UtcNow - stepStart).TotalMilliseconds,
                    proxyAddress.AddressFamily, socket.LocalEndPoint, socket.RemoteEndPoint);

                // Greeting: VER=5, NMETHODS=1, METHOD=0 (no auth)
                stepStart = DateTime.UtcNow;
                await SendAll(socket, new byte[] { 0x05, 0x01, 0x00 }, cancellationToken);
                logger.LogInformation("[Socks5] step=greeting_sent ok in {Ms}ms, available_to_read={Available}",
                    (long)(DateTime.UtcNow - stepStart).TotalMilliseconds, socket.Available);

                stepStart = DateTime.UtcNow;
                var methodReply = new byte[2];
                await ReceiveExact(socket, methodReply, cancellationToken);
                logger.LogInformation("[Socks5] step=greeting_reply ok in {Ms}ms, bytes={Bytes}",
                    (long)(DateTime.UtcNow - stepStart).TotalMilliseconds,
                    Convert.ToHexString(methodReply));

                if (methodReply[0] != 0x05)
                    throw new IOException($"SOCKS5 greeting failed: unexpected version 0x{methodReply[0]:X2}");

                if (methodReply[1] != 0x00)
                    throw new IOException($"SOCKS5 greeting failed: method 0x{methodReply[1]:X2} (expected 0x00 no-auth). The proxy is requiring auth we don't support.");

                // CONNECT request with domain-name addressing so DNS is resolved remotely (-h semantics).
                var hostBytes = Encoding.ASCII.GetBytes(targetHost);

                if (hostBytes.Length > 255)
                    throw new IOException($"SOCKS5 CONNECT: hostname too long ({hostBytes.Length} > 255)");

                var request = new byte[7 + hostBytes.Length];
                request[0] = 0x05;                           // VER
                request[1] = 0x01;                           // CMD = CONNECT
                request[2] = 0x00;                           // RSV
                request[3] = 0x03;                           // ATYP = DOMAINNAME
                request[4] = (byte)hostBytes.Length;
                Buffer.BlockCopy(hostBytes, 0, request, 5, hostBytes.Length);
                request[5 + hostBytes.Length] = (byte)(targetPort >> 8);
                request[6 + hostBytes.Length] = (byte)(targetPort & 0xff);

                stepStart = DateTime.UtcNow;
                logger.LogInformation("[Socks5] step=connect_send sending {Bytes} bytes to proxy (target={TargetHost}:{TargetPort})",
                    request.Length, targetHost, targetPort);
                await SendAll(socket, request, cancellationToken);
                logger.LogInformation("[Socks5] step=connect_sent ok in {Ms}ms", (long)(DateTime.UtcNow - stepStart).TotalMilliseconds);

                // Response header: VER, REP, RSV, ATYP
                stepStart = DateTime.UtcNow;
                var replyHeader = new byte[4];
                await ReceiveExact(socket, replyHeader, cancellationToken);
                logger.LogInformation("[Socks5] step=connect_reply_header ok in {Ms}ms, bytes={Bytes}",
                    (long)(DateTime.UtcNow - stepStart).TotalMilliseconds, Convert.ToHexString(replyHeader));

                if (replyHeader[0] != 0x05)
                    throw new IOException($"SOCKS5 CONNECT: unexpected version 0x{replyHeader[0]:X2}");

                if (replyHeader[1] != 0x00)
                    throw new IOException($"SOCKS5 CONNECT rejected: REP=0x{replyHeader[1]:X2} ({DescribeRep(replyHeader[1])})");

                // Consume the bound-address payload; we don't use it but must drain it before handing off.
                var addrLen = replyHeader[3] switch
                {
                    0x01 => 4,
                    0x04 => 16,
                    0x03 => await ReadDomainLength(socket, cancellationToken),
                    _ => throw new IOException($"SOCKS5 CONNECT: unknown ATYP 0x{replyHeader[3]:X2}")
                };

                stepStart = DateTime.UtcNow;
                var tail = new byte[addrLen + 2];
                await ReceiveExact(socket, tail, cancellationToken);
                logger.LogInformation("[Socks5] step=connect_reply_tail ok in {Ms}ms (tail bytes={Bytes})",
                    (long)(DateTime.UtcNow - stepStart).TotalMilliseconds, Convert.ToHexString(tail));

                logger.LogInformation("[Socks5] tunnel to {TargetHost}:{TargetPort} established via {ProxyHost}:{ProxyPort}",
                    targetHost, targetPort, proxyHost, proxyPort);

                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception e)
            {
                socket.Dispose();
                logger.LogError(e, "[Socks5] tunnel to {TargetHost}:{TargetPort} via {ProxyHost}:{ProxyPort} FAILED",
                    targetHost, targetPort, proxyHost, proxyPort);
                throw;
            }
        };
    }

    private static async Task<IPAddress> ResolveProxyAddress(string host, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(host, out var literal))
            return literal;

        var entries = await Dns.GetHostAddressesAsync(host, cancellationToken);

        // Prefer IPv4 since typical local SOCKS tunnels bind to 127.0.0.1 only.
        return entries.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
               ?? entries.FirstOrDefault()
               ?? throw new IOException($"SOCKS5: unable to resolve proxy host '{host}'");
    }

    private static async Task<int> ReadDomainLength(Socket socket, CancellationToken cancellationToken)
    {
        var lenBuffer = new byte[1];
        await ReceiveExact(socket, lenBuffer, cancellationToken);
        return 1 + lenBuffer[0];
    }

    private static async Task SendAll(Socket socket, byte[] buffer, CancellationToken cancellationToken)
    {
        var total = 0;

        while (total < buffer.Length)
        {
            var n = await socket.SendAsync(buffer.AsMemory(total), SocketFlags.None, cancellationToken);

            if (n == 0)
                throw new IOException("SOCKS5: socket closed during send");

            total += n;
        }
    }

    private static async Task ReceiveExact(Socket socket, byte[] buffer, CancellationToken cancellationToken)
    {
        var total = 0;

        while (total < buffer.Length)
        {
            var n = await socket.ReceiveAsync(buffer.AsMemory(total), SocketFlags.None, cancellationToken);

            if (n == 0)
                throw new IOException("SOCKS5: socket closed during receive");

            total += n;
        }
    }

    private static string DescribeRep(byte rep) => rep switch
    {
        0x01 => "general SOCKS server failure",
        0x02 => "connection not allowed by ruleset",
        0x03 => "network unreachable",
        0x04 => "host unreachable",
        0x05 => "connection refused",
        0x06 => "TTL expired",
        0x07 => "command not supported",
        0x08 => "address type not supported",
        _ => $"unknown (0x{rep:X2})"
    };
}
