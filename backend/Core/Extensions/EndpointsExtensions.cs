using Audio;
using Microsoft.AspNetCore.Mvc;

namespace Core;

public static class EndpointsExtensions
{
    public static IEndpointRouteBuilder AddAudioEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("audio/refresh", ([FromServices] ISongsRepository repository) => repository.Refresh());

        builder.MapGet("audio/getNext", ([FromBody] int current, [FromServices] ISongProvider provider)
            => provider.GetNext(current));

        return builder;
    }
}