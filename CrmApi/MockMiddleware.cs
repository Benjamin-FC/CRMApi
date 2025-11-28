using Microsoft.OpenApi;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;

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
                    _openApiDocument = new OpenApiDocument();
                    return;
                }
                
                using var stream = File.OpenRead(filePath);
                var reader = new OpenApiStreamReader();
                _openApiDocument = reader.Read(stream, out var diagnostic);

                if (diagnostic.Errors.Any())
                {
                    Console.WriteLine("Swagger parse errors:");
                    foreach (var error in diagnostic.Errors)
                    {
                        Console.WriteLine(error.Message);
                    }
                }
                else
                {
                    Console.WriteLine("Swagger loaded successfully.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading swagger: {ex}");
                throw;
            }
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? string.Empty;
            var method = context.Request.Method;

            // Check if this is a Swagger UI or static file request
            if (path.StartsWith("/swagger") || path == "/favicon.ico" || path.StartsWith("/CRMApi/swagger"))
            {
                await _next(context);
                return;
            }

            try
            {
                // Remove base path if present
                var requestPath = path.StartsWith("/CRMApi") ? path.Substring("/CRMApi".Length) : path;
                
                // Find matching operation in our OpenAPI document
                var operation = _openApiDocument.Paths
                    .Where(p => IsPathMatch(p.Key, requestPath, out _))
                    .SelectMany(p => p.Value.Operations
                        .Where(o => o.Key.ToString().ToLower() == method.ToLower())
                        .Select(o => new { Path = p.Key, Operation = o.Value }))
                    .FirstOrDefault();

                if (operation != null)
                {
                    var parameters = GetPathParameters(operation.Path, requestPath);
                    var successResponse = operation.Operation.Responses
                        .FirstOrDefault(r => r.Key.StartsWith("2"));

                    if (!string.IsNullOrEmpty(successResponse.Key))
                    {
                        context.Response.StatusCode = int.Parse(successResponse.Key);
                        context.Response.ContentType = "application/json";

                        // Generate dummy response based on the schema
                        var schema = successResponse.Value.GetResponseSchema();
                        if (schema != null)
                        {
                            var dummyData = GenerateDummyData(schema, parameters);
                            await context.Response.WriteAsync(JsonSerializer.Serialize(dummyData, 
                                new JsonSerializerOptions { WriteIndented = true }));
                            return;
                        }
                    }
                }

                // If we get here, either no matching operation or no response schema
                context.Response.StatusCode = StatusCodes.Status200OK;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { 
                    message = "Mock response",
                    path = requestPath,
                    method = method,
                    timestamp = DateTime.UtcNow
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { 
                    error = ex.Message,
                    stackTrace = ex.StackTrace,
                    timestamp = DateTime.UtcNow
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
        }

        // Helper to match swagger path templates like /api/v1/ClientData/{id}
        private bool IsPathMatch(string template, string actual, out Dictionary<string, string> parameters)
        {
            parameters = new Dictionary<string, string>();
            var templateSegments = template.Trim('/').Split('/');
            var actualSegments = actual.Trim('/').Split('/');

            if (templateSegments.Length != actualSegments.Length)
                return false;

            for (int i = 0; i < templateSegments.Length; i++)
            {
                var templateSegment = templateSegments[i];
                var pathSegment = actualSegments[i];

                if (templateSegment.StartsWith("{") && templateSegment.EndsWith("}"))
                {
                    var paramName = templateSegment.Trim('{', '}');
                    parameters[paramName] = pathSegment;
                }
                else if (!string.Equals(templateSegment, pathSegment, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        // Extract values for path parameters (e.g., {id})
        private Dictionary<string, string> GetPathParameters(string template, string actual)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var templateSegments = template.Trim('/').Split('/');
            var actualSegments = actual.Trim('/').Split('/');
            
            for (int i = 0; i < templateSegments.Length && i < actualSegments.Length; i++)
            {
                var tSeg = templateSegments[i];
                if (tSeg.StartsWith("{") && tSeg.EndsWith("}"))
                {
                    var paramName = tSeg.Trim('{', '}');
                    result[paramName] = actualSegments[i];
                }
            }
            return result;
        }

        // Recursive dummy data generator with optional overrides for Id fields
        private JsonNode? GenerateDummyData(OpenApiSchema? schema, Dictionary<string, string>? overrides = null, int depth = 0, HashSet<string>? visited = null)
        {
            if (depth > 20) return null;
            if (schema == null) return null;
            visited ??= new HashSet<string>();

            // If schema has properties, generate an object
            if (schema.Properties != null && schema.Properties.Count > 0)
            {
                var obj = new JsonObject();
                foreach (var prop in schema.Properties)
                {
                    // If an override exists for an Id field, use it
                    if (overrides != null && overrides.TryGetValue("id", out var overrideVal) && 
                        prop.Key.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
                    {
                        // Use the path parameter value directly; convert if property expects a number
                        if (prop.Value.Type == "integer" || prop.Value.Type == "number")
                        {
                            if (int.TryParse(overrideVal, out var intVal))
                                obj.Add(prop.Key, intVal);
                            else if (double.TryParse(overrideVal, out var doubleVal))
                                obj.Add(prop.Key, doubleVal);
                            else
                                obj.Add(prop.Key, overrideVal);
                        }
                        else
                        {
                            obj.Add(prop.Key, overrideVal);
                        }
                    }
                    else
                    {
                        obj.Add(prop.Key, GenerateDummyData(prop.Value, overrides, depth + 1, visited));
                    }
                }
                return obj;
            }

            // Resolve $ref if present
            if (schema.Reference != null)
            {
                var refId = schema.Reference.Id;
                if (visited.Contains(refId)) return null; // prevent circular refs
                visited.Add(refId);
                if (_openApiDocument.Components?.Schemas?.TryGetValue(refId, out var refSchema) == true)
                {
                    return GenerateDummyData(refSchema, overrides, depth + 1, visited);
                }
                return null;
            }

            // Handle array type
            if (schema.Type == "array" && schema.Items != null)
            {
                var array = new JsonArray();
                var itemCount = Math.Min(3, depth + 1); // Limit array size based on depth
                for (int i = 0; i < itemCount; i++)
                {
                    array.Add(GenerateDummyData(schema.Items, overrides, depth + 1, visited));
                }
                return array;
            }

            // Primitive types
            switch (schema.Type?.ToLower())
            {
                case "string":
                    if (schema.Format == "date-time") return DateTime.Now.ToString("o");
                    if (schema.Format == "date") return DateTime.Now.ToString("yyyy-MM-dd");
                    if (schema.Format == "uuid") return Guid.NewGuid().ToString();
                    if (schema.Format == "email") return "john.doe@example.com";
                    return "Sample Text";
                case "integer":
                    if (schema.Format == "int64") return 1234567890L;
                    return Random.Shared.Next(1, 1000);
                case "number":
                    if (schema.Format == "float") return 123.45f;
                    return Math.Round(Random.Shared.NextDouble() * 1000, 2);
                case "boolean":
                    return Random.Shared.Next(2) == 1;
                case "object":
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

        // Extension method to get the schema from a response
        public static OpenApiSchema? GetResponseSchema(this OpenApiResponse response)
        {
            if (response?.Content?.TryGetValue("application/json", out var mediaType) == true)
            {
                return mediaType.Schema;
            }
            return null;
        }
    }
}
