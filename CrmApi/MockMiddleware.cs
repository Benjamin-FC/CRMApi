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
                }
                using var stream = File.OpenRead(filePath);
                _openApiDocument = new OpenApiStreamReader().Read(stream, out var diagnostic);

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
            try
            {
                var path = context.Request.Path.Value ?? string.Empty;
                var method = context.Request.Method.ToUpperInvariant();

                // Find matching path template
                var matchingPath = _openApiDocument.Paths.Keys.FirstOrDefault(p => IsPathMatch(p, path));

                if (matchingPath != null)
                {
                    var pathItem = _openApiDocument.Paths[matchingPath];
                    var operationType = Enum.Parse<OperationType>(method, true);

                    if (pathItem.Operations.TryGetValue(operationType, out var operation))
                    {
                        if (operation.Responses.TryGetValue("200", out var response))
                        {
                            if (response.Content.TryGetValue("application/json", out var mediaType) ||
                                response.Content.TryGetValue("text/json", out mediaType) ||
                                response.Content.TryGetValue("text/plain", out mediaType)) // fallback
                            {
                                var schema = mediaType.Schema;
                                // Extract path parameters to override Id fields
                                var overrides = GetPathParameters(matchingPath, path);
                                var dummyData = GenerateDummyData(schema, 0, null, overrides);

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

        // Helper to match swagger path templates like /api/v1/ClientData/{id}
        private bool IsPathMatch(string template, string actual)
        {
            var regexPattern = "^" + Regex.Replace(template, @"\{[^}]+\}", "[^/]+") + "$";
            return Regex.IsMatch(actual, regexPattern, RegexOptions.IgnoreCase);
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
        private JsonNode? GenerateDummyData(OpenApiSchema? schema, int depth = 0, HashSet<string>? visited = null, Dictionary<string, string>? overrides = null)
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
                    if (overrides != null && prop.Key.EndsWith("Id", StringComparison.OrdinalIgnoreCase) && overrides.TryGetValue(prop.Key, out var overrideVal))
                    {
                        // Try to parse according to expected type
                        if (schema.Type == "integer" || schema.Type == "number")
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
                        obj.Add(prop.Key, GenerateDummyData(prop.Value, depth + 1, visited, overrides));
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
                    return GenerateDummyData(refSchema, depth + 1, visited, overrides);
                }
                return null;
            }

            // Primitive types
            switch (schema.Type)
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
                case "array":
                    var array = new JsonArray();
                    var itemCount = Random.Shared.Next(1, 4);
                    for (int i = 0; i < itemCount; i++)
                    {
                        array.Add(GenerateDummyData(schema.Items, depth + 1, visited, overrides));
                    }
                    return array;
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
    }
}
