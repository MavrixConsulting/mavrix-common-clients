# Mavrix.Common.Dataverse

A lightweight helper library for interacting with Microsoft Dataverse using .NET (net8.0 / net9.0).
Provides:
- `IDataverseHttpClient` (Managed Identity auth & retry handling)
- Generic repository `IDataverseRepository<T>` for CRUD, upsert, delete, associations
- Fluent OData query builder (select, filter, expand, order, apply, top, count, annotations)
- DTO base + attribute (`DataverseTable` + `DataverseSetNameAttribute`)
- Deserialization model for Dataverse RemoteExecutionContext (Service Bus plugin messages)

## Installation
NuGet package: `Mavrix.Common.Dataverse`
```
dotnet add package Mavrix.Common.Dataverse
```

## Configuration (`appsettings.json`)
```
{
  "Dataverse": {
    "BaseUrl": "https://YOURORG.crm4.dynamics.com"
  }
}
```

## Dependency Injection (`Program.cs`)
```
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDataverseClient(builder.Configuration)
                .AddDataverseRepository<Account>()
                .AddDataverseRepository<Contact>();

builder.Logging.AddDataverseDefaultLoggingSettings();
```

## Define Table DTOs
```
[DataverseSetName("accounts")]
public class Account : DataverseTable
{
    [JsonPropertyName("accountid")] public override Guid? Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
}
```

## Basic CRUD
```
public class AccountService
{
    private readonly IDataverseRepository<Account> _accounts;
    public AccountService(IDataverseRepository<Account> accounts) => _accounts = accounts;

    public Task<Guid?> CreateAsync(string name, CancellationToken ct) =>
        _accounts.CreateAsync(new Account { Name = name }, ct).AsTask();

    public Task<Account?> GetAsync(Guid id, CancellationToken ct)
    {
        var qb = new DataverseQueryBuilder().Select("accountid","name");
        return _accounts.GetAsync(id, qb, ct).AsTask();
    }
}
```

## Query Builder Examples
Select + filter + top:
```
var qb = new DataverseQueryBuilder()
    .Select("accountid","name")
    .Filter(new FilterBuilder("statecode eq 0").And("name eq 'Test'"))
    .Top(10);
await foreach (var acc in _accounts.GetListAsync(qb, ct)) { }
```
Expand + nested select + annotations:
```
var qb = new DataverseQueryBuilder()
    .Select("accountid","name")
    .AddExpand(new ExpandBuilder("primarycontactid").WithSelect("fullname","emailaddress1"))
    .SetInludeAnnotations();
var account = await _accounts.GetAsync(id, qb, ct);
```
Order + count:
```
var qb = new DataverseQueryBuilder()
    .Select("accountid")
    .OrderBy("name asc")
    .Count();
```

## Associations / References
```
await _accounts.AssociateLinkEntityAsync(
    accountId,
    relationshipName: "contact_customer_accounts", // Relationship logical name
    targetSetName: "contacts",
    targetKey: contactId,
    cancellationToken: ct);

await _accounts.DisassociateLinkEntityAsync(accountId, "contact_customer_accounts", contactId, ct);

// Remove single-valued lookup
await _accounts.RemoveReferenceValueAsync(accountId, "primarycontactid", ct);
```

## Update vs Upsert
```
await _accounts.UpdateAsync(id, partialEntity, ct); // PATCH (If-Match: *)
await _accounts.UpsertAsync(id, fullEntity, ct);    // PUT
```

## RemoteExecutionContext Deserialization
```
var context = JsonSerializer.Deserialize<DataverseExecutionContext>(jsonBody, new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
});

var target = context?.Target;       // Entity
var pre = context?.PreImage;        // Pre image
var post = context?.PostImage;      // Post image
if (target?.TryGetAttributeValue<string>("name", out var name)) { }
```
Supports: InputParameters, Pre/Post images, nested ParentContext, OptionSetValue, EntityReference, Money, EntityCollection, ColumnSet, Relationship, primitives, Microsoft `/Date(�. )/` timestamps.

## Authentication
Uses `DefaultAzureCredential` (Managed Identity, Service Principal, etc.). Ensure:
1. Managed Identity / App Registration has Dataverse Application User.
2. Proper security role assigned.

## Resilience & Logging
- Automatic retry (HTTP 429) with exponential backoff (max 3).
- `AddDataverseDefaultLoggingSettings()` suppresses noisy `HttpClient` traces (keeps warnings+).

## Include Annotations
Add `Prefer: odata.include-annotations="*"` via:
```
var qb = new DataverseQueryBuilder().SetInludeAnnotations();
```

## Error Handling
Throws:
- `DataverseHttpClient.DataverseException` (Dataverse logical error; contains StatusCode, Code, Message)
- `DataverseHttpClient.DataverseHttpClientException` (unexpected HTTP failure)

## Versioning
Current preview: `1.0.0-beta`. Breaking changes => major increment.

## License
MIT (see `LICENSE`).

## Notes
Extend DTOs with additional logical column names as required.