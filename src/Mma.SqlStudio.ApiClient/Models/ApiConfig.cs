using System.Net.Http;
using System.Collections.Generic;

namespace Mma.SqlStudio.ApiClient.Models
{
    public class ApiConfig
    {
        /// <summary>
        /// The HttpClient instance to use for API calls.
        /// If null, a default HttpClient will be created.
        /// </summary>
        public HttpClient? HttpClient { get; set; }

        /// <summary>
        /// The endpoint URL for executing queries (SELECT statements).
        /// Example: "https://myapi.com/api/sql/query"
        /// </summary>
        public string QueryEndPoint { get; set; } = "";

        /// <summary>
        /// The endpoint URL for executing non-query commands (INSERT/UPDATE/DELETE).
        /// Example: "https://myapi.com/api/sql/execute"
        /// </summary>
        public string ExecuteEndPoint { get; set; } = "";

        /// <summary>
        /// The endpoint URL for health checks.
        /// Example: "https://myapi.com/api/health"
        /// </summary>
        public string HealthEndpoint { get; set; } = "";

        /// <summary>
        /// The endpoint URL for fetching database schema.
        /// Example: "https://myapi.com/api/sql/schema"
        /// </summary>
        public string SchemaEndPoint { get; set; } = "";

        /// <summary>
        /// Optional. Dictionary of authentication headers to send with each request.
        /// Example: { "Authorization", "Bearer ..." }
        /// </summary>
        public Dictionary<string, string>? AuthHeaders { get; set; }
    }
}
