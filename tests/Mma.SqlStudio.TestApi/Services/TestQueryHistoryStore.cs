using Dapper;
using Microsoft.Data.SqlClient;

namespace Mma.SqlStudio.TestApi.Services;

public sealed class TestQueryHistoryStore
{
    private const string TableName = "__TestQueryHistory";
    private readonly string _connectionString =
        "data source=localhost;initial catalog=LandFeesDB;persist security info=True;TrustServerCertificate=True; user id=sa;password=Abc@1234;MultipleActiveResultSets=True;Max Pool Size=200;";

    public async Task AppendAsync(string query, DateTime executedAt)
    {
        await using var connection = new SqlConnection(_connectionString);
        await EnsureTableAsync(connection);
        await connection.ExecuteAsync(
            "INSERT INTO dbo.[__TestQueryHistory] ([Timestamp], [QueryText]) VALUES (@ExecutedAt, @QueryText)",
            new { ExecutedAt = executedAt, QueryText = query });
    }

    public async Task<IReadOnlyList<HistoryEntry>> GetRecentAsync()
    {
        await using var connection = new SqlConnection(_connectionString);
        await EnsureTableAsync(connection);
        var entries = await connection.QueryAsync<HistoryEntry>(
            "SELECT TOP (100) [Timestamp] AS [ExecutedAt], [QueryText] FROM dbo.[__TestQueryHistory] ORDER BY [Timestamp] DESC");
        return entries.AsList();
    }

    private static Task EnsureTableAsync(SqlConnection connection) => connection.ExecuteAsync(@"
IF OBJECT_ID(N'dbo.__TestQueryHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[__TestQueryHistory] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Timestamp] DATETIME2 NOT NULL,
        [QueryText] NVARCHAR(MAX) NOT NULL
    );
END");

    public sealed class HistoryEntry
    {
        public DateTime ExecutedAt { get; set; }
        public string QueryText { get; set; } = "";
    }
}
