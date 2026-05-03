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

        public async Task<QueryResult> ExecuteQueryAsync(string sql)
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
}
