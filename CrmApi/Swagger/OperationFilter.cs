using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Linq;
using System.Reflection;

namespace CrmApi.Swagger
{
    public class OperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            // Add default responses
            AddDefaultResponses(operation, context);
            
            // Set operation ID if not set
            SetOperationId(operation, context);
            
            // Add operation summary and description from XML comments if available
            UpdateOperationMetadata(operation, context);
        }
        
        private void AddDefaultResponses(OpenApiOperation operation, OperationFilterContext context)
        {
            // Add 200 OK response if not present
            if (!operation.Responses.ContainsKey("200"))
            {
                operation.Responses.Add("200", new OpenApiResponse 
                { 
                    Description = "Success" 
                });
            }
            
            // Add 400 Bad Request response
            if (!operation.Responses.ContainsKey("400"))
            {
                operation.Responses.Add("400", new OpenApiResponse 
                { 
                    Description = "Bad Request - The request could not be understood or was missing required parameters." 
                });
            }
            
            // Add 401 Unauthorized for secured endpoints
            var hasAuthorizeAttribute = context.MethodInfo.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>() != null ||
                                     context.MethodInfo.DeclaringType?.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>() != null;
            
            if (!operation.Responses.ContainsKey("401") && hasAuthorizeAttribute)
            {
                operation.Responses.Add("401", new OpenApiResponse 
                { 
                    Description = "Unauthorized - Authentication failed or user doesn't have permissions for the requested operation." 
                });
            }
            
            // Add 403 Forbidden for authorization failures
            if (!operation.Responses.ContainsKey("403") && hasAuthorizeAttribute)
            {
                operation.Responses.Add("403", new OpenApiResponse 
                { 
                    Description = "Forbidden - The authenticated user doesn't have permission to access this resource." 
                });
            }
            
            // Add 500 Internal Server Error
            if (!operation.Responses.ContainsKey("500"))
            {
                operation.Responses.Add("500", new OpenApiResponse 
                { 
                    Description = "Internal Server Error - An error occurred while processing your request." 
                });
            }
        }
        
        private void SetOperationId(OpenApiOperation operation, OperationFilterContext context)
        {
            if (string.IsNullOrEmpty(operation.OperationId))
            {
                var controllerName = context.MethodInfo.DeclaringType?.Name.Replace("Controller", string.Empty) ?? "Unknown";
                var actionName = context.MethodInfo.Name;
                operation.OperationId = $"{controllerName}_{actionName}";
            }
        }
        
        private void UpdateOperationMetadata(OpenApiOperation operation, OperationFilterContext context)
        {
            // This would be populated by Swashbuckle's XmlCommentsDocumentFilter if XML documentation is enabled
            // We're keeping this as a placeholder for any additional metadata processing
            
            // Example: Add a default summary if none exists
            if (string.IsNullOrEmpty(operation.Summary))
            {
                operation.Summary = $"{context.MethodInfo.Name} operation";
            }
            
            // Example: Add a default description if none exists
            if (string.IsNullOrEmpty(operation.Description))
            {
                operation.Description = $"Handles {context.ApiDescription.HttpMethod} requests for {context.ApiDescription.RelativePath}";
            }
        }
    }
}
