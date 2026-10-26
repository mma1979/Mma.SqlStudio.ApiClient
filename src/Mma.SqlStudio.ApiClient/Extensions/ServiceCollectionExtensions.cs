using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Mma.SqlStudio.ApiClient.Models;
using Mma.SqlStudio.ApiClient.Services;
using System;
using System.Text.Json;

namespace Mma.SqlStudio.ApiClient.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSqlStudio(this IServiceCollection services, Action<SqlStudioOptions> configureOptions)
        {
            if (configureOptions == null) throw new ArgumentNullException(nameof(configureOptions));

            var options = new SqlStudioOptions();
            configureOptions(options);
            services.Configure(configureOptions);
            AddEncryptionValidation(services);

            services.AddHttpClient("SqlStudioClient");

            services.AddOptions<RazorPagesOptions>()
                .Configure<IOptions<SqlStudioOptions>>((razorOptions, sqlStudioOptions) =>
                {
                    razorOptions.Conventions.Add(new SqlStudioPageRouteModelConvention(sqlStudioOptions.Value.Route));
                    
                    if (sqlStudioOptions.Value.AuthFilter is not null)
                    {
                        razorOptions.Conventions.AddPageApplicationModelConvention(
                            "/SqlStudio",
                            model => model.Filters.Add(
                                new Filters.SqlStudioAuthPageFilter(sqlStudioOptions.Value.AuthFilter, sqlStudioOptions.Value.UnauthorizedRedirectUrl)
                            )
                        );
                    }
                });

            services.AddScoped<SchemaService>();
            services.AddScoped<EncryptedApiProxy>();

            return services;
        }

        private static void AddEncryptionValidation(IServiceCollection services)
        {
            services.AddOptions<SqlStudioOptions>()
                .Validate(options =>
                {
                    if (!options.Encryption.Enabled) return true;
                    if (string.IsNullOrWhiteSpace(options.Encryption.BackendKeyId) ||
                        string.IsNullOrWhiteSpace(options.Encryption.BackendPublicKeyJwk) ||
                        string.IsNullOrWhiteSpace(options.ApiConfig.QueryEndPoint) ||
                        (options.EnableSchemaLoad && string.IsNullOrWhiteSpace(options.ApiConfig.SchemaEndPoint)) ||
                        (options.AllowHistoryLog && string.IsNullOrWhiteSpace(options.ApiConfig.HistoryEndPoint))) return false;

                    try
                    {
                        using var jwk = JsonDocument.Parse(options.Encryption.BackendPublicKeyJwk);
                        var root = jwk.RootElement;
                        if (!root.TryGetProperty("kty", out var kty) || kty.GetString() != "RSA" ||
                            !root.TryGetProperty("n", out var modulus) || string.IsNullOrWhiteSpace(modulus.GetString()) ||
                            !root.TryGetProperty("e", out var exponent) || string.IsNullOrWhiteSpace(exponent.GetString())) return false;

                        var modulusBytes = Convert.FromBase64String(modulus.GetString()!.Replace('-', '+').Replace('_', '/') +
                            new string('=', (4 - modulus.GetString()!.Length % 4) % 4));
                        return modulusBytes.Length >= 256;
                    }
                    catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
                    {
                        return false;
                    }
                }, "E2EE requires a valid RSA public JWK and a non-empty backend key identifier.")
                .ValidateOnStart();
        }

        public static IServiceCollection AddSqlStudio(this IServiceCollection services, IConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            services.Configure<SqlStudioOptions>(configuration.GetSection("SqlStudio"));
            AddEncryptionValidation(services);
            services.AddHttpClient("SqlStudioClient");

            services.AddOptions<RazorPagesOptions>()
                .Configure<IOptions<SqlStudioOptions>>((options, sqlStudioOptions) =>
                {
                    options.Conventions.Add(new SqlStudioPageRouteModelConvention(sqlStudioOptions.Value.Route));

                    if (sqlStudioOptions.Value.AuthFilter is not null)
                    {
                        options.Conventions.AddPageApplicationModelConvention(
                            "/SqlStudio",
                            model => model.Filters.Add(
                                new Filters.SqlStudioAuthPageFilter(sqlStudioOptions.Value.AuthFilter, sqlStudioOptions.Value.UnauthorizedRedirectUrl)
                            )
                        );
                    }
                });

            services.AddScoped<SchemaService>();
            services.AddScoped<EncryptedApiProxy>();

            return services;
        }
    }
}
