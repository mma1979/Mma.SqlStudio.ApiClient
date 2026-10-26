using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Mma.SqlStudio.TestApi.Services;

namespace Mma.SqlStudio.TestApi.Controllers
{
    [ApiController]
    [Route("api/sql")]
    public class SqlApiController : ControllerBase
    {
        private readonly string _connectionString = "data source=localhost;initial catalog=LandFeesDB;persist security info=True;TrustServerCertificate=True; user id=sa;password=Abc@1234;MultipleActiveResultSets=True;Max Pool Size=200;";
        private readonly TestApiEncryptionService _encryption;
        private readonly TestQueryHistoryStore _history;
        private readonly ILogger<SqlApiController> _logger;

        public SqlApiController(
            TestApiEncryptionService encryption,
            TestQueryHistoryStore history,
            ILogger<SqlApiController> logger)
        {
            _encryption = encryption;
            _history = history;
            _logger = logger;
        }

        [HttpPost("schema")]
        public Task<IActionResult> GetSchema([FromBody] JsonElement envelope) =>
            HandleEncryptedAsync(envelope, "schema", ["schema.get"], async payload =>
            {
                
                var excludedSchemas = ReadStringSet(payload, "excludedSchemas");
                var excludedObjects = ReadStringSet(payload, "excludedObjects");
                return await GetSchemaDataAsync(excludedSchemas, excludedObjects);
            });

        [HttpPost("query")]
        public Task<IActionResult> ExecuteQuery([FromBody] JsonElement envelope) =>
            HandleEncryptedAsync(envelope, "query", ["query.execute"], async payload =>
            {
                var query = payload.GetProperty("query").GetString() ?? "";
                return await ExecuteSqlAsync(query);
            });

        // Keep the conventional execute route, but require the same encrypted query envelope.
        [HttpPost("execute")]
        public Task<IActionResult> ExecuteCommand([FromBody] JsonElement envelope) =>
            HandleEncryptedAsync(envelope, "query", ["query.execute"], async payload =>
            {
                var query = payload.GetProperty("query").GetString() ?? "";
                return await ExecuteSqlAsync(query);
            });

        [HttpPost("history")]
        public Task<IActionResult> History([FromBody] JsonElement envelope) =>
            HandleEncryptedAsync(envelope, "history", ["history.list", "history.append"], async payload =>
            {
                var operation = payload.GetProperty("operation").GetString();
                if (operation == "history.append")
                {
                    var query = payload.GetProperty("query").GetString() ?? "";
                    var executedAt = payload.GetProperty("executedAt").GetDateTimeOffset().UtcDateTime;
                    try
                    {
                        await _history.AppendAsync(query, executedAt);
                        return new { success = true };
                    }
                    catch (Exception)
                    {
                        _logger.LogWarning("Unable to persist encrypted query history.");
                        return new { success = false };
                    }
                }

                try
                {
                    var items = await _history.GetRecentAsync();
                    return new { items, tableName = "__TestQueryHistory" };
                }
                catch (Exception)
                {
                    _logger.LogWarning("Unable to read encrypted query history.");
                    return new { items = Array.Empty<TestQueryHistoryStore.HistoryEntry>(), tableName = "__TestQueryHistory" };
                }
            });

        [HttpGet("health")]
        public async Task<IActionResult> Health()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                return Ok(new { healthy = true });
            }
            catch
            {
                return Ok(new { healthy = false });
            }
        }

        private async Task<IActionResult> HandleEncryptedAsync(
            JsonElement envelope,
            string route,
            string[] allowedOperations,
            Func<JsonElement, Task<object>> createResponse)
        {
            TestApiEncryptionService.DecryptedRequest decrypted;
            try
            {
                decrypted = _encryption.Decrypt(envelope, route);
            }
            catch (InvalidDataException exception)
            {
                _logger.LogWarning("Rejected E2EE {Route} envelope: {Reason}", route, exception.Message);
                return BadRequest();
            }
            catch (Exception exception) when (exception is CryptographicException or JsonException or
                                                FormatException or InvalidOperationException or
                                                ArgumentException or NullReferenceException)
            {
                _logger.LogWarning("Rejected E2EE {Route} envelope ({FailureType}).", route, exception.GetType().Name);
                return BadRequest();
            }

            using (decrypted)
            {
                var payload = decrypted.Payload.RootElement;
                if (!payload.TryGetProperty("operation", out var operationElement) ||
                    operationElement.ValueKind != JsonValueKind.String ||
                    !allowedOperations.Contains(operationElement.GetString(), StringComparer.Ordinal))
                {
                    _logger.LogWarning("Rejected E2EE {Route} payload because its operation does not match the endpoint.", route);
                    return BadRequest();
                }

                try
                {
                    var clearResponse = await createResponse(payload);
                    return Ok(_encryption.EncryptResponse(clearResponse, decrypted));
                }
                catch (Exception)
                {
                    _logger.LogWarning("Encrypted {Route} operation failed.", route);
                    return StatusCode(StatusCodes.Status500InternalServerError);
                }
            }
        }

        private static HashSet<string> ReadStringSet(JsonElement payload, string propertyName)
        {
            if (!payload.TryGetProperty(propertyName, out var values) || values.ValueKind != JsonValueKind.Array)
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            return values.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString()!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private async Task<List<SchemaNode>> GetSchemaDataAsync(HashSet<string> excludedSchemas, HashSet<string> excludedObjects)
        {
            try
            {
                using IDbConnection db = new SqlConnection(_connectionString);
                const string sql = @"
                    SELECT TABLE_SCHEMA as SchemaName, TABLE_NAME as ObjectName, TABLE_TYPE as ObjectType
                    FROM INFORMATION_SCHEMA.TABLES
                    UNION ALL
                    SELECT ROUTINE_SCHEMA as SchemaName, ROUTINE_NAME as ObjectName, 'PROCEDURE' as ObjectType
                    FROM INFORMATION_SCHEMA.ROUTINES
                    WHERE ROUTINE_TYPE = 'PROCEDURE'
                    ORDER BY SchemaName, ObjectName";

                var results = await db.QueryAsync<SchemaItem>(sql);
                return results
                    .Where(row => !excludedSchemas.Contains(row.SchemaName))
                    .GroupBy(row => row.SchemaName)
                    .Select(group => new SchemaNode
                    {
                        Name = group.Key,
                        Children = new List<CategoryNode>
                        {
                            CreateCategory("Tables", group.Where(row => row.ObjectType == "BASE TABLE"), excludedObjects),
                            CreateCategory("Views", group.Where(row => row.ObjectType == "VIEW"), excludedObjects),
                            CreateCategory("Procedures", group.Where(row => row.ObjectType == "PROCEDURE"), excludedObjects)
                        }
                    }).ToList();
            }
            catch (Exception)
            {
                _logger.LogWarning("Unable to load schema for encrypted request.");
                return new List<SchemaNode>();
            }
        }

        private static CategoryNode CreateCategory(
            string name,
            IEnumerable<SchemaItem> entries,
            HashSet<string> excludedObjects) =>
            new()
            {
                Name = name,
                Objects = entries.Select(entry => entry.ObjectName)
                    .Where(objectName => !excludedObjects.Contains(objectName))
                    .ToList()
            };

        private async Task<QueryResult> ExecuteSqlAsync(string sql)
        {
            var result = new QueryResult();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();
                if (reader.FieldCount > 0)
                {
                    var table = new DataTable();
                    table.Load(reader);
                    var rows = new List<Dictionary<string, object?>>();
                    var columns = table.Columns.Cast<DataColumn>().Select(column => column.ColumnName!).ToList();
                    foreach (DataRow row in table.Rows)
                    {
                        var values = new Dictionary<string, object?>();
                        foreach (var column in columns)
                            values[column] = row[column] == DBNull.Value ? null : row[column];
                        rows.Add(values);
                    }

                    result.Rows = rows;
                    result.Message = $"Success: {table.Rows.Count} rows returned.";
                    result.IsQuery = true;
                }
                else
                {
                    result.Message = $"Success: Command executed. {reader.RecordsAffected} rows affected.";
                }
                result.Success = true;
            }
            catch (Exception exception)
            {
                result.Success = false;
                result.Message = "Error: " + exception.Message;
            }
            return result;
        }

        private sealed class SchemaItem
        {
            public string SchemaName { get; set; } = "";
            public string ObjectName { get; set; } = "";
            public string ObjectType { get; set; } = "";
        }

        private sealed class SchemaNode
        {
            public string Name { get; set; } = "";
            public List<CategoryNode> Children { get; set; } = new();
        }

        private sealed class CategoryNode
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

        private sealed class QueryResult
        {
            public bool Success { get; set; }
            public bool IsQuery { get; set; }
            public string Message { get; set; } = "";
            public List<Dictionary<string, object?>> Rows { get; set; } = new();
        }
    }
}
