using Common.Reactive;
using Microsoft.AspNetCore.Components;

namespace Console;

public abstract class UiComponent : ComponentBase, IDisposable
{
    private readonly ILifetime _lifetime = new Lifetime();

    public IReadOnlyLifetime Lifetime => _lifetime;

    protected override async Task OnInitializedAsync()
    {
        await OnSetup(_lifetime);
        await InvokeAsync(StateHasChanged);
    }

    protected abstract Task OnSetup(IReadOnlyLifetime lifetime);

    public void Dispose()
    {
        _lifetime.Terminate();
    }
}