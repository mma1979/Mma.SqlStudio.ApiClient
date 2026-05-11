using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Mma.SqlStudio.ApiClient.Services;

namespace Mma.SqlStudio.ApiClient.Extensions
{
    public static class EndpointRouteBuilderExtensions
    {
        public static IEndpointRouteBuilder MapSqlStudioEndpoints(this IEndpointRouteBuilder endpoints)
        {
            var options = endpoints.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Models.SqlStudioOptions>>().Value;
            var apiPath = $"/api/{options.Route.TrimStart('/')}";
            var group = endpoints.MapGroup(apiPath);

            if (options.AuthFilter is not null)
            {
                group.AddEndpointFilter(new Filters.SqlStudioAuthEndpointFilter(options.AuthFilter, options.UnauthorizedRedirectUrl));
            }

            group.MapGet("/schema", async (SchemaService schemaService) =>
            {
                var schema = await schemaService.GetSchemaAsync();
                return Results.Ok(schema);
            });

            group.MapPost("/query", async (QueryRequest request, SchemaService schemaService) =>
            {
                var result = await schemaService.ExecuteQueryAsync(request.Query, request.Cookies, request.LocalStorage);
                return Results.Ok(result);
            });

            group.MapGet("/history", async (SchemaService schemaService) =>
            {
                var history = await schemaService.GetHistoryAsync();
                return Results.Ok(new { items = history, tableName = options.HistoryTableName });
            });

            group.MapGet("/health", async (SchemaService schemaService) =>
            {
                var isHealthy = await schemaService.CheckHealthAsync();
                return Results.Ok(new { healthy = isHealthy });
            });

            return endpoints;
        }
    }

    public class QueryRequest
    {
        public string Query { get; set; } = "";
        public string? Cookies { get; set; }
        public string? LocalStorage { get; set; }
    }
}
