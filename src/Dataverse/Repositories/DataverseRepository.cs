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
	/// <summary>
	/// Provides CRUD and relationship operations for a Dataverse entity set specified by <typeparamref name="T"/>.
	/// </summary>
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

		/// <inheritdoc />
		public async ValueTask<Guid?> CreateAsync(T record, CancellationToken cancellationToken)
		{
			var content = JsonContent.Create(record, options: _jsonSerializerOptions);
			return await _client.CreateAsync(_setName, content, cancellationToken);
		}

		/// <inheritdoc />
		public async ValueTask<T?> GetAsync(Guid key, DataverseQueryBuilder queryBuilder, CancellationToken cancellationToken)
		{
			return await GetAsync((DataverseKey)key, queryBuilder, cancellationToken);
		}

		/// <inheritdoc />
		public async ValueTask<T?> GetAsync(DataverseKey key, DataverseQueryBuilder queryBuilder, CancellationToken cancellationToken)
		{
			var uri = queryBuilder.Build(_setName, key);
			return await _client.GetAsync<T>(uri, cancellationToken, queryBuilder.IncludeAnnotations);
		}

		/// <inheritdoc />
		public async IAsyncEnumerable<T> GetListAsync(DataverseQueryBuilder queryBuilder, [EnumeratorCancellation] CancellationToken cancellationToken)
		{
			var uri = queryBuilder.Build(_setName);
			await foreach (var item in _client.GetListAsync<T>(uri, cancellationToken, queryBuilder.IncludeAnnotations))
			{
				yield return item;
			}
		}

		/// <inheritdoc />
		public async ValueTask UpdateAsync(Guid key, T record, CancellationToken cancellationToken)
		{
			await UpdateAsync((DataverseKey)key, record, cancellationToken);
		}

		/// <inheritdoc />
		public async ValueTask UpdateAsync(DataverseKey key, T record, CancellationToken cancellationToken)
		{
			var content = JsonContent.Create(record, options: _jsonSerializerOptions);
			var uri = $"{_setName}({key.KeyExpression})";
			await _client.UpdateAsync(uri, content, cancellationToken);
		}

		/// <inheritdoc />
		public async ValueTask UpsertAsync(Guid key, T record, CancellationToken cancellationToken)
		{
			await UpsertAsync((DataverseKey)key, record, cancellationToken);
		}

		/// <inheritdoc />
		public async ValueTask UpsertAsync(DataverseKey key, T record, CancellationToken cancellationToken)
		{
			var content = JsonContent.Create(record, options: _jsonSerializerOptions);
			var uri = $"{_setName}({key.KeyExpression})";
			await _client.UpsertAsync(uri, content, cancellationToken);
		}

		/// <inheritdoc />
		public async ValueTask DeleteAsync(Guid key, CancellationToken cancellationToken)
		{
			await DeleteAsync((DataverseKey)key, cancellationToken);
		}

		/// <inheritdoc />
		public async ValueTask DeleteAsync(DataverseKey key, CancellationToken cancellationToken)
		{
			var uri = $"{_setName}({key.KeyExpression})";
			await _client.DeleteAsync(uri, cancellationToken);
		}

		/// <inheritdoc />
		public async ValueTask AssociateLinkEntityAsync(Guid sourceKey, string relationshipName, string targetSetName, Guid targetKey, CancellationToken cancellationToken)
		{
			var uri = $"{_setName}({sourceKey})/{relationshipName}/$ref";
			var linkUri = $"{_client.ApiUrl}/{targetSetName}({targetKey})";
			var linkEntity = new LinkEntity { EntityId = linkUri };
			var content = JsonContent.Create(linkEntity, options: _jsonSerializerOptions);
			await _client.CreateAsync(uri, content, cancellationToken);
		}

		/// <summary>
		/// Represents a Dataverse reference payload pointing to a related entity.
		/// </summary>
		public class LinkEntity
		{
			/// <summary>
			/// Gets or sets the absolute OData identifier of the linked entity.
			/// </summary>
			[JsonPropertyName("@odata.id")]
			public string? EntityId { get; set; }
		}

		/// <inheritdoc />
		public async ValueTask DisassociateLinkEntityAsync(Guid sourceKey, string relationshipName, Guid targetKey, CancellationToken cancellationToken)
		{
			var uri = $"{_setName}{sourceKey}/{relationshipName}({targetKey})/$ref";
			await _client.DeleteAsync(uri, cancellationToken);
		}

		/// <inheritdoc />
		public async ValueTask RemoveReferenceValueAsync(Guid key, string propertyName, CancellationToken cancellationToken)
		{
			var uri = $"{_setName}({key})/{propertyName}/$ref";
			await _client.DeleteAsync(uri, cancellationToken);
		}
	}
}
