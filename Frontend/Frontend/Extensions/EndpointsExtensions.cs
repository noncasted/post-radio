using Microsoft.AspNetCore.Mvc;

namespace Frontend.Extensions;

public static class EndpointsExtensions
{
    public static IEndpointRouteBuilder AddEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("image/refresh", ([FromServices] IImageAPI api) => api.Refresh());

        builder.MapPost("image/getNext", ([FromBody] ImageRequest request, [FromServices] IImageAPI api)
            => api.GetNext(request));

        return builder;
    }
}