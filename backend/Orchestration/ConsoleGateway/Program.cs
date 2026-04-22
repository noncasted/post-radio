using System.Security.Claims;
using BlazorBlueprint.Components;
using Console;
using Console.Home;
using Console.Infrastructure.Monitoring;
using ConsoleGateway;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Orchestration;

const string defaultToken = "local-dev-token";

var builder = WebApplication.CreateBuilder(args);

builder.SetupConsole();
builder.AddCommonConsoleComponents();
builder.Services.AddBlazorBlueprintComponents();
builder.Services.AddSingleton<IHeapReportStorage, HeapReportStorage>();
builder.Services.AddSingleton<HeapReportCollector>();

var consoleToken = Environment.GetEnvironmentVariable("CONSOLE_TOKEN") ?? "";
var authEnabled = !string.IsNullOrEmpty(consoleToken) && consoleToken != defaultToken;

if (authEnabled)
{
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
           .AddCookie(options => {
               options.LoginPath = ConsoleConstants.Pages.Login;
               options.Cookie.Name = "Console.Auth";
               options.Cookie.HttpOnly = true;
               options.Cookie.SameSite = SameSiteMode.Strict;
               options.ExpireTimeSpan = TimeSpan.FromDays(30);
           });
    builder.Services.AddAuthorization();
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

if (authEnabled)
{
    System.Console.WriteLine("[Console] Auth enabled");

    app.UseAuthentication();
    app.UseAuthorization();

    app.Use(async (context, next) => {
        var path = context.Request.Path.Value ?? "";
        var isLoginPath = path.StartsWith("/login", StringComparison.OrdinalIgnoreCase);
        var isStaticPath = path.StartsWith("/_") || path.StartsWith("/css") || path.StartsWith("/styles");

        var isHealthPath = path.Equals("/health", StringComparison.OrdinalIgnoreCase) ||
                           path.Equals("/alive", StringComparison.OrdinalIgnoreCase) ||
                           path.Equals("/ready", StringComparison.OrdinalIgnoreCase);

        if (!isStaticPath && !isLoginPath && !isHealthPath && context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.Redirect(ConsoleConstants.Pages.Login);
            return;
        }

        await next();
    });
}
else
{
    System.Console.WriteLine("[Console] Auth disabled (default token)");
}

app.UseAntiforgery();
app.MapStaticAssets();

if (authEnabled)
{
    app.MapGet("/login", async (HttpContext context, string? token) => {
        if (!string.IsNullOrEmpty(token) && token == consoleToken)
        {
            var claims = new List<Claim> { new(ClaimTypes.Name, "admin") };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));
            context.Response.Redirect("/");
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        var errorHtml = !string.IsNullOrEmpty(token) ? """<div class="error">Invalid token</div>""" : "";

        await context.Response.WriteAsync($$"""
                                            <!DOCTYPE html>
                                            <html lang="en" class="dark">
                                            <head>
                                                <meta charset="utf-8"/>
                                                <meta name="viewport" content="width=device-width, initial-scale=1.0"/>
                                                <title>Console — Login</title>
                                                <style>
                                                    * { margin: 0; padding: 0; box-sizing: border-box; }
                                                    body { font-family: system-ui, -apple-system, sans-serif; background: #0a0a0a; color: #fafafa; display: flex; align-items: center; justify-content: center; min-height: 100vh; }
                                                    .card { background: #141414; border: 1px solid #262626; border-radius: 12px; padding: 2rem; width: 100%; max-width: 400px; }
                                                    h1 { font-size: 1.5rem; margin-bottom: 0.5rem; }
                                                    .subtitle { color: #a1a1aa; font-size: 0.875rem; margin-bottom: 1.5rem; }
                                                    label { display: block; font-size: 0.875rem; font-weight: 500; margin-bottom: 0.5rem; }
                                                    input { width: 100%; padding: 0.625rem 0.75rem; background: #0a0a0a; border: 1px solid #262626; border-radius: 8px; color: #fafafa; font-size: 0.875rem; outline: none; }
                                                    input:focus { border-color: #3b82f6; }
                                                    button { width: 100%; padding: 0.625rem; background: #3b82f6; color: white; border: none; border-radius: 8px; font-size: 0.875rem; font-weight: 500; cursor: pointer; margin-top: 1rem; }
                                                    button:hover { background: #2563eb; }
                                                    .error { color: #ef4444; font-size: 0.875rem; margin-bottom: 1rem; }
                                                </style>
                                            </head>
                                            <body>
                                                <div class="card">
                                                    <h1>Console</h1>
                                                    <p class="subtitle">Enter access token to continue</p>
                                                    {{errorHtml}}
                                                    <form method="get" action="/login">
                                                        <label for="token">Token</label>
                                                        <input type="password" id="token" name="token" placeholder="Enter token..." autofocus required />
                                                        <button type="submit">Sign in</button>
                                                    </form>
                                                </div>
                                            </body>
                                            </html>
                                            """);
    });
}

app.MapDefaultEndpoints();
app.MapConsoleMediaEndpoints();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode()
   .AddAdditionalAssemblies(typeof(Home).Assembly);

app.Run();