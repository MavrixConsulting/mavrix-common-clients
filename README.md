# Mavrix.Common.Dataverse

A lightweight helper library for interacting with Microsoft Dataverse using .NET (net8.0 / net9.0 / net10.0).
Provides:
- `IDataverseHttpClient` (Managed Identity auth & retry handling)
- Generic repository `IDataverseRepository<T>` for CRUD, upsert, delete, associations
- `IDataverseBatchService` for atomic multi-operation change sets across tables
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

## Lookup Attributes in DTOs
Use `Lookup<T>` for Dataverse single-valued navigation properties that need `@odata.bind` payloads.

Define lookup columns on your DTO:
```
[DataverseSetName("contacts")]
public class Contact : DataverseTable
{
    [JsonPropertyName("contactid")]
    public override Guid? Id { get; set; }

    [JsonPropertyName("ParentCustomerId_account@odata.bind")]
    public Lookup<Account>? ParentCustomerIdAccount { get; set; }

    [JsonPropertyName("ParentCustomerId_contact@odata.bind")]
    public Lookup<Contact>? ParentCustomerIdContact { get; set; }

    [JsonPropertyName("AccountId@odata.bind")]
    public Lookup<Account>? AccountId { get; set; }
}
```

Assign lookup values:
```
// By GUID (implicit conversion from Guid)
contact.AccountId = accountId;

// By nullable GUID (null => no value written because nulls are ignored)
contact.ParentCustomerIdContact = maybeParentContactId;

// By alternate key: /accounts(accountnumber='ACME-001')
contact.AccountId = new Lookup<Account>("accountnumber", "ACME-001");

// By raw key expression when needed
contact.AccountId = new Lookup<Account>("accountnumber='ACME-001'");
```

Notes:
- `T` in `Lookup<T>` must be a Dataverse table type with `[DataverseSetName("...")]`.
- String key values are escaped automatically for single quotes.
- Serialized output is written as the correct bind path (for example `/accounts(<key>)`).

## DataverseKey
Use `DataverseKey` when you need to address records by GUID, alternate keys, composite alternate keys, or a raw key expression.

Create keys:
```
var byGuid = new DataverseKey(accountId);

// Single alternate key: accountnumber='ACME-001'
var byAltKey = new DataverseKey("accountnumber", "ACME-001");

// Composite alternate key: customercode='ACME',region='EU'
var byComposite = new DataverseKey(("customercode", "ACME"), ("region", "EU"));

// Raw key expression when needed
var byRaw = new DataverseKey("accountnumber='ACME-001'");
```

Use keys with repository overloads:
```
var qb = new DataverseQueryBuilder().Select("accountid", "name");

var account = await _accounts.GetAsync(byAltKey, qb, ct);
await _accounts.UpdateAsync(byAltKey, partialEntity, ct);
await _accounts.UpsertAsync(byAltKey, fullEntity, ct);
await _accounts.DeleteAsync(byAltKey, ct);
```

Use keys with lookup values:
```
var parentKey = new DataverseKey("accountnumber", "ACME-001");
contact.AccountId = parentKey; // implicit DataverseKey -> Lookup<Account>
```

When to use `Guid` vs `DataverseKey`:
- Use `Guid` overloads when you already have the Dataverse record ID.
- Use `DataverseKey` overloads when you need alternate key support, composite key support, or want to pass a raw OData key expression.
- `DataverseKey` also keeps key construction consistent between repository methods and `Lookup<T>` assignments.

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
    .SetIncludeAnnotations();
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

## Atomic Change Sets (Batch)
Use `IDataverseBatchService` when you need operations across multiple tables to succeed or fail as one transaction.

```
public class ContactWriteService
{
    private readonly IDataverseBatchService _batchService;

    public ContactWriteService(IDataverseBatchService batchService)
    {
        _batchService = batchService;
    }

    public async Task UpdateContactAndCreateRelatedAsync(Guid contactId, CancellationToken ct)
    {
        var result = await _batchService
            .CreateChangeSet()
            .Update(
                new DataverseKey("emailaddress1", "ada@example.com"),
                new Contact
                {
                    ParentCustomerIdContact = Guid.NewGuid()
                },
                contentId: 1)
            .Create(
                new Account(),
                contentId: 2)
            .ExecuteAsync(ct);

        var updatedContactId = result.GetCreatedEntityId(1);
        var createdTaskId = result.GetCreatedEntityId(2);
    }
}
```

Notes:
- `GET` is not allowed inside a change set.
- Operations are executed in order and rolled back if one fails.
- Use `contentId` + `$n` references when one operation depends on a created record URI.
- Alternate keys are supported through `DataverseKey` in typed overloads, for example `new DataverseKey("emailaddress1", "ada@example.com")`.

If you build operations directly (without the fluent builder), pass the shared `JsonSerializerOptions` explicitly:
```
var jsonOptions = serviceProvider.GetRequiredService<JsonSerializerOptions>();

var updateOperation = DataverseBatchOperation.Update(
    new DataverseKey("emailaddress1", "ada@example.com"),
    new Contact { ParentCustomerIdContact = Guid.NewGuid() },
    jsonOptions,
    contentId: 1);

var createOperation = DataverseBatchOperation.Create(
    new Account(),
    jsonOptions,
    contentId: 2);
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

To supply your own token provider instead of registering `ManagedIdentityTokenProvider`, call `AddDataverseClient` with `useManagedIdentity: false`:
```
builder.Services.AddDataverseClient(builder.Configuration, useManagedIdentity: false);
```

## Resilience & Logging
- Automatic retry (HTTP 429) with exponential backoff (max 3).
- `AddDataverseDefaultLoggingSettings()` suppresses noisy `HttpClient` traces (keeps warnings+).

## Include Annotations
Add `Prefer: odata.include-annotations="*"` via:
```
var qb = new DataverseQueryBuilder().SetIncludeAnnotations();
```

## Error Handling
Throws:
- `DataverseHttpClient.DataverseException` (Dataverse logical error; contains StatusCode, Code, Message)
- `DataverseHttpClient.DataverseHttpClientException` (unexpected HTTP failure)

## JSON Serialization Customization
Default behavior:
- Ignores null values (`JsonIgnoreCondition.WhenWritingNull`)
- Skips serializing collections when they are null or empty
- Leaves strings untouched (empty strings are written)

You can customize the shared `JsonSerializerOptions` in two (combinable) ways:

1. Inline configuration lambda (per application – passed to `AddDataverseClient`):
```
builder.Services.AddDataverseClient(builder.Configuration, opts =>
{
    opts.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    opts.WriteIndented = true;          // example
    opts.Converters.Add(new JsonStringEnumConverter());
});
```

2. One or more configurator classes implementing `IDataverseJsonSerializerOptionsConfigurator`:
```
public sealed class MyDataverseSerializerConfigurator : IDataverseJsonSerializerOptionsConfigurator
{
    public void Configure(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter());
    }
}

builder.Services.AddDataverseJsonSerializerConfigurator(new MyDataverseSerializerConfigurator());

builder.Services.AddDataverseClient(builder.Configuration); // builds options after configurators are registered
```

3. Combine both (lambda runs first, then each configurator in registration order):
```
builder.Services
    .AddDataverseJsonSerializerConfigurator(new MyDataverseSerializerConfigurator())
    .AddDataverseJsonSerializerConfigurator(new AnotherConfigurator())
    .AddDataverseClient(builder.Configuration, opts =>
    {
        // Base tweaks; later configurators can override
        opts.WriteIndented = true;
    });
```

If you never call `AddDataverseJsonSerializerConfigurator` you still get sensible defaults (and optional lambda changes). If you only want configurators, omit the lambda. For more granular control over ordering, place logic in configurators instead of the lambda.

## Versioning
Current: `1.0.7`. Breaking changes => major increment.

## License
MIT (see `LICENSE`).

## Notes
Extend DTOs with additional logical column names as required.