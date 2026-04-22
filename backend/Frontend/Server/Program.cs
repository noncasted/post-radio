var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("meta", client =>
{
    var metaUrl = builder.Configuration["services:meta:http:0"]
                  ?? Environment.GetEnvironmentVariable("services__meta__http__0")
                  ?? "http://localhost:5000";
    client.BaseAddress = new Uri(metaUrl);
});

var app = builder.Build();

app.Use(async (ctx, next) =>
{
    app.Logger.LogInformation("[Req] {Method} {Path}{Query}", ctx.Request.Method, ctx.Request.Path, ctx.Request.QueryString);
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.Map("/api/{**path}", async (HttpContext ctx, IHttpClientFactory factory, string path) =>
{
    var client = factory.CreateClient("meta");
    var target = $"api/{path}{ctx.Request.QueryString}";
    using var req = new HttpRequestMessage(new HttpMethod(ctx.Request.Method), target);

    if (ctx.Request.ContentLength > 0 || ctx.Request.Headers.ContainsKey("Transfer-Encoding"))
    {
        req.Content = new StreamContent(ctx.Request.Body);
        if (ctx.Request.ContentType is { } ct)
            req.Content.Headers.TryAddWithoutValidation("Content-Type", ct);
    }

    foreach (var header in ctx.Request.Headers)
    {
        if (ShouldSkipProxyRequestHeader(header.Key))
            continue;

        req.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
    }

    using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);
    ctx.Response.StatusCode = (int)resp.StatusCode;
    foreach (var h in resp.Headers)
        ctx.Response.Headers[h.Key] = h.Value.ToArray();
    foreach (var h in resp.Content.Headers)
        ctx.Response.Headers[h.Key] = h.Value.ToArray();
    ctx.Response.Headers.Remove("transfer-encoding");
    ctx.Response.Headers.Remove("connection");
    await resp.Content.CopyToAsync(ctx.Response.Body, ctx.RequestAborted);
});

app.MapFallbackToFile("index.html");

app.Run();

static bool ShouldSkipProxyRequestHeader(string name)
{
    return string.Equals(name, "Host", StringComparison.OrdinalIgnoreCase)
           || string.Equals(name, "Content-Type", StringComparison.OrdinalIgnoreCase)
           || string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase)
           || string.Equals(name, "Connection", StringComparison.OrdinalIgnoreCase)
           || string.Equals(name, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase);
}
