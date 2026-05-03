using Mma.SqlStudio.TestApp.Components;
using Mma.SqlStudio.ApiClient.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSqlStudio(options =>
{
    options.ApiConfig = new()
    {
        QueryEndPoint = "https://localhost:7138/api/sql/query",
        ExecuteEndPoint = "https://localhost:7138/api/sql/execute",
        SchemaEndPoint = "https://localhost:7138/api/sql/schema",
        HealthEndpoint = "https://localhost:7138/api/health",
        AuthHeaders = new Dictionary<string, string>
        {
            { "X-Api-Key", "test-key" }
        }
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorPages();
app.MapSqlStudioEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();


app.Run();
