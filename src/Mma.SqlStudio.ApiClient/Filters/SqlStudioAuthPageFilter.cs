using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Mma.SqlStudio.ApiClient.Filters
{
    internal class SqlStudioAuthPageFilter : IAsyncPageFilter
    {
        private readonly Func<HttpContext, bool> _authFilter;
        private readonly string? _redirectUrl;

        public SqlStudioAuthPageFilter(Func<HttpContext, bool> authFilter, string? redirectUrl)
        {
            _authFilter = authFilter;
            _redirectUrl = redirectUrl;
        }

        public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

        public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
        {
            if (!_authFilter(context.HttpContext))
            {
                if (string.IsNullOrEmpty(_redirectUrl))
                    context.Result = new UnauthorizedResult();
                else
                    context.Result = new RedirectResult(_redirectUrl);
                return;
            }
            await next();
        }
    }
}
