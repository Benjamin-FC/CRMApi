using CrmApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure for sub-application deployment (e.g., /CRMApi)
app.UsePathBase("/CRMApi");

// Serve the existing swagger.json file
app.MapGet("/swagger/v1/swagger.json", async context =>
{
    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "swagger.json");
    if (File.Exists(filePath))
    {
        context.Response.ContentType = "application/json";
        await context.Response.SendFileAsync(filePath);
    }
    else
    {
        context.Response.StatusCode = 404;
    }
});

// Enable Swagger UI
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/CRMApi/swagger/v1/swagger.json", "FrankCrum CRM API V1");
    c.RoutePrefix = "swagger";
});

// app.UseCors("AllowReactApp");

app.UseMockApi();

app.Run();
