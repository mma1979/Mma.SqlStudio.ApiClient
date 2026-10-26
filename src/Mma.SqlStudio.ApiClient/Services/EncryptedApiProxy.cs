using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Mma.SqlStudio.ApiClient.Models;

namespace Mma.SqlStudio.ApiClient.Services
{
    /// <summary>Relays encrypted protocol messages without inspecting their contents.</summary>
    public sealed class EncryptedApiProxy
    {
        private readonly SqlStudioOptions _options;
        private readonly HttpClient _httpClient;

        public EncryptedApiProxy(IOptions<SqlStudioOptions> options, IHttpClientFactory httpClientFactory)
        {
            _options = options.Value;
            _httpClient = _options.ApiConfig.HttpClient ?? httpClientFactory.CreateClient("SqlStudioClient");
        }

        public async Task ForwardAsync(HttpContext context, string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StreamContent(context.Request.Body)
            };

            if (context.Request.ContentType is not null)
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(context.Request.ContentType);

            if (_options.ApiConfig.AuthHeaders is not null)
            {
                foreach (var header in _options.ApiConfig.AuthHeaders)
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                context.RequestAborted);

            context.Response.StatusCode = (int)response.StatusCode;
            if (response.Content.Headers.ContentType is not null)
                context.Response.ContentType = response.Content.Headers.ContentType.ToString();
            if (response.Content.Headers.ContentLength.HasValue)
                context.Response.ContentLength = response.Content.Headers.ContentLength;

            await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
        }
    }
}
