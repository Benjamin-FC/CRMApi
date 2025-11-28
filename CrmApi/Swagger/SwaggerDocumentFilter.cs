using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.OpenApi.Readers;

namespace CrmApi.Swagger
{
    public class SwaggerDocumentFilter : IDocumentFilter
    {
        private readonly IWebHostEnvironment _env;

        public SwaggerDocumentFilter(IWebHostEnvironment env)
        {
            _env = env ?? throw new ArgumentNullException(nameof(env));
        }

        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            try
            {
                var swaggerPath = Path.Combine(_env.ContentRootPath, "swagger.json");
                if (!File.Exists(swaggerPath))
                {
                    Console.WriteLine($"Swagger file not found at: {swaggerPath}");
                    return;
                }

                Console.WriteLine($"Loading swagger from: {swaggerPath}");
                
                using var stream = File.OpenRead(swaggerPath);
                var reader = new OpenApiStreamReader();
                var readResult = reader.Read(stream, out var diagnostic);
                
                if (diagnostic.Errors.Count > 0)
                {
                    Console.WriteLine($"Swagger parsing errors: {string.Join(", ", diagnostic.Errors.Select(e => e.Message))}");
                    return;
                }

                var externalDoc = readResult;
                Console.WriteLine("Swagger loaded successfully.");

                if (externalDoc?.Paths != null)
                {
                    foreach (var path in externalDoc.Paths)
                    {
                        if (!swaggerDoc.Paths.ContainsKey(path.Key))
                        {
                            swaggerDoc.Paths.Add(path.Key, path.Value);
                        }
                        else
                        {
                            // Update existing path with any new operations
                            foreach (var operation in path.Value.Operations)
                            {
                                if (!swaggerDoc.Paths[path.Key].Operations.ContainsKey(operation.Key))
                                {
                                    swaggerDoc.Paths[path.Key].Operations.Add(operation.Key, operation.Value);
                                }
                            }
                        }
                    }
                }

                // Merge components (schemas, etc.) if they exist
                if (externalDoc?.Components?.Schemas != null)
                {
                    swaggerDoc.Components ??= new OpenApiComponents();
                    
                    foreach (var schema in externalDoc.Components.Schemas)
                    {
                        if (!swaggerDoc.Components.Schemas.ContainsKey(schema.Key))
                        {
                            swaggerDoc.Components.Schemas.Add(schema.Key, schema.Value);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SwaggerDocumentFilter: {ex}");
                throw; // Re-throw to ensure the application doesn't start with a broken Swagger configuration
            }
        }
    }
}
