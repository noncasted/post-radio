namespace Meta.Audio;

public static class AudioDurationReader
{
    private const int MinimumFrameCount = 3;

    private static readonly int[,,] BitratesKbps =
    {
        // MPEG 2.5
        {
            // reserved
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            // Layer III
            { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0 },
            // Layer II
            { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0 },
            // Layer I
            { 0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256, 0 }
        },
        // reserved
        {
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }
        },
        // MPEG 2
        {
            // reserved
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            // Layer III
            { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0 },
            // Layer II
            { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0 },
            // Layer I
            { 0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256, 0 }
        },
        // MPEG 1
        {
            // reserved
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            // Layer III
            { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0 },
            // Layer II
            { 0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384, 0 },
            // Layer I
            { 0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448, 0 }
        }
    };

    private static readonly int[,] SampleRates =
    {
        { 11025, 12000, 8000, 0 },
        { 0, 0, 0, 0 },
        { 22050, 24000, 16000, 0 },
        { 44100, 48000, 32000, 0 }
    };

    public static async Task<TimeSpan?> TryReadDuration(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
            return null;

        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        return TryReadDuration(bytes);
    }

    public static TimeSpan? TryReadDuration(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4)
            return null;

        return TryReadMp3Duration(bytes) ?? TryReadMp4Duration(bytes);
    }

    private static TimeSpan? TryReadMp3Duration(ReadOnlySpan<byte> bytes)
    {
        var offset = GetMp3AudioStartOffset(bytes);
        var totalSeconds = 0d;
        var frameCount = 0;

        while (offset + 4 <= bytes.Length)
        {
            if (!TryReadFrame(bytes[offset..], out var frame))
            {
                offset++;
                continue;
            }

            totalSeconds += frame.Samples / (double)frame.SampleRate;
            frameCount++;
            offset += frame.Length;
        }

        return frameCount >= MinimumFrameCount
            ? TimeSpan.FromSeconds(totalSeconds)
            : null;
    }

    private static TimeSpan? TryReadMp4Duration(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12 || !HasMp4Signature(bytes))
            return null;

        return FindMvhdDuration(bytes);
    }

    private static bool HasMp4Signature(ReadOnlySpan<byte> bytes)
    {
        return bytes.Length >= 12
               && bytes[4] == 'f'
               && bytes[5] == 't'
               && bytes[6] == 'y'
               && bytes[7] == 'p';
    }

    private static TimeSpan? FindMvhdDuration(ReadOnlySpan<byte> bytes)
    {
        var moov = FindBox(bytes, "moov");
        if (moov == null)
            return null;

        var mvhd = FindBox(bytes.Slice(moov.Value.PayloadOffset, moov.Value.PayloadLength), "mvhd");
        if (mvhd == null)
            return null;

        var payload = bytes.Slice(moov.Value.PayloadOffset + mvhd.Value.PayloadOffset, mvhd.Value.PayloadLength);
        if (payload.Length < 20)
            return null;

        var version = payload[0];
        ulong duration;
        uint timescale;

        if (version == 1)
        {
            if (payload.Length < 32)
                return null;

            timescale = ReadUInt32BigEndian(payload[20..24]);
            duration = ReadUInt64BigEndian(payload[24..32]);
        }
        else
        {
            timescale = ReadUInt32BigEndian(payload[12..16]);
            duration = ReadUInt32BigEndian(payload[16..20]);
        }

        if (timescale == 0 || duration == 0)
            return null;

        return TimeSpan.FromSeconds(duration / (double)timescale);
    }

    private static Box? FindBox(ReadOnlySpan<byte> bytes, string type)
    {
        var offset = 0;

        while (offset + 8 <= bytes.Length)
        {
            var size = ReadUInt32BigEndian(bytes.Slice(offset, 4));
            var headerLength = 8;
            ulong boxSize = size;

            if (size == 1)
            {
                if (offset + 16 > bytes.Length)
                    return null;

                boxSize = ReadUInt64BigEndian(bytes.Slice(offset + 8, 8));
                headerLength = 16;
            }
            else if (size == 0)
            {
                boxSize = (ulong)(bytes.Length - offset);
            }

            if (boxSize < (ulong)headerLength || boxSize > (ulong)(bytes.Length - offset))
                return null;

            if (BoxTypeEquals(bytes.Slice(offset + 4, 4), type))
            {
                return new Box(
                    offset + headerLength,
                    checked((int)boxSize - headerLength));
            }

            offset += checked((int)boxSize);
        }

        return null;
    }

    private static bool BoxTypeEquals(ReadOnlySpan<byte> actual, string expected)
    {
        return actual.Length == 4
               && actual[0] == expected[0]
               && actual[1] == expected[1]
               && actual[2] == expected[2]
               && actual[3] == expected[3];
    }

    private static uint ReadUInt32BigEndian(ReadOnlySpan<byte> bytes)
    {
        return (uint)(bytes[0] << 24
                      | bytes[1] << 16
                      | bytes[2] << 8
                      | bytes[3]);
    }

    private static ulong ReadUInt64BigEndian(ReadOnlySpan<byte> bytes)
    {
        return (ulong)ReadUInt32BigEndian(bytes[..4]) << 32
               | ReadUInt32BigEndian(bytes[4..8]);
    }

    private static int GetMp3AudioStartOffset(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 10 || bytes[0] != 'I' || bytes[1] != 'D' || bytes[2] != '3')
            return 0;

        var tagSize = (bytes[6] & 0x7F) << 21
                      | (bytes[7] & 0x7F) << 14
                      | (bytes[8] & 0x7F) << 7
                      | bytes[9] & 0x7F;

        return Math.Min(bytes.Length, 10 + tagSize);
    }

    private static bool TryReadFrame(ReadOnlySpan<byte> bytes, out Mp3Frame frame)
    {
        frame = default;

        if (bytes.Length < 4)
            return false;

        var header = (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
        if ((header & 0xFFE00000) != 0xFFE00000)
            return false;

        var version = (header >> 19) & 0x3;
        var layer = (header >> 17) & 0x3;
        var bitrateIndex = (header >> 12) & 0xF;
        var sampleRateIndex = (header >> 10) & 0x3;
        var padding = (header >> 9) & 0x1;

        if (version == 1 || layer == 0 || bitrateIndex is 0 or 15 || sampleRateIndex == 3)
            return false;

        var bitrateKbps = BitratesKbps[version, layer, bitrateIndex];
        var sampleRate = SampleRates[version, sampleRateIndex];
        if (bitrateKbps <= 0 || sampleRate <= 0)
            return false;

        var samples = GetSamplesPerFrame(version, layer);
        var length = GetFrameLength(version, layer, bitrateKbps, sampleRate, padding);
        if (length < 4 || length > bytes.Length)
            return false;

        frame = new Mp3Frame(length, samples, sampleRate);
        return true;
    }

    private static int GetSamplesPerFrame(int version, int layer)
    {
        return layer switch
        {
            3 => 384,
            2 => 1152,
            1 => version == 3 ? 1152 : 576,
            _ => 0
        };
    }

    private static int GetFrameLength(int version, int layer, int bitrateKbps, int sampleRate, int padding)
    {
        var bitrate = bitrateKbps * 1000;

        return layer switch
        {
            3 => ((12 * bitrate / sampleRate) + padding) * 4,
            2 => 144 * bitrate / sampleRate + padding,
            1 => (version == 3 ? 144 : 72) * bitrate / sampleRate + padding,
            _ => 0
        };
    }

    private readonly record struct Box(int PayloadOffset, int PayloadLength);

    private readonly record struct Mp3Frame(int Length, int Samples, int SampleRate);
}
