namespace Mma.SqlStudio.ApiClient.Models
{
    public class SqlStudioOptions
    {
        public ApiConfig ApiConfig { get; set; } = new();
        public string Route { get; set; } = "/sql-studio";
        public string AppName { get; set; } = "Mma SQL Studio";
        public bool EnableSchemaLoad { get; set; } = true;
        public List<string> ExcludedSchemas { get; set; } = new();
        public List<string> ExcludedObjects { get; set; } = new();

        /// <summary>
        /// The theme of the SqlStudio UI. Options: "Dark", "Light". Defaults to "Dark".
        /// </summary>
        public string Theme { get; set; } = "Dark";

        /// <summary>
        /// Optional. A predicate that receives the current HttpContext and returns true
        /// when the request is authorized to use SqlStudio. If null, no restriction is applied.
        /// </summary>
        public Func<Microsoft.AspNetCore.Http.HttpContext, bool>? AuthFilter { get; set; }

        /// <summary>
        /// The URL to redirect to when AuthFilter returns false.
        /// If null, a 401 Unauthorized response is returned instead.
        /// Defaults to "/".
        /// </summary>
        public string? UnauthorizedRedirectUrl { get; set; } = "/";
    }
}
