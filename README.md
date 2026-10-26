# Mma.SqlStudio.ApiClient

![Mma.SqlStudio Logo](https://raw.githubusercontent.com/mma1979/Mma.SqlStudio.ApiClient/refs/heads/main/src/Mma.SqlStudio.ApiClient/icon.png)

**A modern, highly customizable, embeddable SQL Server Object Explorer and Query Editor for .NET.**

[![NuGet](https://img.shields.io/nuget/v/Mma.SqlStudio.ApiClient.svg?style=flat-square)](https://www.nuget.org/packages/Mma.SqlStudio.ApiClient)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-blueviolet?style=flat-square)](#)
[![License](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](https://raw.githubusercontent.com/mma1979/Mma.SqlStudio.ApiClient/main/LICENSE)

---

Mma.SqlStudio.ApiClient is a Razor Class Library (RCL) that allows you to easily embed a full-featured SQL development environment into any ASP.NET Core application. It connects to a backend API to execute queries and manage database schema, providing a decoupled and secure way to interact with your data.

## ✨ Features

- 🗄️ **SQL Object Explorer**: Browse databases, schemas, tables, views, and stored procedures seamlessly.
- ✍️ **Query Editor**: Execute queries with full syntax highlighting and a responsive results grid.
- 🎨 **Modern UI**: Clean, responsive, and dynamic interface built with vanilla CSS. Dark and Light mode supported!
- 🔌 **Embeddable**: Drop into any ASP.NET Core application via Minimal APIs and Razor Pages in just a few lines of code.
- ⚙️ **Highly Configurable**: Control routing, application naming, and schema loading.
- 🕒 **Query History**: Dedicated sidebar tab for query history and easy script re-execution; browser metadata is sent only when E2EE is disabled.
- 🔒 **Customizable Authorization**: Secure your SQL Studio instance by applying custom endpoint and page authorization filters, including built-in support for HTTP Basic Auth.
- 🔐 **Optional end-to-end encryption**: Encrypt query, schema, and history payloads in the browser for a backend that implements the documented E2EE protocol. Disabled by default.

## 🚀 Getting Started

### 1. Install the NuGet Package

Add the package to your project using the .NET CLI:

```bash
dotnet add package Mma.SqlStudio.ApiClient --version 1.5.0
```

### 2. Configure Services

Register the required services in your `Program.cs`. You can customize the studio behavior by configuring the `SqlStudioOptions`.

```csharp
builder.Services.AddRazorPages();

// Configure the HttpClient that SQL Studio will use to talk to your backend API
builder.Services.AddHttpClient("SqlStudioClient", client =>
{
    client.BaseAddress = new Uri("https://your-api-url.com");
});

// Add and configure SQL Studio
builder.Services.AddSqlStudio(options => 
{
    options.Route = "/sql-studio";
    options.AppName = "Mma SQL Studio";
    
    options.ApiConfig = new()
    {
        QueryEndPoint = "api/sql/query",
        ExecuteEndPoint = "api/sql/execute",
        SchemaEndPoint = "api/sql/schema",
        HealthEndpoint = "api/health",
        AuthHeaders = new Dictionary<string, string>
        {
            { "X-Api-Key", "your-secret-key" }
        }
    };
    
    // Optional: UI Configuration
    options.EnableSchemaLoad = true;
    options.Theme = "Dark"; // "Dark" or "Light"
    
    // Optional: Object Filtering
    options.ExcludedSchemas = ["guest", "temp"]; 
    options.ExcludedObjects = ["Logs", "InternalTable"];
    
    // Optional: Authorization Filter for the UI pages
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
    
    // History Configuration
    options.AllowHistoryLog = true;
    options.HistoryTableName = "__SqlStudioQueryHistory";
    options.CreateTable = true; // Auto-provision history table
    
    // Set to null to return 401 Unauthorized instead of redirecting
    options.UnauthorizedRedirectUrl = null;
});
```

### End-to-End Encryption (E2EE)

#### What it does

E2EE is **off by default** (`options.Encryption.Enabled` defaults to `false`). When enabled, the browser encrypts query/execute requests, schema requests, history operations, and their responses before they pass through the SqlStudio proxy. The proxy forwards ciphertext without parsing or decrypting it. The configured backend is the cryptographic endpoint: it decrypts requests to execute SQL and encrypts the response for the browser.

E2EE requires HTTPS (and a secure browser context with Web Crypto). It does not replace application authorization, backend authentication, TLS, or careful protection of the page and its scripts. Health checks remain ordinary unencrypted GET requests. Authentication headers configured in `ApiConfig.AuthHeaders` are server-to-backend credentials, not part of the E2EE payload.

E2EE does **not** hide data from the backend: the backend must see SQL to execute it, and may see results and history. It does not encrypt database history at rest. The browser displays decrypted values, so XSS, compromised scripts/browser, or a compromised backend can expose them. If the SqlStudio proxy and backend share a process, server, or access to the backend private key, that deployment does not provide confidentiality from that host. This protocol version also does not provide forward secrecy: later compromise of the RSA private key may expose archived request envelopes.

In E2EE mode the browser does not send `document.cookie` or `localStorage` as history metadata. History query text is sent in encrypted `history.append` requests and is stored by the backend. Excluded schema/object lists are encrypted client hints; enforce actual data access restrictions in the backend, not in UI filters.

#### 1. Configure the package

Obtain the backend's **public RSA JWK** through a trusted deployment/configuration channel. Keep the matching private key only on the backend. Use an RSA key of at least 2048 bits; the public JWK must contain `kty` (`RSA`), base64url `n` and `e`, and be usable for `RSA-OAEP-256` encryption. Set a key ID that uniquely identifies that key. Never place private JWK members (such as `d`, `p`, or `q`) in package options, browser configuration, source control, or logs.

Add the following inside the `AddSqlStudio(options => { ... })` callback from the setup example above. Use the real public JWK from your backend; the placeholder below is not a usable key:

```csharp
options.Encryption.Enabled = true;
options.Encryption.BackendKeyId = "sql-api-key-2026-01";
options.Encryption.BackendPublicKeyJwk = """{"kty":"RSA","n":"<base64url-encoded-modulus>","e":"AQAB","alg":"RSA-OAEP-256","ext":true,"key_ops":["encrypt"]}""";

// Required when AllowHistoryLog is true.
options.ApiConfig.HistoryEndPoint = "api/sql/history";
```

The same public key and key ID must correspond to a private key accepted by the configured backend. E2EE startup validation requires the key ID, a valid public RSA JWK, and `QueryEndPoint`; it also requires `SchemaEndPoint` when schema loading is enabled and `HistoryEndPoint` when history is enabled. With E2EE on, `QueryEndPoint` is a unified encrypted endpoint for **all** SQL statements, including mutations; `ExecuteEndPoint` is not used. Schema and history requests use POST in this mode. The health endpoint remains GET.

For configuration-driven setup, put the public JWK and key ID in trusted application configuration and bind `SqlStudioOptions` with `builder.Services.AddSqlStudio(builder.Configuration)`. The public key itself is not secret, but its integrity is essential: an attacker who can replace the configured key or modify the page can undermine the trust relationship. Example `appsettings.json` structure (replace the JWK placeholder with the serialized public key):

```json
{
  "SqlStudio": {
    "AllowHistoryLog": true,
    "Encryption": {
      "Enabled": true,
      "BackendKeyId": "sql-api-key-2026-01",
      "BackendPublicKeyJwk": "<serialized public RSA JWK>"
    },
    "ApiConfig": {
      "QueryEndPoint": "api/sql/query",
      "SchemaEndPoint": "api/sql/schema",
      "HistoryEndPoint": "api/sql/history",
      "HealthEndpoint": "api/sql/health"
    }
  }
}
```

#### 2. Use the included TestApi for a local walkthrough

`tests/Mma.SqlStudio.TestApi` enables E2EE in its sample `Program.cs`. It creates a 3072-bit RSA key pair in memory, configures SqlStudio with that public key, and uses the matching private key in its SQL API controller. Its query, schema, and history endpoints require encrypted envelopes; its health endpoint is not encrypted. The test API persists history in its SQL database.

Run the TestApi over HTTPS and make sure the `SqlStudioClient` `BaseAddress` in the test API points to that same running HTTPS origin (the port must match the selected Visual Studio/IIS Express or `dotnet run` profile). The sample key is regenerated at every process start, so restart requires a page reload. The TestApi's sample SQL Server connection string/database must also be available for query/schema/history verification; update it for your local environment rather than using sample credentials in production.

After starting the app, open the SQL Studio page, load the schema, run `SELECT 1 AS E2EECheck;`, and open History. A successful query response, populated schema, and history entry exercise all three encrypted routes. Health status alone does not prove encryption because health is deliberately a plaintext GET.

#### 3. Backend protocol: E2EE v1

The backend must implement the following contract on the endpoints configured in `ApiConfig`. The SqlStudio proxy forwards each POST body and response body without inspecting their JSON contents. A compatible backend must never silently accept plaintext on an E2EE endpoint or fall back to plaintext after a cryptographic failure.

| Proxy route | Backend endpoint | Decrypted operation | Expected encrypted response |
| --- | --- | --- | --- |
| `POST /query` | `QueryEndPoint` | `query.execute` with `query` | Standard `QueryResult` |
| `POST /schema` | `SchemaEndPoint` | `schema.get` with `excludedSchemas` and `excludedObjects` | Schema array |
| `POST /history` | `HistoryEndPoint` | `history.append` with `query` and `executedAt`, or `history.list` | Append acknowledgment, or `{ "items": [...], "tableName": "..." }` |

Each request uses a new random AES-256-GCM key and a 12-byte nonce. The browser wraps the AES key with the configured RSA-OAEP/SHA-256 public key and encrypts UTF-8 JSON. AES-GCM ciphertext is the encrypted bytes followed by the 16-byte authentication tag, then Base64 encoded. The JSON request envelope is:

```json
{
  "version": 1,
  "algorithm": "RSA-OAEP-256+A256GCM",
  "keyId": "configured-key-id",
  "requestId": "random-guid",
  "wrappedKey": "base64 RSA-OAEP ciphertext",
  "iv": "base64 12-byte nonce",
  "ciphertext": "base64 AES-GCM ciphertext and tag"
}
```

The route-bound request additional authenticated data (UTF-8) is `Mma.SqlStudio.E2EE.v1|{keyId}|{requestId}|{route}|request`, where `route` is exactly `query`, `schema`, or `history`. The decrypted JSON includes the same `requestId`; operations are exactly `query.execute`, `schema.get`, `history.append`, or `history.list`. `schema.get` also carries the configured excluded names as hints. `history.append` carries the SQL text and an ISO-8601 `executedAt`; cookies and local storage are omitted.

The backend encrypts the normal operation response with the same per-request AES key, a new 12-byte nonce, and UTF-8 AAD `Mma.SqlStudio.E2EE.v1|{keyId}|{requestId}|{route}|response`. Return this envelope as JSON and echo `version`, `keyId`, and `requestId` exactly:

```json
{
  "version": 1,
  "keyId": "configured-key-id",
  "requestId": "same-request-guid",
  "iv": "base64 12-byte nonce",
  "ciphertext": "base64 AES-GCM ciphertext and tag"
}
```

A `QueryResult` response has `success`, `isQuery`, `message`, and `rows`; schema responses use the regular schema model; history-list responses have `items` and `tableName`. Encrypt application-level errors in the same response envelope. For malformed/unauthenticated requests, reject without returning plaintext error details or logging the envelope contents.

#### 4. Backend security and operations checklist

- Validate version, algorithm, key ID, request/response route, Base64 fields, IV/tag lengths, decrypted JSON shape, operation, and matching request ID. Reject invalid authentication tags and oversized inputs.
- Use an atomic replay cache for request IDs. In a multi-instance backend, share the cache across instances; retain old private keys only as long as needed for an intentional rotation window. The sample TestApi keeps request IDs in a process-local cache capped at 50,000 entries and rejects repeated IDs; it is for development/testing, not a production replay-cache design.
- Keep the private key in a secret manager/HSM or equivalent backend-only store. All replicas receiving traffic for a key ID need access to the matching private key. During rotation, publish a new key ID/public key and coordinate backend key availability; open browser pages may still hold the old public key, so reload pages after deployment.
- Do not log SQL, results, decrypted payloads, AES keys, private keys, or full envelopes. Avoid plaintext error bodies. Protect the page and script delivery with HTTPS, appropriate CSP, controlled dependencies, and XSS prevention.
- Keep backend authorization and database permissions least-privileged. E2EE does not authenticate a SQL user by itself, prevent an authorized browser from viewing decrypted results, or encrypt history at rest.

#### 5. Troubleshooting

- **HTTP 400 / invalid envelope:** reload the page after restarting the TestApi (its key changes at startup); verify the browser's key ID and public JWK correspond to the backend private key; verify the exact protocol version, route AAD, and operation name.
- **Replay rejection:** do not re-send a captured “Copy as PowerShell” request. It reuses the same `requestId` and ciphertext; trigger a new browser action to generate a fresh key, nonce, and request ID.
- **Operation/route mismatch:** rebuild and redeploy both the package's static JavaScript and backend together, then clear the browser cache. The TestApi logs sanitized E2EE rejection reasons without logging SQL or ciphertext.
- **Web Crypto unavailable:** use HTTPS and a supported browser. E2EE fails closed; it does not fall back to plaintext.
- **Schema/history empty:** check backend endpoint configuration and database connectivity. Excluded-name options are hints; enforce authorization at the backend.

The included TestApi demonstrates protocol interoperability, not production key custody or a separated trust boundary. Existing plaintext backends must be upgraded to this contract before E2EE is enabled.

### 3. Map Endpoints

Ensure static files and endpoints are mapped properly in your middleware pipeline:

```csharp
// Required to serve the embedded CSS and JS files from the RCL
app.UseStaticFiles(); 

app.MapRazorPages();

// Map the API endpoints required by SQL Studio (Proxies to your configured API)
app.MapSqlStudioEndpoints();
```

## 🔌 API Specification

Your backend API must implement the following endpoints. You can find the full **[OpenAPI Specification (YAML)](https://raw.githubusercontent.com/mma1979/Mma.SqlStudio.ApiClient/refs/heads/main/src/Mma.SqlStudio.ApiClient/openapi.yaml)** in the repository.

### Response Models

All query/execute endpoints should return a `QueryResult` object:

```json
{
  "success": true,
  "isQuery": true,
  "message": "Query executed successfully",
  "rows": [
    { "Id": 1, "Name": "Alice" },
    { "Id": 2, "Name": "Bob" }
  ]
}
```

### 1. Query Endpoint (`POST`)
Used for `SELECT` statements.
- **Request**: `{ "query": "string", "cookies": "string", "localStorage": "string" }`
- **Response**: `QueryResult` (rows populated)

### 2. Execute Endpoint (`POST`)
Used for `INSERT`, `UPDATE`, `DELETE`, etc.
- **Request**: `{ "query": "string", "cookies": "string", "localStorage": "string" }`
- **Response**: `QueryResult` (rows usually empty)

### 3. History Endpoint (`GET`)
Retrieves query history.
- **Response**: `{ "items": [HistoryItem], "tableName": "string" }`

### 3. Schema Endpoint (`GET`)
Returns the database structure.
- **Response**: Array of `SchemaNode`:
```json
[
  {
    "name": "dbo",
    "children": [
      { "name": "Tables", "objects": ["Users", "Orders"] },
      { "name": "Views", "objects": ["v_ActiveUsers"] }
    ]
  }
]
```

### 4. Health Endpoint (`GET`)
Simple check to verify backend connectivity.
- **Response**: Any `2xx` status code.

## 📸 Screenshots

**Dark Theme**
![dark theme](https://raw.githubusercontent.com/mma1979/Mma.SqlStudio.ApiClient/refs/heads/main/src/Mma.SqlStudio.ApiClient/dark.png)

**Light Theme**
![light theme](https://raw.githubusercontent.com/mma1979/Mma.SqlStudio.ApiClient/refs/heads/main/src/Mma.SqlStudio.ApiClient/light.png)

## 📝 License

This project is licensed under the MIT License. See the [LICENSE](https://raw.githubusercontent.com/mma1979/Mma.SqlStudio.ApiClient/main/LICENSE) file for details.

