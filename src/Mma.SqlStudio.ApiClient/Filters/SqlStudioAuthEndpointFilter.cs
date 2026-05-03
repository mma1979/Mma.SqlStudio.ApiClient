using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Mma.SqlStudio.ApiClient.Filters
{
    internal class SqlStudioAuthEndpointFilter : IEndpointFilter
    {
        private readonly Func<HttpContext, bool> _authFilter;
        private readonly string? _redirectUrl;

        public SqlStudioAuthEndpointFilter(Func<HttpContext, bool> authFilter, string? redirectUrl)
        {
            _authFilter = authFilter;
            _redirectUrl = redirectUrl;
        }

        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            if (!_authFilter(context.HttpContext))
            {
                if (string.IsNullOrEmpty(_redirectUrl))
                    return Results.Unauthorized();
                else
                    return Results.Redirect(_redirectUrl);
            }

            return await next(context);
        }
    }
}
