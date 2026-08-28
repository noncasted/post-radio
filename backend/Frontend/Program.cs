using Frontend.Components;
using Frontend.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("meta", client =>
{
    var metaUrl = builder.Configuration["services:meta:http:0"]
                  ?? Environment.GetEnvironmentVariable("services__meta__http__0")
                  ?? "http://localhost:5000";
    client.BaseAddress = new Uri(metaUrl);
});

// Under InteractiveServer the api client runs on the server, so it talks to meta
// directly instead of looping back through this host's own proxy.
builder.Services.AddScoped<IRadioApi>(sp => new RadioApi(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("meta"),
    sp.GetRequiredService<ILogger<RadioApi>>()));

builder.Services.AddRazorComponents()
       .AddInteractiveServerComponents();

var site = builder.Configuration.GetSection("Site").Get<SiteOptions>() ?? new SiteOptions();
builder.Services.AddSingleton(site);

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

app.UseStaticFiles();
app.UseAntiforgery();

// Still required: audio and image urls handed to the browser are relative to this host.
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

// Generated rather than served from wwwroot: both files have to name the canonical domain,
// and a static copy would silently drift from Site:BaseUrl.
app.MapGet("/robots.txt", (SiteOptions options) => Results.Text(
    $"""
     User-agent: *
     Allow: /

     Sitemap: {options.Url("sitemap.xml")}

     """,
    "text/plain"));

app.MapGet("/sitemap.xml", (SiteOptions options) => Results.Text(
    $"""
     <?xml version="1.0" encoding="UTF-8"?>
     <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
       <url>
         <loc>{options.Url("/")}</loc>
         <changefreq>daily</changefreq>
         <priority>1.0</priority>
       </url>
     </urlset>

     """,
    "application/xml"));

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.Run();

static bool ShouldSkipProxyRequestHeader(string name)
{
    return string.Equals(name, "Host", StringComparison.OrdinalIgnoreCase)
           || string.Equals(name, "Content-Type", StringComparison.OrdinalIgnoreCase)
           || string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase)
           || string.Equals(name, "Connection", StringComparison.OrdinalIgnoreCase)
           || string.Equals(name, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase);
}
