namespace Frontend.Services;

/// <summary>
/// Public origin the site presents to search engines.
/// </summary>
/// <remarks>
/// post-radio.ru and post-radio.com both resolve to this host and serve identical pages, so
/// every absolute url in page metadata has to name one chosen domain. Without that, crawlers
/// index the two domains as duplicates and split the ranking signals between them.
/// </remarks>
public sealed class SiteOptions
{
    public string BaseUrl { get; init; } = "https://post-radio.ru";

    public string Origin => BaseUrl.TrimEnd('/');

    public string Url(string path) => $"{Origin}/{path.TrimStart('/')}";
}
