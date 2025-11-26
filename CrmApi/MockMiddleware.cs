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
                    }
                }
                Console.WriteLine("Swagger loaded successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading swagger: {ex}");
            }
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                var path = context.Request.Path.Value;
                var method = context.Request.Method.ToUpper();

                // Find matching path
                var matchingPath = _openApiDocument.Paths.Keys.FirstOrDefault(p => IsPathMatch(p, path));

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
                Console.WriteLine($"Error processing request: {ex}");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync($"Internal Server Error: {ex.Message}");
            }
        }

        private bool IsPathMatch(string template, string actual)
        {
            // Convert swagger template /api/v1/ClientData/{id} to regex
            var regexPattern = "^" + Regex.Replace(template, @"\{[^}]+\}", "[^/]+") + "$";
            return Regex.IsMatch(actual, regexPattern, RegexOptions.IgnoreCase);
        }

        private JsonNode? GenerateDummyData(OpenApiSchema? schema, int depth = 0, HashSet<string>? visited = null, string? propertyName = null)
        {
            if (depth > 20) return null; // Increased depth limit
            if (schema == null) return null;

            visited ??= new HashSet<string>();

            // Check for properties FIRST (even if there's a reference, if it's resolved it will have properties)
            if (schema.Properties != null && schema.Properties.Count > 0)
            {
                var obj = new JsonObject();
                foreach (var prop in schema.Properties)
                {
                    obj.Add(prop.Key, GenerateDummyData(prop.Value, depth + 1, visited, prop.Key));
                }
                return obj;
            }

            // Handle $ref only if no properties
            if (schema.Reference != null)
            {
                var refId = schema.Reference.Id;
                
                // Avoid circular references
                if (visited.Contains(refId))
                {
                    return null; // return null for circular refs
                }
                
                visited.Add(refId);
                
                if (_openApiDocument.Components?.Schemas?.TryGetValue(refId, out var refSchema) == true)
                {
                    return GenerateDummyData(refSchema, depth + 1, visited, propertyName);
                }
                return null;
            }

            switch (schema.Type)
            {
                case "string":
                    if (schema.Format == "date-time") return DateTime.Now.ToString("o");
                    if (schema.Format == "date") return DateTime.Now.ToString("yyyy-MM-dd");
                    if (schema.Format == "uuid") return Guid.NewGuid().ToString();
                    if (schema.Format == "email") return "example@email.com";
                    
                    // Smart field name detection
                    if (!string.IsNullOrEmpty(propertyName))
                    {
                        var lowerName = propertyName.ToLower();
                        
                        // Names
                        if (lowerName.EndsWith("name"))
                        {
                            string[] firstNames = { "John", "Jane", "Michael", "Sarah", "David", "Emily", "Robert", "Lisa", "James", "Mary" };
                            string[] lastNames = { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez" };
                            
                            if (lowerName.Contains("first"))
                                return firstNames[Random.Shared.Next(firstNames.Length)];
                            if (lowerName.Contains("last"))
                                return lastNames[Random.Shared.Next(lastNames.Length)];
                            if (lowerName.Contains("full") || lowerName.Contains("client") || lowerName.Contains("legal"))
                                return $"{firstNames[Random.Shared.Next(firstNames.Length)]} {lastNames[Random.Shared.Next(lastNames.Length)]}";
                            
                            // Default for any field ending in Name
                            return $"{firstNames[Random.Shared.Next(firstNames.Length)]} {lastNames[Random.Shared.Next(lastNames.Length)]}";
                        }
                        
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
