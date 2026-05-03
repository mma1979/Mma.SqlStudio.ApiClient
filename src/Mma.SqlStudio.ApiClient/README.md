# Mma.SqlStudio.ApiClient

![Mma.SqlStudio Logo](https://raw.githubusercontent.com/mma1979/sql-editor/main/src/Mma.SqlStudio.ApiClient/icon.png)

**A modern, highly customizable, embeddable SQL Server Object Explorer and Query Editor for .NET.**

[![NuGet](https://img.shields.io/nuget/v/Mma.SqlStudio.ApiClient.svg?style=flat-square)](https://www.nuget.org/packages/Mma.SqlStudio.ApiClient)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-blueviolet?style=flat-square)](#)
[![License](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](LICENSE)

---

Mma.SqlStudio.ApiClient is packaged as a Razor Class Library (RCL), making it incredibly simple to drop a full-featured SQL development environment into any existing ASP.NET Core application.

## ✨ Features

- 🗄️ **SQL Object Explorer**: Browse databases, schemas, tables, views, and stored procedures seamlessly.
- ✍️ **Query Editor**: Execute queries with full syntax highlighting and a responsive results grid.
- 🎨 **Modern UI**: Clean, responsive, and dynamic interface built with vanilla CSS. Dark and Light mode supported!
- 🔌 **Embeddable**: Drop into any ASP.NET Core application via Minimal APIs and Razor Pages in just a few lines of code.
- ⚙️ **Highly Configurable**: Control routing, application naming, default connections, and schema loading.
- 🔒 **Customizable Authorization**: Secure your SQL Studio instance by applying custom endpoint and page authorization filters, including built-in support for HTTP Basic Auth or your own custom logic.

## 🚀 Getting Started

### 1. Install the NuGet Package

Add the package to your project using the .NET CLI:

```bash
dotnet add package Mma.SqlStudio.ApiClient --version 1.3.1
```

### 2. Configure Services

Register the required services in your `Program.cs`. You can customize the studio behavior by configuring the `SqlStudioOptions`.

```csharp
builder.Services.AddRazorPages();

// Add and configure SQL Studio
builder.Services.AddSqlStudio(options => 
{
    options.Route = "/sql-studio";
    options.AppName = "Mma SQL Studio";
    
    options.ApiConfig = new()
    {
        QueryEndPoint = "https://api.example.com/sql/query",
        ExecuteEndPoint = "https://api.example.com/sql/execute",
        SchemaEndPoint = "https://api.example.com/sql/schema",
        HealthEndpoint = "https://api.example.com/health",
        AuthHeaders = new Dictionary<string, string>
        {
            { "Authorization", "Bearer your-token" }
        }
    };
    
    // Optional: UI Configuration
    options.EnableSchemaLoad = true;
    options.Theme = "Dark"; // "Dark" or "Light"
    
    // Optional: Object Filtering
    options.ExcludedSchemas = ["guest", "temp"]; 
    options.ExcludedObjects = ["Logs", "InternalTable"];
    
    // Optional: Authorization Filter
    options.AuthFilter = ctx => 
    {
        // Example: HTTP Basic Auth (admin:password)
        if (ctx.Request.Headers.TryGetValue("Authorization", out var authHeader) && 
            authHeader.ToString().StartsWith("Basic "))
        {
            var token = authHeader.ToString().Substring("Basic ".Length).Trim();
            return token == "YWRtaW46cGFzc3dvcmQ=";
        }
        
        ctx.Response.Headers.WWWAuthenticate = "Basic realm=\"SqlStudio\"";
        return false;
    };
    
    // Set to null to return 401 Unauthorized instead of redirecting
    // Alternatively, provide a path like "/access-denied" to redirect rejected requests
    options.UnauthorizedRedirectUrl = null;
});
```

### 3. Map Endpoints

Ensure static files and endpoints are mapped properly in your middleware pipeline:

```csharp
// Required to serve the embedded CSS and JS files from the RCL
app.UseStaticFiles(); 
// Tip: Use app.MapStaticAssets() for .NET 9+

app.MapRazorPages();

// Map the API endpoints required by SQL Studio
app.MapSqlStudioEndpoints();
```

```bash
dotnet run
```

## 🔌 API Specification

To use `Mma.SqlStudio.ApiClient`, you need a backend API that implements the following endpoints. You can find the full **[OpenAPI Specification (YAML)](openapi.yaml)** in the repository for easier recreation.

### 1. Query Endpoint (`POST`)
Used for executing `SELECT` statements.
- **Request Body**: `{ "query": "string" }`
- **Response**: `QueryResult` object containing rows as a list of dictionaries.

### 2. Execute Endpoint (`POST`)
Used for statements that modify state (`INSERT`, `UPDATE`, `DELETE`, etc.).
- **Request Body**: `{ "query": "string" }`
- **Response**: `QueryResult` object (rows usually empty).

### 3. Schema Endpoint (`GET`)
Returns the database structure.
- **Response**: A hierarchical list of schemas, their categories (Tables, Views), and object names.

### 4. Health Endpoint (`GET`)
Simple check to verify backend connectivity.
- **Response**: Any `2xx` status code.

## 📸 Screenshots

**Dark Theme**
![dark theme](https://raw.githubusercontent.com/mma1979/sql-editor/main/src/Mma.SqlStudio.ApiClient/dark.png)

**Light Theme**
![dark theme](https://raw.githubusercontent.com/mma1979/sql-editor/main/src/Mma.SqlStudio.ApiClient/light.png)

## 📝 License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
