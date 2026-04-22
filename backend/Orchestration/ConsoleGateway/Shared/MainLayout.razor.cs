using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ConsoleGateway.Shared;

public partial class MainLayout : LayoutComponentBase
{
    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    private StartupOverlay _startupOverlay = null!;
    private bool isDarkMode;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            isDarkMode = await JS.InvokeAsync<bool>("isDarkModeEnabled");
            StateHasChanged();
        }
    }

    private void ShowStartupOverlay()
    {
        _startupOverlay.Show();
    }

    private async Task ToggleDarkMode()
    {
        await JS.InvokeVoidAsync("toggleDarkMode");
        isDarkMode = !isDarkMode;
    }
}