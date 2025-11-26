using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CrmApi
{
    public class MockMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly OpenApiDocument _openApiDocument;

        public MockMiddleware(RequestDelegate next)
        {
            _next = next;
            try 
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "swagger.json");
                Console.WriteLine($"Loading swagger from: {filePath}");
                if (!File.Exists(filePath))
                {
                    Console.WriteLine("Swagger file not found!");
                }
                using var stream = File.OpenRead(filePath);
                _openApiDocument = new OpenApiStreamReader().Read(stream, out var diagnostic);
                
                if (diagnostic.Errors.Any())
                {
                    Console.WriteLine("Swagger parse errors:");
                    foreach(var error in diagnostic.Errors)
                    {
                        Console.WriteLine(error.Message);

                if (matchingPath != null)
                {
                    var pathItem = _openApiDocument.Paths[matchingPath];
                    var operationType = Enum.Parse<OperationType>(method, true);
                    
                    if (pathItem.Operations.TryGetValue(operationType, out var operation))
                    {
                        // Try to find 200 OK response
                        if (operation.Responses.TryGetValue("200", out var response))
                        {
                            if (response.Content.TryGetValue("application/json", out var mediaType) || 
                                response.Content.TryGetValue("text/json", out mediaType) ||
                                response.Content.TryGetValue("text/plain", out mediaType)) // Fallback
                            {
                                var schema = mediaType.Schema;
                                var dummyData = GenerateDummyData(schema);
                                
                                context.Response.ContentType = "application/json";
                                context.Response.StatusCode = 200;
                                
                                if (dummyData != null)
                                {
                                    await context.Response.WriteAsync(dummyData.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                                }
                                else
                                {
                                    await context.Response.WriteAsync("{}");
                                }
                                return;
                            }
                        }
                    }
                }

                await _next(context);
            }
            catch (Exception ex)
            {
                        
                        // Email addresses
                        if (lowerName.Contains("email"))
                            return "john.doe@example.com";
                        
                        // Phone numbers
                        if (lowerName.Contains("phone") || lowerName.Contains("tel"))
                            return "(555) 123-4567";
                        
                        // Addresses
                        if (lowerName.Contains("address"))
                            return "123 Main Street";
                        if (lowerName.Contains("city"))
                            return "New York";
                        if (lowerName.Contains("state"))
                            return "NY";
                        if (lowerName.Contains("zip") || lowerName.Contains("postal"))
                            return "10001";
                        if (lowerName.Contains("country"))
                            return "USA";
                    }
                    
                    return "Sample Text";
                case "integer":
                    if (schema.Format == "int64") return 1234567890L;
                    return Random.Shared.Next(1, 1000);
                case "number":
                    if (schema.Format == "float") return 123.45f;
                    return Math.Round(Random.Shared.NextDouble() * 1000, 2);
                case "boolean":
                    return Random.Shared.Next(2) == 1;
                case "array":
                    var array = new JsonArray();
                    // Generate 1-3 items
                    var itemCount = Random.Shared.Next(1, 4);
                    for (int i = 0; i < itemCount; i++)
                    {
                        array.Add(GenerateDummyData(schema.Items, depth + 1, visited, null));
                    }
                    return array;
                case "object":
                    // Object without properties
                    return new JsonObject();
                default:
                    return null;
            }
        }
    }

    public static class MockMiddlewareExtensions
    {
        public static IApplicationBuilder UseMockApi(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<MockMiddleware>();
        }
    }
}
