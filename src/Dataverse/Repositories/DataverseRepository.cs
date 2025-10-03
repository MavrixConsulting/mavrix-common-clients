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

		public async ValueTask<Guid?> CreateAsync(T record, CancellationToken cancellationToken)
		{
			var content = JsonContent.Create(record, options: _jsonSerializerOptions);
			return await _client.CreateAsync(_setName, content, cancellationToken);
		}

		public async ValueTask<T?> GetAsync(Guid key, DataverseQueryBuilder queryBuilder, CancellationToken cancellationToken)
		{
			var uri = queryBuilder.Build(_setName, key);
			return await _client.GetAsync<T>(uri, cancellationToken, queryBuilder.InludeAnnotations);
		}

		public async IAsyncEnumerable<T> GetListAsync(DataverseQueryBuilder queryBuilder, [EnumeratorCancellation] CancellationToken cancellationToken)
		{
			var uri = queryBuilder.Build(_setName);
			await foreach (var item in _client.GetListAsync<T>(uri, cancellationToken, queryBuilder.InludeAnnotations))
			{
				yield return item;
			}
		}

		public async ValueTask UpdateAsync(Guid key, T record, CancellationToken cancellationToken)
		{
			var content = JsonContent.Create(record, options: _jsonSerializerOptions);
			var uri = $"{_setName}({key})";
			await _client.UpdateAsync(uri, content, cancellationToken);
		}

		public async ValueTask UpsertAsync(Guid key, T record, CancellationToken cancellationToken)
		{
			var content = JsonContent.Create(record, options: _jsonSerializerOptions);
			var uri = $"{_setName}({key})";
			await _client.UpsertAsync(uri, content, cancellationToken);
		}

		public async ValueTask DeleteAsync(Guid key, CancellationToken cancellationToken)
		{
			var uri = $"{_setName}({key})";
			await _client.DeleteAsync(uri, cancellationToken);
		}

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

		public async ValueTask DisassociateLinkEntityAsync(Guid sourceKey, string relationshipName, Guid targetKey, CancellationToken cancellationToken)
		{
			var uri = $"{_setName}{sourceKey}/{relationshipName}({targetKey})/$ref";
			await _client.DeleteAsync(uri, cancellationToken);
		}

		public async ValueTask RemoveReferenceValueAsync(Guid key, string propertyName, CancellationToken cancellationToken)
		{
			var uri = $"{_setName}({key})/{propertyName}/$ref";
			await _client.DeleteAsync(uri, cancellationToken);
		}
	}
}
