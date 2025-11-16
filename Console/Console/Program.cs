using Common;
using Console;
using Console.Services;
using App = Console.Components.App;

var builder = WebApplication.CreateBuilder(args);

builder.SetupConsole();
builder.Services.AddScoped<IAuthService, AuthService>();

var app = builder.Build();

if (app.Environment.IsDevelopment() == false)
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(Main).Assembly)
    .AddAdditionalAssemblies(typeof(Playlists).Assembly)
    .AddAdditionalAssemblies(typeof(Songs).Assembly)
    .AddAdditionalAssemblies(typeof(Migrations).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();