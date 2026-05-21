using Mavrix.Common.Dataverse.DTO;
using System.Text.Json;

namespace Mavrix.Common.Dataverse.Batch
{
	/// <summary>
	/// Fluent builder for composing Dataverse batch operations and executing them as a change set.
	/// </summary>
	/// <example>
	/// <code>
	/// var result = await batchService
	///     .CreateChangeSet()
	///     .Update(new DataverseKey("emailaddress1", "ada@example.com"), new Contact(), contentId: 1)
	///     .Create(new Account(), contentId: 2)
	///     .ExecuteAsync(cancellationToken);
	/// </code>
	/// </example>
	public sealed class DataverseBatchBuilder : IDataverseChangeSet
	{
		private readonly IDataverseBatchService _batchService;
		private readonly JsonSerializerOptions _jsonSerializerOptions;
		private readonly List<DataverseBatchOperation> _operations = [];

		internal DataverseBatchBuilder(IDataverseBatchService batchService, JsonSerializerOptions jsonSerializerOptions)
		{
			_batchService = batchService;
			_jsonSerializerOptions = jsonSerializerOptions;
		}

		/// <summary>
		/// Gets operations currently added to the change set in insertion order.
		/// </summary>
		public IReadOnlyCollection<DataverseBatchOperation> Operations => _operations.AsReadOnly();

		/// <summary>
		/// Adds a create operation to the change set.
		/// </summary>
		public DataverseBatchBuilder Create<TTable>(
			TTable payload,
			int? contentId = null,
			IReadOnlyDictionary<string, string>? headers = null)
			where TTable : DataverseTable
		{
			_operations.Add(DataverseBatchOperation.Create(payload, _jsonSerializerOptions, contentId, headers));
			return this;
		}

		/// <summary>
		/// Adds an update operation to the change set.
		/// </summary>
		public DataverseBatchBuilder Update<TTable>(
			DataverseKey key,
			TTable payload,
			int? contentId = null,
			IReadOnlyDictionary<string, string>? headers = null)
			where TTable : DataverseTable
		{
			_operations.Add(DataverseBatchOperation.Update(key, payload, _jsonSerializerOptions, contentId, headers));
			return this;
		}

		/// <summary>
		/// Adds an upsert operation to the change set.
		/// </summary>
		public DataverseBatchBuilder Upsert<TTable>(
			DataverseKey key,
			TTable payload,
			int? contentId = null,
			IReadOnlyDictionary<string, string>? headers = null)
			where TTable : DataverseTable
		{
			_operations.Add(DataverseBatchOperation.Upsert(key, payload, _jsonSerializerOptions, contentId, headers));
			return this;
		}

		/// <summary>
		/// Adds a delete operation to the change set.
		/// </summary>
		public DataverseBatchBuilder Delete<TTable>(
			DataverseKey key,
			int? contentId = null,
			IReadOnlyDictionary<string, string>? headers = null)
			where TTable : DataverseTable
		{
			_operations.Add(DataverseBatchOperation.Delete<TTable>(key, contentId, headers));
			return this;
		}

		/// <summary>
		/// Executes the built operations as a Dataverse atomic change set.
		/// </summary>
		public ValueTask<DataverseBatchResult> ExecuteAsync(CancellationToken cancellationToken)
		{
			return _batchService.ExecuteChangeSetAsync(_operations, cancellationToken);
		}

		IDataverseChangeSet IDataverseChangeSet.Create<TTable>(TTable payload, int? contentId, IReadOnlyDictionary<string, string>? headers)
		{
			return Create(payload, contentId, headers);
		}

		IDataverseChangeSet IDataverseChangeSet.Update<TTable>(DataverseKey key, TTable payload, int? contentId, IReadOnlyDictionary<string, string>? headers)
		{
			return Update(key, payload, contentId, headers);
		}

		IDataverseChangeSet IDataverseChangeSet.Upsert<TTable>(DataverseKey key, TTable payload, int? contentId, IReadOnlyDictionary<string, string>? headers)
		{
			return Upsert(key, payload, contentId, headers);
		}

		IDataverseChangeSet IDataverseChangeSet.Delete<TTable>(DataverseKey key, int? contentId, IReadOnlyDictionary<string, string>? headers)
		{
			return Delete<TTable>(key, contentId, headers);
		}
	}
}
