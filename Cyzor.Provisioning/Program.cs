using Cyzor.Core.Configuration;
using Cyzor.Core.Domain.Entities;
using Cyzor.Core.Domain.Interfaces;
using Cyzor.Core.Domain.Services;
using Cyzor.Infrastructure.Services.Command;
using Cyzor.Infrastructure.Services.Fake;
using Cyzor.Infrastructure.Services.Real;
using Cyzor.Infrastructure.Services.Persistence;
using Cyzor.Provisioning.Application.Pipeline;
using Cyzor.Provisioning.DTOs;
using Cyzor.Provisioning.Queue;
using Cyzor.Provisioning.Services;
using Cyzor.Provisioning.Workers;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using System.Net;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<ProvisioningSettings>(
    builder.Configuration.GetSection("Provisioning"));

// Port Allocator
builder.Services.AddSingleton<IPortAllocator, PortAllocatorService>();

// Command Executor - Local (rodando no mesmo servidor)
builder.Services.AddSingleton<ICommandExecutor, LocalCommandExecutor>();
builder.Services.AddSingleton<ITenantDeploymentService, TenantDeploymentService>();

// Real Provisioners
builder.Services.AddSingleton<IResourceAllocator, RealResourceAllocator>();
builder.Services.AddSingleton<IEnvironmentProvisioner, LocalEnvironmentProvisioner>();
builder.Services.AddSingleton<IBlueprintInstaller, RealBlueprintInstaller>();
builder.Services.AddSingleton<IDomainConfigurator, RealDomainConfigurator>();
builder.Services.AddSingleton<Cyzor.Core.Domain.Interfaces.ITenantRepository, SqliteTenantRepository>();

// Queue & Pipeline
builder.Services.AddSingleton<IProvisioningQueue, InMemoryProvisioningQueue>();
builder.Services.AddSingleton<ProvisionInstancePipeline>();

// Health Check & Rollback
builder.Services.AddSingleton<IHealthCheckService, HealthCheckService>();
builder.Services.AddSingleton<IRollbackService, RollbackService>();

// Alerts
builder.Services.AddSingleton<IAlertService, AlertService>();

// Swagger
builder.Services.AddSingleton<ISwaggerService, SwaggerService>();

// Nginx Configuration
builder.Services.AddSingleton<INginxConfigService, NginxConfigService>();

// Let's Encrypt
builder.Services.AddSingleton<ILetsEncryptService, LetsEncryptService>();

// Status Page
builder.Services.AddSingleton<IStatusPageService, StatusPageService>();

// Prometheus Metrics
builder.Services.AddSingleton<IMetricsService, PrometheusMetricsService>();

// API Authentication & Validation
var apiKey = Environment.GetEnvironmentVariable("PROVISIONING_API_KEY") ?? "default-insecure-key-change-in-production";
builder.Services.AddSingleton<IApiAuthenticationService>(new ApiAuthenticationService(apiKey));
builder.Services.AddSingleton<IProvisioningRequestValidator, ProvisioningRequestValidator>();

// Workers
builder.Services.AddHostedService<ProvisioningQueueWorker>();
builder.Services.AddHostedService<SeedWorker>();
builder.Services.AddHostedService<SqliteBackupService>();

builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});

var host = builder.Build();

// HTTP API using HttpListener
var queue = host.Services.GetRequiredService<IProvisioningQueue>();
var authService = host.Services.GetRequiredService<IApiAuthenticationService>();
var validator = host.Services.GetRequiredService<IProvisioningRequestValidator>();
var tenantRepository = host.Services.GetRequiredService<Cyzor.Core.Domain.Interfaces.ITenantRepository>();
var healthCheckService = host.Services.GetRequiredService<IHealthCheckService>();
var letsEncryptService = host.Services.GetRequiredService<ILetsEncryptService>();
var metricsService = host.Services.GetRequiredService<IMetricsService>();
var swaggerService = host.Services.GetRequiredService<ISwaggerService>();

_ = Task.Run(async () =>
{
    var listener = new HttpListener();
    try
    {
        listener.Prefixes.Add("http://+:5000/");
        listener.Start();
        Console.WriteLine("[API] HTTP Provisioning API listening on :5000");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[API] Failed to start listener: {ex.Message}");
        return;
    }

    while (true)
    {
        try
        {
            var context = await listener.GetContextAsync();
            _ = Task.Run(async () =>
            {
                try
                {
                    var request = context.Request;
                    var response = context.Response;

                    if (request.HttpMethod == "POST" && request.Url?.LocalPath == "/api/provision")
                    {
                        // Authenticate
                        var apiKey = request.Headers["X-API-Key"];
                        if (!authService.ValidateApiKey(apiKey))
                        {
                            Console.WriteLine($"[API] Unauthorized request (missing/invalid X-API-Key)");
                            response.StatusCode = 401;
                            var errMsg = "Unauthorized: Invalid or missing X-API-Key header";
                            var errBuffer = System.Text.Encoding.UTF8.GetBytes(errMsg);
                            await response.OutputStream.WriteAsync(errBuffer, 0, errBuffer.Length);
                        }
                        else
                        {
                            using var reader = new StreamReader(request.InputStream);
                            var body = await reader.ReadToEndAsync();
                            Console.WriteLine($"[API] Authenticated request body: {body}");
                            
                            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                            var reqData = JsonSerializer.Deserialize<ProvisioningRequest>(body, options);

                            // Validate input
                            var (isValid, errorMessage) = validator.Validate(reqData);
                            if (!isValid)
                            {
                                Console.WriteLine($"[API] Validation failed: {errorMessage}");
                                response.StatusCode = 400;
                                var errBuffer = System.Text.Encoding.UTF8.GetBytes(errorMessage ?? "Validation failed");
                                await response.OutputStream.WriteAsync(errBuffer, 0, errBuffer.Length);
                            }
                            else
                            {
                                var instance = new Instance(Guid.NewGuid(), reqData!.Domain);
                                await queue.EnqueueAsync(instance);
                                Console.WriteLine($"[API] Enqueued {reqData.Domain} (ID: {instance.Id})");

                                var respData = new ProvisioningResponse
                                {
                                    InstanceId = instance.Id,
                                    Domain = reqData.Domain,
                                    Status = "Queued"
                                };

                                var respJson = JsonSerializer.Serialize(respData);
                                var buffer = System.Text.Encoding.UTF8.GetBytes(respJson);
                                response.ContentType = "application/json";
                                response.ContentLength64 = buffer.Length;
                                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                            }
                        }
                    }
                    else if (request.HttpMethod == "GET" && request.Url?.LocalPath == "/health")
                    {
                        var buffer = System.Text.Encoding.UTF8.GetBytes("OK");
                        response.ContentType = "text/plain";
                        response.ContentLength64 = buffer.Length;
                        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    }
                    else if (request.HttpMethod == "GET" && request.Url?.LocalPath == "/swagger")
                    {
                        // Swagger UI
                        var swaggerHtml = swaggerService.GetSwaggerUI();
                        var buffer = System.Text.Encoding.UTF8.GetBytes(swaggerHtml);
                        response.ContentType = "text/html; charset=utf-8";
                        response.ContentLength64 = buffer.Length;
                        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    }
                    else if (request.HttpMethod == "GET" && request.Url?.LocalPath == "/openapi.json")
                    {
                        // OpenAPI specification
                        var openApiJson = @"{
  ""openapi"": ""3.0.0"",
  ""info"": {
    ""title"": ""Cyzor Provisioning API"",
    ""version"": ""1.0.0"",
    ""description"": ""API para provisionamento automático de tenants""
  },
  ""servers"": [{""url"": ""http://72.60.247.117:5000""}],
  ""components"": {
    ""securitySchemes"": {
      ""ApiKeyAuth"": {
        ""type"": ""apiKey"",
        ""in"": ""header"",
        ""name"": ""X-API-Key""
      }
    }
  },
  ""security"": [{ ""ApiKeyAuth"": [] }],
  ""paths"": {
    ""/api/provision"": {
      ""post"": {
        ""summary"": ""Provisionar novo tenant"",
        ""requestBody"": {
          ""required"": true,
          ""content"": {
            ""application/json"": {
              ""schema"": {
                ""type"": ""object"",
                ""properties"": {
                    ""domain"": {""type"": ""string"", ""example"": ""back.cyzor.com.br"", ""default"": ""back.cyzor.com.br""},
                    ""appType"": {""type"": ""string"", ""enum"": [""node"", ""python"", ""static""], ""default"": ""node"", ""example"": ""node""}
                }
              }
            }
          }
        },
        ""responses"": {
          ""200"": {""description"": ""Tenant enfileirado""},
          ""400"": {""description"": ""Validação falhou""},
          ""401"": {""description"": ""Não autorizado""}
        }
      }
    },
    ""/api/status/{instanceId}"": {
      ""get"": {
        ""summary"": ""Consultar status de um tenant"",
        ""parameters"": [
          {""name"": ""instanceId"", ""in"": ""path"", ""required"": true, ""schema"": {""type"": ""string""}}
        ],
        ""responses"": {
          ""200"": {""description"": ""Status retornado""},
          ""404"": {""description"": ""Tenant não encontrado""}
        }
      }
    },
    ""/api/ssl/{instanceId}"": {
      ""post"": {
        ""summary"": ""Gerar certificado SSL"",
        ""parameters"": [
          {""name"": ""instanceId"", ""in"": ""path"", ""required"": true, ""schema"": {""type"": ""string""}}
        ],
        ""responses"": {
          ""200"": {""description"": ""SSL iniciado""},
          ""404"": {""description"": ""Tenant não encontrado""}
        }
      }
    },
    ""/health"": {
      ""get"": {
        ""summary"": ""Health check"",
        ""responses"": {""200"": {""description"": ""OK""}}
      }
    },
    ""/metrics"": {
      ""get"": {
        ""summary"": ""Métricas Prometheus"",
        ""responses"": {""200"": {""description"": ""Métricas""}}
      }
    }
  }
}";
                        var buffer = System.Text.Encoding.UTF8.GetBytes(openApiJson);
                        response.ContentType = "application/json";
                        response.ContentLength64 = buffer.Length;
                        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    }
                    else if (request.HttpMethod == "GET" && request.Url?.LocalPath == "/metrics")
                    {
                        // Prometheus metrics endpoint (no auth required for monitoring)
                        var metricsData = metricsService.GetMetricsSnapshot();
                        var buffer = System.Text.Encoding.UTF8.GetBytes(metricsData);
                        response.ContentType = "text/plain; version=0.0.4";
                        response.ContentLength64 = buffer.Length;
                        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    }
                    else if (request.HttpMethod == "GET" && request.Url?.LocalPath?.StartsWith("/api/status/") == true)
                    {
                        // Parse: /api/status/{instanceId}
                        var pathParts = request.Url.LocalPath.Split('/');
                        if (pathParts.Length >= 4 && Guid.TryParse(pathParts[3], out var instanceId))
                        {
                            // Authenticate
                            var apiKey = request.Headers["X-API-Key"];
                            if (!authService.ValidateApiKey(apiKey))
                            {
                                response.StatusCode = 401;
                                var errBuffer = System.Text.Encoding.UTF8.GetBytes("Unauthorized");
                                await response.OutputStream.WriteAsync(errBuffer, 0, errBuffer.Length);
                            }
                            else
                            {
                                // Get tenant status from repo
                                var tenant = await tenantRepository.GetByIdAsync(instanceId);
                                if (tenant == null)
                                {
                                    response.StatusCode = 404;
                                    var errBuffer = System.Text.Encoding.UTF8.GetBytes("Tenant not found");
                                    await response.OutputStream.WriteAsync(errBuffer, 0, errBuffer.Length);
                                }
                                else
                                {
                                    // Check health if tenant is in Running state
                                    bool isHealthy = false;
                                    string healthStatus = "Unknown";
                                    if (tenant.Port.HasValue && tenant.State == "Running")
                                    {
                                        isHealthy = await healthCheckService.CheckTenantHealthAsync(instanceId, tenant.Port.Value, maxRetries: 1);
                                        healthStatus = isHealthy ? "Healthy" : "Unhealthy";
                                    }
                                    else if (tenant.Port.HasValue)
                                    {
                                        healthStatus = tenant.State;
                                    }

                                    var statusResponse = new TenantStatusResponse
                                    {
                                        InstanceId = tenant.Id,
                                        Domain = tenant.Domain,
                                        State = tenant.State,
                                        Port = tenant.Port,
                                        CreatedAt = tenant.CreatedAt,
                                        UpdatedAt = tenant.UpdatedAt,
                                        IsHealthy = isHealthy,
                                        HealthStatus = healthStatus
                                    };

                                    var statusJson = JsonSerializer.Serialize(statusResponse);
                                    var buffer = System.Text.Encoding.UTF8.GetBytes(statusJson);
                                    response.ContentType = "application/json";
                                    response.ContentLength64 = buffer.Length;
                                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                                }
                            }
                        }
                        else
                        {
                            response.StatusCode = 400;
                            var errBuffer = System.Text.Encoding.UTF8.GetBytes("Invalid instanceId format");
                            await response.OutputStream.WriteAsync(errBuffer, 0, errBuffer.Length);
                        }
                    }
                    else if (request.HttpMethod == "POST" && request.Url?.LocalPath?.StartsWith("/api/ssl/") == true)
                    {
                        // POST /api/ssl/{instanceId} - Trigger Let's Encrypt
                        var pathParts = request.Url.LocalPath.Split('/');
                        if (pathParts.Length >= 4 && Guid.TryParse(pathParts[3], out var instanceId))
                        {
                            // Authenticate
                            var apiKey = request.Headers["X-API-Key"];
                            if (!authService.ValidateApiKey(apiKey))
                            {
                                response.StatusCode = 401;
                                var errBuffer = System.Text.Encoding.UTF8.GetBytes("Unauthorized");
                                await response.OutputStream.WriteAsync(errBuffer, 0, errBuffer.Length);
                            }
                            else
                            {
                                // Get tenant
                                var tenant = await tenantRepository.GetByIdAsync(instanceId);
                                if (tenant == null)
                                {
                                    response.StatusCode = 404;
                                    var errBuffer = System.Text.Encoding.UTF8.GetBytes("Tenant not found");
                                    await response.OutputStream.WriteAsync(errBuffer, 0, errBuffer.Length);
                                }
                                else
                                {
                                    // Start Let's Encrypt async (don't wait)
                                    _ = Task.Run(async () =>
                                    {
                                        Console.WriteLine($"[SSL] Starting Let's Encrypt for {tenant.Domain} (requested via webhook)");
                                        var result = await letsEncryptService.GenerateCertificateAsync(tenant.Domain);
                                        if (result)
                                        {
                                            Console.WriteLine($"[SSL] ✓ Certificate generated for {tenant.Domain}");
                                        }
                                        else
                                        {
                                            Console.WriteLine($"[SSL] ✗ Failed to generate certificate for {tenant.Domain}");
                                        }
                                    });

                                    // Return immediate response
                                    var responseMsg = $"{{\"Status\":\"Processing\",\"Message\":\"Let's Encrypt certificate generation started for {tenant.Domain}\"}}";
                                    var buffer = System.Text.Encoding.UTF8.GetBytes(responseMsg);
                                    response.ContentType = "application/json";
                                    response.ContentLength64 = buffer.Length;
                                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                                }
                            }
                        }
                        else
                        {
                            response.StatusCode = 400;
                            var errBuffer = System.Text.Encoding.UTF8.GetBytes("Invalid instanceId format");
                            await response.OutputStream.WriteAsync(errBuffer, 0, errBuffer.Length);
                        }
                    }
                    else
                    {
                        response.StatusCode = 404;
                    }

                    response.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[API] Error: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] Listener error: {ex.Message}");
        }
    }
});

host.Run();
