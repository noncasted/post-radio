# Blazor Console UI Rules

Reference: `backend/Console/Pages/Match/Match.razor`

## Rule 1: Early Return for State Guards (CRITICAL)

Exit as early as possible. Never nest main content inside `else`.

```razor
@* Correct — early return *@
@if (_isLoading)
{
    <div class="flex items-center justify-center py-16">
        <LucideIcon Name="loader-circle" Size="32" class="animate-spin text-primary"/>
    </div>
    return;
}

@if (_data == null)
{
    <BbAlert Variant="AlertVariant.Danger">
        <ChildContent>
            <BbAlertTitle>Not Found</BbAlertTitle>
            <BbAlertDescription>Item not found</BbAlertDescription>
        </ChildContent>
    </BbAlert>
    return;
}

@* Main content here — no nesting *@
<div>...</div>
```

```razor
@* Wrong — deeply nested content *@
@if (_isLoading)
{
    <Spinner/>
}
else if (_data == null)
{
    <NotFound/>
}
else
{
    <div>...main content buried in else...</div>
}
```

## Rule 2: Inject Only in @code Block (CRITICAL)

Never use `@inject` directive in markup. Always use `[Inject]` attribute in the `@code` section.

```razor
@* Wrong *@
@inject ToastService ToastService
@inject NavigationManager Nav

@* Correct — in @code block *@
@code {
    [Inject] ToastService ToastService { get; set; } = null!;
    [Inject] NavigationManager Nav { get; set; } = null!;
}
```

Exception: `@inherits UiComponent` is a directive, not injection — it stays in markup.

## Rule 3: Extract Components for Collections

When rendering a collection of items, always extract a separate component.

```razor
@* Correct — extracted component *@
@foreach (var participant in _participants)
{
    <MatchParticipant
        Name="@participant.Name"
        CurrentRating="@participant.CurrentRating"
        IsWinner="@participant.IsWinner"/>
}
```

```razor
@* Wrong — inline rendering of complex items *@
@foreach (var participant in _participants)
{
    <div class="rounded-lg border p-4">
        <h3>@participant.Name</h3>
        <span>@participant.CurrentRating</span>
        @* ...30 more lines of markup... *@
    </div>
}
```

Extracted component rules:
- All data via `[Parameter, EditorRequired]`
- No business logic — pure presentation
- Place in the same folder as the parent page

## Rule 4: Inherit UiComponent for Reactive Pages

Pages that subscribe to reactive state must inherit from `UiComponent`.

```razor
@inherits UiComponent

@code {
    [Inject] public IClusterFeatures ClusterFeatures { get; set; } = null!;

    private ClusterFeaturesState _state = new();

    protected override Task OnSetup(IReadOnlyLifetime lifetime)
    {
        ClusterFeatures.View(lifetime, state =>
        {
            _state = state;
            InvokeAsync(StateHasChanged).NoAwait();
        });

        return Task.CompletedTask;
    }
}
```

When to use `UiComponent`:
- Page subscribes to `ViewableProperty`, `ViewableList`, or `EventSource`
- Page needs Lifetime-managed subscriptions

When NOT to use:
- Simple data fetch in `OnInitializedAsync` (use plain `ComponentBase`)

## Rule 5: Loading Spinner Pattern

Standard loading indicator:

```razor
<div class="flex items-center justify-center py-16">
    <LucideIcon Name="loader-circle" Size="32" class="animate-spin text-primary"/>
</div>
```

## Rule 6: @code Block Order

```razor
@code {
    // 1. [Parameter] properties
    [Parameter] public string Id { get; set; } = string.Empty;

    // 2. [Inject] dependencies
    [Inject] public IOrleans Orleans { get; set; } = null!;
    [Inject] ToastService ToastService { get; set; } = null!;

    // 3. Private fields
    private bool _isLoading = true;
    private MyState? _state;

    // 4. Records / nested types
    private record ItemData(string Name, int Value);

    // 5. Lifecycle methods (OnInitializedAsync / OnSetup)
    protected override async Task OnInitializedAsync() { ... }

    // 6. Private methods
    private async Task LoadData() { ... }
}
```

## Rule 7: Error Handling

Use try-catch with `ToastService` for user-facing errors:

```csharp
try
{
    await LoadData();
}
catch (Exception ex)
{
    ToastService.Error($"Error loading data: {ex.Message}", "Error");
}
```
