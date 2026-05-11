using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Data;

namespace Mma.SqlStudio.TestApi.Controllers
{
    [ApiController]
    [Route("api/sql")]
    public class SqlApiController : ControllerBase
    {
        private readonly string _connectionString = "data source=localhost;initial catalog=LandFeesDB;persist security info=True;TrustServerCertificate=True; user id=sa;password=Abc@1234;MultipleActiveResultSets=True;Max Pool Size=200;";

        [HttpGet("schema")]
        public async Task<IActionResult> GetSchema()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var sql = @"
                SELECT 
                    TABLE_SCHEMA as SchemaName, 
                    TABLE_NAME as ObjectName, 
                    TABLE_TYPE as ObjectType 
                FROM INFORMATION_SCHEMA.TABLES
                UNION ALL
                SELECT 
                    ROUTINE_SCHEMA as SchemaName,
                    ROUTINE_NAME as ObjectName,
                    'PROCEDURE' as ObjectType
                FROM INFORMATION_SCHEMA.ROUTINES
                WHERE ROUTINE_TYPE = 'PROCEDURE'
                ORDER BY SchemaName, ObjectName";

            try
            {
                var results = await db.QueryAsync<SchemaItem>(sql);
                var nodes = results.GroupBy(r => r.SchemaName)
                    .Select(g => new
                    {
                        Name = g.Key,
                        Children = new[]
                        {
                            new { Name = "Tables", Objects = g.Where(x => x.ObjectType == "BASE TABLE").Select(x => x.ObjectName).ToList() },
                            new { Name = "Views", Objects = g.Where(x => x.ObjectType == "VIEW").Select(x => x.ObjectName).ToList() },
                            new { Name = "Procedures", Objects = g.Where(x => x.ObjectType == "PROCEDURE").Select(x => x.ObjectName).ToList() }
                        }
                    }).ToList();
                return Ok(nodes);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("query")]
        public async Task<IActionResult> ExecuteQuery([FromBody] QueryRequest request)
        {
            return await ProcessQuery(request.Query, true);
        }

        [HttpPost("execute")]
        public async Task<IActionResult> ExecuteCommand([FromBody] QueryRequest request)
        {
            return await ProcessQuery(request.Query, false);
        }

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

        private async Task<IActionResult> ProcessQuery(string sql, bool isQuery)
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
                    var dt = new DataTable();
                    dt.Load(reader);

                    var rows = new List<Dictionary<string, object>>();
                    var columns = dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName!).ToList();

                    foreach (DataRow row in dt.Rows)
                    {
                        var dict = new Dictionary<string, object>();
                        foreach (var col in columns)
                        {
                            dict[col] = row[col] == DBNull.Value ? null : row[col];
                        }
                        rows.Add(dict);
                    }

                    result.Rows = rows;
                    result.Message = $"Success: {dt.Rows.Count} rows returned.";
                    result.IsQuery = true;
                }
                else
                {
                    int affected = reader.RecordsAffected;
                    result.Message = $"Success: Command executed. {affected} rows affected.";
                    result.IsQuery = false;
                }
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = "Error: " + ex.Message;
            }
            return Ok(result);
        }

        private class SchemaItem
        {
            public string SchemaName { get; set; } = "";
            public string ObjectName { get; set; } = "";
            public string ObjectType { get; set; } = "";
        }

        public class QueryRequest
        {
            public string Query { get; set; } = "";
            public string? Cookies { get; set; }
            public string? LocalStorage { get; set; }
        }

        public class QueryResult
        {
            public bool Success { get; set; }
            public bool IsQuery { get; set; }
            public string Message { get; set; } = "";
            public List<Dictionary<string, object>> Rows { get; set; } = new();
        }
    }
}
