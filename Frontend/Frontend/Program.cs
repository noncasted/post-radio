using Audio;
using Core;
using Frontend.Components;
using Images;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

services
    .AddMudServices();

services.AddEndpointsApiExplorer();
services.AddHttpClient();

builder.AddCredentials();
builder.ConfigureCors();

builder
    .AddDefaultServices()
    .AddAudioServices()
    .AddImageServices();

services.AddHostedService<CoreStartup>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseStaticFiles();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies();

app.Run();