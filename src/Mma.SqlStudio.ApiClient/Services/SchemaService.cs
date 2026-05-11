using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Mma.SqlStudio.ApiClient.Models;

namespace Mma.SqlStudio.ApiClient.Services
{
    public class SchemaService
    {
        private readonly SqlStudioOptions _options;
        private readonly HttpClient _httpClient;

        public SchemaService(IOptions<SqlStudioOptions> options, IHttpClientFactory httpClientFactory)
        {
            _options = options.Value;
            
            if (_options.ApiConfig.HttpClient != null)
            {
                _httpClient = _options.ApiConfig.HttpClient;
            }
            else
            {
                _httpClient = httpClientFactory.CreateClient("SqlStudioClient");
            }
        }

        private void ApplyAuthHeaders(HttpRequestMessage request)
        {
            if (_options.ApiConfig.AuthHeaders != null)
            {
                foreach (var header in _options.ApiConfig.AuthHeaders)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
        }

        public async Task<List<SchemaNode>> GetSchemaAsync()
        {
            if (string.IsNullOrEmpty(_options.ApiConfig.SchemaEndPoint))
                return new List<SchemaNode>();

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, _options.ApiConfig.SchemaEndPoint);
                ApplyAuthHeaders(request);
                
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                
                var results = await response.Content.ReadFromJsonAsync<List<SchemaNode>>();
                if (results == null) return new List<SchemaNode>();

                // Client-side filtering
                if (_options.ExcludedSchemas != null && _options.ExcludedSchemas.Any())
                {
                    results = results.Where(s => !_options.ExcludedSchemas.Contains(s.Name, StringComparer.OrdinalIgnoreCase)).ToList();
                }

                if (_options.ExcludedObjects != null && _options.ExcludedObjects.Any())
                {
                    foreach (var schema in results)
                    {
                        foreach (var category in schema.Children)
                        {
                            category.Objects = category.Objects.Where(o => !_options.ExcludedObjects.Contains(o, StringComparer.OrdinalIgnoreCase)).ToList();
                        }
                    }
                }

                return results;
            }
            catch
            {
                return new List<SchemaNode>();
            }
        }

        private bool _historyTableInitialized = false;

        private async Task EnsureHistoryTableAsync()
        {
            if (!_options.AllowHistoryLog || !_options.CreateTable || _historyTableInitialized)
                return;

            try
            {
                // Simple check and create. We use the ExecuteEndPoint for this.
                // Note: The specific SQL might vary by DB provider, but we try a standard approach.
                // Most providers support some form of this or we just try-catch.
                string createSql = $@"
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[{_options.HistoryTableName}]') AND type in (N'U'))
BEGIN
    CREATE TABLE [{_options.HistoryTableName}] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [ExecutedAt] DATETIME DEFAULT GETDATE(),
        [QueryText] NVARCHAR(MAX),
        [Cookies] NVARCHAR(MAX),
        [LocalStorage] NVARCHAR(MAX)
    )
END";
                
                var request = new HttpRequestMessage(HttpMethod.Post, _options.ApiConfig.ExecuteEndPoint);
                ApplyAuthHeaders(request);
                request.Content = JsonContent.Create(new { query = createSql });

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    _historyTableInitialized = true;
                }
            }
            catch
            {
                // Ignore errors in table creation, maybe it already exists or provider is different
                _historyTableInitialized = true; 
            }
        }

        private async Task LogHistoryAsync(string sql, string? cookies, string? localStorage)
        {
            if (!_options.AllowHistoryLog) return;

            await EnsureHistoryTableAsync();

            try
            {
                string escapedSql = sql.Replace("'", "''");
                string escapedCookies = cookies?.Replace("'", "''") ?? "";
                string escapedLocalStorage = localStorage?.Replace("'", "''") ?? "";

                string finalLogSql = $@"INSERT INTO [{_options.HistoryTableName}] (QueryText, Cookies, LocalStorage) VALUES ('{escapedSql}', '{escapedCookies}', '{escapedLocalStorage}')";

                var request = new HttpRequestMessage(HttpMethod.Post, _options.ApiConfig.ExecuteEndPoint);
                ApplyAuthHeaders(request);
                request.Content = JsonContent.Create(new { query = finalLogSql });

                await _httpClient.SendAsync(request);
            }
            catch
            {
                // Silently fail logging
            }
        }

        public async Task<QueryResult> ExecuteQueryAsync(string sql, string? cookies = null, string? localStorage = null)
        {
            var result = new QueryResult();
            try
            {
                bool isMutation = IsMutationQuery(sql);
                string endpoint = isMutation ? _options.ApiConfig.ExecuteEndPoint : _options.ApiConfig.QueryEndPoint;

                if (string.IsNullOrEmpty(endpoint))
                {
                    result.Success = false;
                    result.Message = $"Error: {(isMutation ? "Execute" : "Query")} endpoint is not configured.";
                    return result;
                }

                var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                ApplyAuthHeaders(request);
                request.Content = JsonContent.Create(new { query = sql });

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var apiResult = await response.Content.ReadFromJsonAsync<QueryResult>();
                if (apiResult != null)
                {
                    if (apiResult.Success)
                    {
                        await LogHistoryAsync(sql, cookies, localStorage);
                    }
                    return apiResult;
                }

                result.Success = false;
                result.Message = "Error: Invalid response from API.";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = "Error: " + ex.Message;
            }
            return result;
        }

        public async Task<List<HistoryItem>> GetHistoryAsync()
        {
            if (!_options.AllowHistoryLog) return new List<HistoryItem>();

            try
            {
                string sql = $"SELECT TOP 100 [ExecutedAt], [QueryText], [Cookies], [LocalStorage] FROM [{_options.HistoryTableName}] ORDER BY [ExecutedAt] DESC";
                
                var request = new HttpRequestMessage(HttpMethod.Post, _options.ApiConfig.QueryEndPoint);
                ApplyAuthHeaders(request);
                request.Content = JsonContent.Create(new { query = sql });

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return new List<HistoryItem>();

                var result = await response.Content.ReadFromJsonAsync<QueryResult>();
                if (result == null || !result.Success) return new List<HistoryItem>();

                var history = new List<HistoryItem>();
                foreach (var row in result.Rows)
                {
                    history.Add(new HistoryItem
                    {
                        ExecutedAt = GetValue<DateTime>(row, "ExecutedAt", "Timestamp"),
                        QueryText = GetValue<string>(row, "QueryText", "Query") ?? "",
                        Cookies = GetValue<string>(row, "Cookies"),
                        LocalStorage = GetValue<string>(row, "LocalStorage")
                    });
                }
                return history;
            }
            catch
            {
                return new List<HistoryItem>();
            }
        }

        private T? GetValue<T>(Dictionary<string, object> row, params string[] keys)
        {
            foreach (var key in keys)
            {
                object? val = null;
                if (row.TryGetValue(key, out val)) { }
                else
                {
                    var match = row.Keys.FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
                    if (match != null) val = row[match];
                }

                if (val != null)
                {
                    if (val is System.Text.Json.JsonElement elem)
                    {
                        if (typeof(T) == typeof(DateTime)) return (T?)(object)elem.GetDateTime();
                        if (typeof(T) == typeof(string)) return (T?)(object)elem.GetString();
                        if (typeof(T) == typeof(int)) return (T?)(object)elem.GetInt32();
                        if (typeof(T) == typeof(long)) return (T?)(object)elem.GetInt64();
                    }
                    return (T?)Convert.ChangeType(val, typeof(T));
                }
            }
            return default;
        }

        public async Task<bool> CheckHealthAsync()
        {
            if (string.IsNullOrEmpty(_options.ApiConfig.HealthEndpoint))
                return true; // Assume healthy if not configured

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, _options.ApiConfig.HealthEndpoint);
                ApplyAuthHeaders(request);
                
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private bool IsMutationQuery(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) return false;
            var trimmedSql = sql.TrimStart().ToUpperInvariant();
            return trimmedSql.StartsWith("INSERT") || 
                   trimmedSql.StartsWith("UPDATE") || 
                   trimmedSql.StartsWith("DELETE") || 
                   trimmedSql.StartsWith("DROP") || 
                   trimmedSql.StartsWith("CREATE") || 
                   trimmedSql.StartsWith("ALTER");
        }
    }

    public class SchemaNode
    {
        public string Name { get; set; } = "";
        public List<CategoryNode> Children { get; set; } = new();
    }

    public class CategoryNode
    {
        public string Name { get; set; } = "";
        public List<string> Objects { get; set; } = new();
        
        public CategoryNode() { }
        public CategoryNode(string name, List<string> objects)
        {
            Name = name;
            Objects = objects;
        }
    }

    public class QueryResult
    {
        public bool Success { get; set; }
        public bool IsQuery { get; set; }
        public string Message { get; set; } = "";
        public List<Dictionary<string, object>> Rows { get; set; } = new();
        
        // Helper to get columns from rows
        public List<string> Columns => Rows.FirstOrDefault()?.Keys.ToList() ?? new List<string>();
    }

    public class HistoryItem
    {
        public DateTime ExecutedAt { get; set; }
        public string QueryText { get; set; } = "";
        public string? Cookies { get; set; }
        public string? LocalStorage { get; set; }
    }
}
