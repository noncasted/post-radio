using Common;
using Frontend;
using Frontend.Components;
using Frontend.Extensions;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.SetupFrontend();

var services = builder.Services;

services.AddRazorComponents()
//    .AddInteractiveWebAssemblyComponents()
    .AddInteractiveServerComponents();

CorsExtensions.ConfigureCors(builder);

builder.AddImageServices();
services.AddHostedService<CoreStartup>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
  //  app.UseWebAssemblyDebugging();
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
    .AddAdditionalAssemblies();

app.Run();