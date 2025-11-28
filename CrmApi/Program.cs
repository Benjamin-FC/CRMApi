using CrmApi.Swagger;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using System.Reflection;
using CrmApi;

var builder = WebApplication.CreateBuilder(args);

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
app.MapGet("/", () => "FrankCrum CRM API is running. Go to /CRMApi/swagger to view the Swagger UI.")
    .Produces<string>(StatusCodes.Status200OK)
    .WithTags("Home")
    .WithName("GetRoot")
    .WithOpenApi(operation => new(operation)
    {
        Summary = "API Root",
        Description = "Returns a welcome message and API information"
    });

// Add mock API middleware
app.UseMiddleware<MockMiddleware>();

// Add a health check endpoint
app.MapGet("/health", () => Results.Ok(new { 
        status = "Healthy", 
        timestamp = DateTime.UtcNow,
        version = "1.0.0"
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
        environment = app.Environment.EnvironmentName
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
