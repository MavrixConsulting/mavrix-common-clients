using Mavrix.Common.Dataverse.Clients;
using Mavrix.Common.Dataverse.CustomAttributes;
using Mavrix.Common.Dataverse.DTO;
using Mavrix.Common.Dataverse.QueryBuilder;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mavrix.Common.Dataverse.Repositories
{
	public class DataverseRepository<T> : IDataverseRepository<T> where T : DataverseTable
	{
		private readonly IDataverseHttpClient _client;
		private readonly string _setName;
		private readonly JsonSerializerOptions _jsonSerializerOptions;

		/// <summary>
		/// Initializes a repository for the Dataverse set determined by the <see cref="DataverseSetNameAttribute"/> on <typeparamref name="T"/>.
		/// </summary>
		/// <param name="client">Dataverse HTTP client used to issue requests.</param>
		/// <param name="jsonSerializerOptions">Serializer options applied to outgoing payloads.</param>
		public DataverseRepository(IDataverseHttpClient client, JsonSerializerOptions jsonSerializerOptions)
		{
			_client = client;
			_jsonSerializerOptions = jsonSerializerOptions;

			var setNameAttribute = typeof(T).GetCustomAttribute<DataverseSetNameAttribute>();
			if (setNameAttribute is null || string.IsNullOrEmpty(setNameAttribute.SetName))
			{
				throw new InvalidOperationException($"Type {typeof(T).Name} must be decorated with DataverseSetNameAttribute");
			}
			_setName = setNameAttribute.SetName;
		}

		/// <summary>
		/// Creates a Dataverse record of type <typeparamref name="T"/>.
		/// </summary>
		/// <param name="record">Record payload to persist.</param>
		/// <param name="cancellationToken">Cancellation token for the request.</param>
		/// <returns>The identifier of the newly created record, when returned by Dataverse.</returns>
		public async ValueTask<Guid?> CreateAsync(T record, CancellationToken cancellationToken)
		{
			var content = JsonContent.Create(record, options: _jsonSerializerOptions);
			return await _client.CreateAsync(_setName, content, cancellationToken);
		}

		/// <summary>
		/// Retrieves a single record by key using the provided query builder.
		/// </summary>
		/// <param name="key">Primary key identifying the record.</param>
		/// <param name="queryBuilder">Builder describing select, expand, or annotation options.</param>
		/// <param name="cancellationToken">Cancellation token for the request.</param>
		/// <returns>The matching record or <c>null</c> when not found.</returns>
		public async ValueTask<T?> GetAsync(Guid key, DataverseQueryBuilder queryBuilder, CancellationToken cancellationToken)
		{
			var uri = queryBuilder.Build(_setName, key);
			return await _client.GetAsync<T>(uri, cancellationToken, queryBuilder.InludeAnnotations);
		}

		/// <summary>
		/// Streams records that satisfy the provided query definition.
		/// </summary>
		/// <param name="queryBuilder">Builder describing filters, selects, and pagination.</param>
		/// <param name="cancellationToken">Cancellation token for the request.</param>
		/// <returns>An async enumerable yielding each retrieved record.</returns>
		public async IAsyncEnumerable<T> GetListAsync(DataverseQueryBuilder queryBuilder, [EnumeratorCancellation] CancellationToken cancellationToken)
		{
			var uri = queryBuilder.Build(_setName);
			await foreach (var item in _client.GetListAsync<T>(uri, cancellationToken, queryBuilder.InludeAnnotations))
			{
				yield return item;
			}
		}

		/// <summary>
		/// Updates an existing record identified by the supplied key.
		/// </summary>
		/// <param name="key">Primary key of the record to update.</param>
		/// <param name="record">Payload containing the updated fields.</param>
		/// <param name="cancellationToken">Cancellation token for the request.</param>
		public async ValueTask UpdateAsync(Guid key, T record, CancellationToken cancellationToken)
		{
			var content = JsonContent.Create(record, options: _jsonSerializerOptions);
			var uri = $"{_setName}({key})";
			await _client.UpdateAsync(uri, content, cancellationToken);
		}

		/// <summary>
		/// Upserts a record, creating or updating it based on the provided key.
		/// </summary>
		/// <param name="key">Primary key used for the upsert target.</param>
		/// <param name="record">Payload to create or update.</param>
		/// <param name="cancellationToken">Cancellation token for the request.</param>
		public async ValueTask UpsertAsync(Guid key, T record, CancellationToken cancellationToken)
		{
			var content = JsonContent.Create(record, options: _jsonSerializerOptions);
			var uri = $"{_setName}({key})";
			await _client.UpsertAsync(uri, content, cancellationToken);
		}

		/// <summary>
		/// Deletes a record from the Dataverse set.
		/// </summary>
		/// <param name="key">Primary key of the record to remove.</param>
		/// <param name="cancellationToken">Cancellation token for the request.</param>
		public async ValueTask DeleteAsync(Guid key, CancellationToken cancellationToken)
		{
			var uri = $"{_setName}({key})";
			await _client.DeleteAsync(uri, cancellationToken);
		}

		/// <summary>
		/// Creates a relationship between the source record and a target record in another set.
		/// </summary>
		/// <param name="sourceKey">Primary key of the source record.</param>
		/// <param name="relationshipName">Relationship logical name to traverse.</param>
		/// <param name="targetSetName">Target set logical name.</param>
		/// <param name="targetKey">Primary key of the target record.</param>
		/// <param name="cancellationToken">Cancellation token for the request.</param>
		public async ValueTask AssociateLinkEntityAsync(Guid sourceKey, string relationshipName, string targetSetName, Guid targetKey, CancellationToken cancellationToken)
		{
			var uri = $"{_setName}({sourceKey})/{relationshipName}/$ref";
			var linkUri = $"{_client.ApiUrl}/{targetSetName}({targetKey})";
			var linkEntity = new LinkEntity { EntityId = linkUri };
			var content = JsonContent.Create(linkEntity, options: _jsonSerializerOptions);
			await _client.CreateAsync(uri, content, cancellationToken);
		}

		public class LinkEntity
		{
			[JsonPropertyName("@odata.id")]
			public string? EntityId { get; set; }
		}

		/// <summary>
		/// Removes the relationship between the source record and the specified target record.
		/// </summary>
		/// <param name="sourceKey">Primary key of the source record.</param>
		/// <param name="relationshipName">Relationship logical name.</param>
		/// <param name="targetKey">Primary key of the target record.</param>
		/// <param name="cancellationToken">Cancellation token for the request.</param>
		public async ValueTask DisassociateLinkEntityAsync(Guid sourceKey, string relationshipName, Guid targetKey, CancellationToken cancellationToken)
		{
			var uri = $"{_setName}{sourceKey}/{relationshipName}({targetKey})/$ref";
			await _client.DeleteAsync(uri, cancellationToken);
		}

		/// <summary>
		/// Removes the reference value from a lookup property on the specified record.
		/// </summary>
		/// <param name="key">Primary key of the record containing the reference.</param>
		/// <param name="propertyName">Lookup property name.</param>
		/// <param name="cancellationToken">Cancellation token for the request.</param>
		public async ValueTask RemoveReferenceValueAsync(Guid key, string propertyName, CancellationToken cancellationToken)
		{
			var uri = $"{_setName}({key})/{propertyName}/$ref";
			await _client.DeleteAsync(uri, cancellationToken);
		}
	}
}
