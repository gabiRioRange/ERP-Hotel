using ConsoleApp1.Application.Contracts.Mcp;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ConsoleApp1.API.Filters;

public class McpQuotaFilter(IMemoryCache cache, IOptions<McpQuotaOptions> optionsAccessor) : IEndpointFilter
{
    private readonly McpQuotaOptions _options = optionsAccessor.Value;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var client = http.User.FindFirst("sub")?.Value ?? "anonymous";
        var minuteKey = $"mcp-quota:{client}:{DateTime.UtcNow:yyyyMMddHHmm}";

        var counter = cache.GetOrCreate(minuteKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
            return 0;
        });

        if (counter >= _options.RequestsPerMinutePerClient)
        {
            return Results.Problem(
                title: "Quota excedida",
                detail: "Limite de requisições MCP por minuto excedido para o cliente.",
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        cache.Set(minuteKey, counter + 1, TimeSpan.FromMinutes(1));
        return await next(context);
    }
}