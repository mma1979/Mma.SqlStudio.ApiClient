using Mma.SqlStudio.ApiClient.Extensions;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddRazorPages();

builder.Services.AddHttpClient("SqlStudioClient", client =>
{
    client.BaseAddress = new Uri("https://localhost:7211");
});

builder.Services.AddSqlStudio(options =>
{
    options.Route = "/script-runner";
    options.AppName = "Test SQL Studio";
    options.EnableSchemaLoad = true;
    options.ExcludedSchemas = new List<string> { "HangFire", "farz", "meeting", "Objection", "TEMPInvoice", "Violation" };
    options.ExcludedObjects = new List<string> { "ApiLogs", "AppUsers" };

    options.Theme = "Light";

    options.ApiConfig = new()
    {
        // Using full URLs as requested
        QueryEndPoint = "https://localhost:7211/api/sql/query",
        ExecuteEndPoint = "https://localhost:7211/api/sql/execute",
        SchemaEndPoint = "https://localhost:7211/api/sql/schema",
        HealthEndpoint = "https://localhost:7211/api/sql/health"
    };

    // Example Auth Filter: HTTP Basic Auth (admin:password)
    options.AuthFilter = ctx =>
    {
        if (ctx.Request.Headers.TryGetValue("Authorization", out var authHeader) &&
            authHeader.ToString().StartsWith("Basic "))
        {
            var token = authHeader.ToString().Substring("Basic ".Length).Trim();
            // "admin:password" base64 encoded is "YWRtaW46cGFzc3dvcmQ="
            return token == "YWRtaW46cGFzc3dvcmQ=";
        }

        ctx.Response.Headers.WWWAuthenticate = "Basic realm=\"SqlStudio\"";
        return false;
    };

    options.UnauthorizedRedirectUrl = null;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAntiforgery();
app.MapStaticAssets();

app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();
app.MapSqlStudioEndpoints();

app.Run();
