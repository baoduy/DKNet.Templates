# API Configuration and Endpoint Development

## Overview
API endpoints are configured using ASP.NET Core Minimal APIs with versioning support. DKNet provides extension methods for clean, fluent endpoint configuration.

## Endpoint Configuration Structure

### 1. Endpoint Config Class
Create an endpoint configuration class that implements `IEndpointConfig`:
```csharp
using SlimBus.AppServices.Profiles.V1.Actions;
using SlimBus.AppServices.Profiles.V1.Queries;

namespace SlimBus.Api.ApiEndpoints;

internal sealed class ProfileV1Endpoint : IEndpointConfig
{
    #region Properties

    public int Version => 1;
    public string GroupEndpoint => "/profiles";

    #endregion

    #region Methods

    public void Map(RouteGroupBuilder group)
    {
        group.MapGetPage<PageProfilePageQuery, ProfileResult>("")
            .WithDescription("Get all profiles");
            
        group.MapGet<ProfileQuery, ProfileResult?>("{id:guid}")
            .WithDescription("Get profile by id");
            
        group.MapPost<CreateProfileCommand, ProfileResult>("")
            .AddIdempotencyFilter()
            .WithDescription("Create profile. Note: Idempotency key is required in the header.");
            
        group.MapPut<UpdateProfileCommand, ProfileResult>("{id:guid}")
            .WithDescription("Update profile by id");
            
        group.MapDelete<DeleteProfileCommand>("{id:guid}")
            .WithDescription("Delete profile by id");
    }

    #endregion
}
```

## Endpoint Mapping Methods

### MapGet - Single Item
```csharp
group.MapGet<ProfileQuery, ProfileResult?>("{id:guid}")
    .WithDescription("Get profile by id")
    .WithTags("Profiles")
    .Produces<ProfileResult>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);
```

**Signature**: `MapGet<TQuery, TResult>(string pattern)`
- `TQuery`: Query request type
- `TResult`: Response type (use `TResult?` for nullable results)
- `pattern`: Route pattern with parameters

### MapGetPage - Paginated List
```csharp
group.MapGetPage<PageProfilePageQuery, ProfileResult>("")
    .WithDescription("Get all profiles")
    .WithTags("Profiles")
    .Produces<PageResults<ProfileResult>>(StatusCodes.Status200OK);
```

**Signature**: `MapGetPage<TQuery, TResult>(string pattern)`
- Automatically returns `PageResults<TResult>`
- Query must inherit from `BasePageQuery`

### MapPost - Create
```csharp
group.MapPost<CreateProfileCommand, ProfileResult>("")
    .AddIdempotencyFilter()
    .WithDescription("Create a new profile")
    .WithTags("Profiles")
    .Produces<ProfileResult>(StatusCodes.Status201Created)
    .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);
```

**Signature**: `MapPost<TCommand, TResult>(string pattern)`
- `TCommand`: Command request type
- `TResult`: Response type
- Returns 201 Created with location header

### MapPut - Update
```csharp
group.MapPut<UpdateProfileCommand, ProfileResult>("{id:guid}")
    .WithDescription("Update profile by id")
    .WithTags("Profiles")
    .Produces<ProfileResult>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);
```

**Signature**: `MapPut<TCommand, TResult>(string pattern)`
- Route parameters are automatically bound to command properties

### MapDelete - Delete
```csharp
group.MapDelete<DeleteProfileCommand>("{id:guid}")
    .WithDescription("Delete profile by id")
    .WithTags("Profiles")
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound);
```

**Signature**: `MapDelete<TCommand>(string pattern)`
- Returns 204 No Content on success

## Route Parameters

### Route Constraints
```csharp
"{id:guid}"           // GUID
"{id:int}"            // Integer
"{id:long}"           // Long
"{name:alpha}"        // Alphabetic
"{code:length(5)}"    // Fixed length
"{slug:minlength(3)}" // Minimum length
```

### Multiple Parameters
```csharp
group.MapGet<GetProfileAddressQuery, AddressResult>("{profileId:guid}/addresses/{addressId:guid}")
    .WithDescription("Get specific address for a profile");
```

## API Versioning

### Version Support
Create separate endpoint configs for each version:
```csharp
// Version 1
internal sealed class ProfileV1Endpoint : IEndpointConfig
{
    public int Version => 1;
    public string GroupEndpoint => "/profiles";
    
    public void Map(RouteGroupBuilder group)
    {
        // V1 endpoints
    }
}

// Version 2
internal sealed class ProfileV2Endpoint : IEndpointConfig
{
    public int Version => 2;
    public string GroupEndpoint => "/profiles";
    
    public void Map(RouteGroupBuilder group)
    {
        // V2 endpoints (can differ from V1)
    }
}
```

### Accessing Versions
```
GET /api/v1/profiles
GET /api/v2/profiles
```

## Endpoint Enhancements

### Idempotency
For POST operations that should be idempotent:
```csharp
group.MapPost<CreateProfileCommand, ProfileResult>("")
    .AddIdempotencyFilter()
    .WithDescription("Create profile. Header: X-Idempotency-Key: {your-key}");
```

Client must provide header:
```
X-Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
```

### Authorization
```csharp
group.MapGet<ProfileQuery, ProfileResult?>("{id:guid}")
    .RequireAuthorization("ReadProfile");

group.MapPost<CreateProfileCommand, ProfileResult>("")
    .RequireAuthorization("WriteProfile");
```

### Rate Limiting
```csharp
group.MapPost<CreateProfileCommand, ProfileResult>("")
    .RequireRateLimiting("fixed");
```

### CORS
```csharp
group.MapGet<ProfileQuery, ProfileResult?>("{id:guid}")
    .RequireCors("AllowSpecificOrigins");
```

### Custom Filters
```csharp
group.MapPost<CreateProfileCommand, ProfileResult>("")
    .AddEndpointFilter<ValidationFilter>()
    .AddEndpointFilter<LoggingFilter>();
```

## OpenAPI/Swagger Documentation

### Description
```csharp
.WithDescription("Get profile by id")
```

### Tags
```csharp
.WithTags("Profiles", "Customer")
```

### Summary
```csharp
.WithSummary("Retrieves a customer profile by unique identifier")
```

### Response Types
```csharp
.Produces<ProfileResult>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status500InternalServerError)
```

### Deprecation
```csharp
.WithMetadata(new ApiVersionAttribute(1.0, Deprecated = true))
```

## Complete Example

```csharp
using SlimBus.AppServices.Profiles.V1.Actions;
using SlimBus.AppServices.Profiles.V1.Queries;

namespace SlimBus.Api.ApiEndpoints;

internal sealed class ProfileV1Endpoint : IEndpointConfig
{
    #region Properties

    public int Version => 1;
    public string GroupEndpoint => "/profiles";

    #endregion

    #region Methods

    public void Map(RouteGroupBuilder group)
    {
        // List with pagination
        group.MapGetPage<PageProfilePageQuery, ProfileResult>("")
            .WithDescription("Get all profiles with pagination")
            .WithTags("Profiles")
            .Produces<PageResults<ProfileResult>>(StatusCodes.Status200OK);

        // Get by ID
        group.MapGet<ProfileQuery, ProfileResult?>("{id:guid}")
            .WithDescription("Get profile by id")
            .WithTags("Profiles")
            .Produces<ProfileResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // Create
        group.MapPost<CreateProfileCommand, ProfileResult>("")
            .AddIdempotencyFilter()
            .WithDescription("Create a new profile")
            .WithTags("Profiles")
            .Produces<ProfileResult>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // Update
        group.MapPut<UpdateProfileCommand, ProfileResult>("{id:guid}")
            .WithDescription("Update profile by id")
            .WithTags("Profiles")
            .Produces<ProfileResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // Delete
        group.MapDelete<DeleteProfileCommand>("{id:guid}")
            .WithDescription("Delete profile by id")
            .WithTags("Profiles")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }

    #endregion
}
```

## Service Registration

Endpoints are auto-discovered and registered in Program.cs:
```csharp
app.MapApiVersioning([typeof(ProfileV1Endpoint).Assembly]);
```

## Best Practices

1. **Versioning**: Always start with V1, create new versions for breaking changes
2. **Idempotency**: Use for POST operations that create resources
3. **Descriptions**: Provide clear descriptions for all endpoints
4. **Tags**: Group related endpoints with tags
5. **Status Codes**: Document all possible response codes
6. **Route Constraints**: Use type constraints on route parameters
7. **Authorization**: Apply appropriate authorization policies
8. **Naming**: Use RESTful conventions (plural nouns for collections)
9. **HTTP Methods**:
   - GET: Retrieve (safe, idempotent)
   - POST: Create (not idempotent)
   - PUT: Update (idempotent)
   - DELETE: Remove (idempotent)
10. **Internal**: Mark endpoint configs as `internal sealed`

## Common Patterns

### Nested Resources
```csharp
group.MapGet<GetProfileAddressesQuery, List<AddressResult>>("{id:guid}/addresses")
    .WithDescription("Get all addresses for a profile");

group.MapPost<AddProfileAddressCommand, AddressResult>("{id:guid}/addresses")
    .WithDescription("Add an address to a profile");
```

### Search Endpoints
```csharp
group.MapGet<SearchProfilesQuery, List<ProfileResult>>("search")
    .WithDescription("Search profiles by various criteria");
```

### Batch Operations
```csharp
group.MapPost<BatchCreateProfilesCommand, List<ProfileResult>>("batch")
    .WithDescription("Create multiple profiles in a single request");
```

### Export Endpoints
```csharp
group.MapGet<ExportProfilesQuery, FileResult>("export")
    .WithDescription("Export profiles to CSV")
    .Produces<FileResult>(StatusCodes.Status200OK, "text/csv");
```

## File Location
```
{ProjectName}.Api/ApiEndpoints/{FeatureName}Endpoints.cs
```

## Testing Endpoints

### Using Swagger
Navigate to `/swagger` to test endpoints interactively.

### Using HTTP Files
Create `.http` files for testing:
```http
### Get all profiles
GET {{baseUrl}}/api/v1/profiles?pageIndex=1&pageSize=10

### Get profile by ID
GET {{baseUrl}}/api/v1/profiles/{{profileId}}

### Create profile
POST {{baseUrl}}/api/v1/profiles
Content-Type: application/json
X-Idempotency-Key: {{$guid}}

{
  "name": "John Doe",
  "email": "john.doe@example.com",
  "phone": "1234567890"
}

### Update profile
PUT {{baseUrl}}/api/v1/profiles/{{profileId}}
Content-Type: application/json

{
  "name": "Jane Doe",
  "phone": "0987654321"
}

### Delete profile
DELETE {{baseUrl}}/api/v1/profiles/{{profileId}}
```
