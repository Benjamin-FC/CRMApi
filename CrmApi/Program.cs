using CrmApi.Swagger;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using System.Reflection;
using CrmApi;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .CreateLogger();

try
{
    Log.Information("Starting FrankCrum CRM API");

// Add Serilog
builder.Host.UseSerilog();

// Add HttpClient for proxying requests
var azureApiBaseUrl = builder.Configuration.GetValue<string>("AzureApiBaseUrl");
if (!string.IsNullOrWhiteSpace(azureApiBaseUrl))
{
    builder.Services.AddHttpClient("CrmApiProxy", client =>
    {
        client.BaseAddress = new Uri(azureApiBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    });
}

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "FrankCrum CRM API", 
        Version = "v1",
        Description = "API for FrankCrum CRM System",
        Contact = new OpenApiContact
        {
            Name = "FrankCrum Support",
            Email = "support@frankcrum.com"
        }
    });
    
    // Include XML comments if available
    try 
    {
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            c.IncludeXmlComments(xmlPath);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Could not load XML comments: {ex.Message}");
    }
    
    // Add document filter
    c.DocumentFilter<SwaggerDocumentFilter>();
    
    // Add operation filter
    c.OperationFilter<OperationFilter>();
    
    // Enable annotations
    c.EnableAnnotations();
});

// Add controllers
builder.Services.AddControllers();

var app = builder.Build();

// Read configuration flag for mock mode
var useMockData = app.Configuration.GetValue<bool>("UseMockData", true);
Log.Information("API Mode: {Mode}", useMockData ? "Mock Data" : "Proxy to Azure");

// Configure for sub-application deployment (e.g., /CRMApi)
var basePath = "/CRMApi";
app.UsePathBase(basePath);
app.UseRouting();

// Enable middleware to serve generated Swagger as a JSON endpoint
app.UseSwagger(c =>
{
    c.RouteTemplate = "swagger/{documentName}/swagger.json";
    c.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
    {
        swaggerDoc.Servers = new List<OpenApiServer> 
        { 
            new OpenApiServer 
            { 
                Url = $"{httpReq.Scheme}://{httpReq.Host.Value}{basePath}",
                Description = "Production Server"
            }
        };
    });
});

// Enable middleware to serve swagger-ui
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint($"{basePath}/swagger/v1/swagger.json", "FrankCrum CRM API V1");
    c.RoutePrefix = "swagger";
    c.DisplayRequestDuration();
    c.EnableDeepLinking();
    c.DisplayOperationId();
    c.EnableFilter();
    c.EnableValidator();
    c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
});

app.UseDeveloperExceptionPage();
app.UseHttpsRedirection();
app.UseStaticFiles();

// Add a welcome message at the root
app.MapGet("/", () => $"FrankCrum CRM API is running in {(useMockData ? "Mock" : "Proxy")} mode. Go to /CRMApi/swagger to view the Swagger UI.")
    .Produces<string>(StatusCodes.Status200OK)
    .WithTags("Home")
    .WithName("GetRoot")
    .WithOpenApi(operation => new(operation)
    {
        Summary = "API Root",
        Description = "Returns a welcome message and API information"
    });

// Use either mock middleware or proxy middleware based on configuration
if (useMockData)
{
    Log.Information("Using Mock Middleware for API responses");
    
    var mockToken = app.Configuration.GetValue<string>("MockBearerToken", "123");
    Log.Information("Mock mode authentication token: {Token}", mockToken);
    
    // Add authentication middleware for mock mode
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path.Value ?? string.Empty;
        
        // Skip auth for swagger, static files, and system endpoints
        if (path.StartsWith("/swagger") || path == "/favicon.ico" || path.StartsWith("/CRMApi/swagger") ||
            path == "/" || path == "/CRMApi" || path == "/CRMApi/" || path == "/health" || path == "/api/version")
        {
            await next(context);
            return;
        }
        
        // Check for Authorization header
        if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
            {
                error = "Unauthorized",
                message = "Authorization header is required",
                timestamp = DateTime.UtcNow
            }));
            return;
        }
        
        var token = authHeader.ToString().Replace("Bearer ", "").Trim();
        if (token != mockToken)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
            {
                error = "Unauthorized",
                message = "Invalid authentication token",
                timestamp = DateTime.UtcNow
            }));
            return;
        }
        
        await next(context);
    });
    
    app.UseMiddleware<MockMiddleware>();
}
else
{
    Log.Information("Using Proxy Middleware - forwarding to Azure endpoint");
    
    // Try to get token from environment variable first, then fall back to configuration
    var azureToken = Environment.GetEnvironmentVariable("AZURE_BEARER_TOKEN") 
                     ?? app.Configuration.GetValue<string>("AzureBearerToken", "");
    
    if (string.IsNullOrEmpty(azureToken))
    {
        Log.Warning("AzureBearerToken is not configured. Set AZURE_BEARER_TOKEN environment variable or AzureBearerToken in appsettings. Requests to Azure may fail.");
    }
    else
    {
        Log.Information("Azure authentication token configured from {Source}", 
            Environment.GetEnvironmentVariable("AZURE_BEARER_TOKEN") != null ? "environment variable" : "configuration");
    }
    
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path.Value ?? string.Empty;
        
        // Check if this is a Swagger UI or static file request
        if (path.StartsWith("/swagger") || path == "/favicon.ico" || path.StartsWith("/CRMApi/swagger") ||
            path == "/" || path == "/CRMApi" || path == "/CRMApi/" || path == "/health" || path == "/api/version")
        {
            await next(context);
            return;
        }

        try
        {
            var httpClientFactory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("CrmApiProxy");
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

            // Remove base path if present
            var requestPath = path.StartsWith("/CRMApi") ? path.Substring("/CRMApi".Length) : path;
            var queryString = context.Request.QueryString.ToString();
            var targetUrl = $"{requestPath}{queryString}";

            logger.LogInformation("Proxying request: {Method} {TargetUrl}", context.Request.Method, targetUrl);

            // Create the proxy request
            var proxyRequest = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUrl);

            // Add Azure Bearer token if configured
            if (!string.IsNullOrEmpty(azureToken))
            {
                proxyRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", azureToken);
            }

            // Copy headers
            foreach (var header in context.Request.Headers)
            {
                if (!header.Key.StartsWith(":") && header.Key != "Host" && header.Key != "Authorization")
                {
                    proxyRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }

            // Copy body for POST/PUT/PATCH
            if (context.Request.Method == "POST" || context.Request.Method == "PUT" || context.Request.Method == "PATCH")
            {
                var requestContent = new StreamContent(context.Request.Body);
                if (context.Request.ContentType != null)
                {
                    requestContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(context.Request.ContentType);
                }
                proxyRequest.Content = requestContent;
            }

            // Send the request
            var response = await httpClient.SendAsync(proxyRequest);

            // Copy response status code
            context.Response.StatusCode = (int)response.StatusCode;

            // Copy response headers
            foreach (var header in response.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }
            foreach (var header in response.Content.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            // Copy response body
            await response.Content.CopyToAsync(context.Response.Body);

            logger.LogInformation("Proxy response: {StatusCode} for {Method} {TargetUrl}", 
                context.Response.StatusCode, context.Request.Method, targetUrl);
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Error proxying request to Azure endpoint");
            
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
            {
                error = "Failed to proxy request to Azure endpoint",
                message = ex.Message,
                timestamp = DateTime.UtcNow
            }));
        }
    });
}

// Add a health check endpoint
app.MapGet("/health", () => Results.Ok(new { 
        status = "Healthy", 
        timestamp = DateTime.UtcNow,
        version = "1.0.0",
        mode = useMockData ? "Mock" : "Proxy"
    }))
    .Produces(StatusCodes.Status200OK)
    .WithTags("Health")
    .WithName("GetHealth")
    .WithOpenApi(operation => new(operation)
    {
        Summary = "Health Check",
        Description = "Performs a health check of the API"
    });

// Add a sample API endpoint that will be visible in Swagger
app.MapGet("/api/version", () => new 
    { 
        version = "1.0.0",
        name = "FrankCrum CRM API",
        status = "Running",
        environment = app.Environment.EnvironmentName,
        mode = useMockData ? "Mock Data" : "Proxy to Azure"
    })
    .Produces(StatusCodes.Status200OK)
    .WithTags("System")
    .WithName("GetVersion")
    .WithOpenApi(operation => new(operation)
    {
        Summary = "API Version",
        Description = "Returns the current API version information"
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
