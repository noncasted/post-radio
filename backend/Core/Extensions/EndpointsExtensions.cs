using Audio;
using Images;
using Microsoft.AspNetCore.Mvc;

namespace Core;

public static class EndpointsExtensions
{
    public static IEndpointRouteBuilder AddEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("audio/refresh", ([FromServices] IAudioAPI api) => api.Refresh());

        builder.MapPost("audio/getNext", ([FromBody] GetNextTrackRequest request, [FromServices] IAudioAPI api)
            => api.GetNext(request));

        builder.MapGet("image/refresh", ([FromServices] IImageAPI api) => api.Refresh());

        builder.MapPost("image/getNext", ([FromBody] ImageRequest request, [FromServices] IImageAPI api)
            => api.GetNext(request));

        builder.MapGet("/", ([FromServices] BuildUrl url) => Results.Redirect(url.Value));
        return builder;
    }
}