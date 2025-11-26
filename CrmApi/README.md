# CRM Mock API Server

This is a C# ASP.NET Core Web API server that automatically generates dummy data responses based on your `swagger.json` OpenAPI specification.

## What Was Created

1. **Fixed swagger.json** - Removed the invalid URL from the beginning of the file to make it valid JSON.

2. **Mock API Server** - A C# Web API that:
   - Loads the OpenAPI specification from `swagger.json`
   - Intercepts incoming HTTP requests
   - Matches them to paths defined in the swagger file
   - Generates dummy JSON responses based on the schema definitions

##Features

- ✅ Automatic endpoint detection from swagger.json
- ✅ Dynamic dummy data generation based on schema types
- ✅ Support for complex nested objects
- ✅ Handling of circular reference detection
- ✅ Support for arrays, objects, strings, numbers, booleans, dates
- ✅ Path parameter matching (e.g., `/api/v1/ClientData/{id}`)

## How to Run

```bash
cd CrmApi
dotnet run --urls=http://localhost:5000
```

The server will start at `http://localhost:5000`.

## Testing Endpoints

Once the server is running, you can test any endpoint defined in your swagger.json file:

**Example Endpoints:**

- `http://localhost:5000/api/v1/ClientData/1`
- `http://localhost:5000/api/v1/ClientData/123/division/numbers`
- `http://localhost:5000/api/v1/ClientData/pi-screen/1`
- etc.

All GET and POST endpoints defined in the swagger.json will return appropriate dummy data.

## Sample Response

```json
{
  "clientId": "string_value",
  "editApproval": "string_value",
  "dba": "string_value",
  "clientLegalName": "string_value",
  "complianceHold": "string_value",
  "level": "string_value",
  "paymentTermID": "string_value",
  "paymentMethod": "string_value",
  "status": "string_value"
}
```

## Files

- `Program.cs` - Main application entry point
- `MockMiddleware.cs` - Middleware that intercepts requests and generates dummy responses
- `swagger.json` - Your OpenAPI specification
- `CrmApi.csproj` - Project configuration

## How It Works

1. On startup, the middleware loads and parses `swagger.json`
2. For each incoming request:
   - It matches the request path and method to an endpoint in the swagger file
   - It extracts the response schema for HTTP 200 responses
   - It generates dummy JSON data based on the schema type and properties
   - It returns the generated data as JSON

## Next Steps

If you need a React frontend to interact with this API and display all available endpoints, let me know!
