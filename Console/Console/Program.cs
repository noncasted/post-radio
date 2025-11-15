using Common;
using Console;
using Console.Services;
using App = Console.Components.App;

var builder = WebApplication.CreateBuilder(args);

builder.SetupConsole();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient();

// Add authentication service
builder.Services.AddScoped<IAuthService, AuthService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseStaticFiles();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(Main).Assembly)
    .AddAdditionalAssemblies(typeof(Playlists).Assembly)
    .AddAdditionalAssemblies(typeof(Songs).Assembly)
    .AddAdditionalAssemblies(typeof(Migrations).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();