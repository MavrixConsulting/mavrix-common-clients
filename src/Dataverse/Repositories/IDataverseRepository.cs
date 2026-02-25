using Mavrix.Common.Dataverse.DTO;
using Mavrix.Common.Dataverse.QueryBuilder;

namespace Mavrix.Common.Dataverse.Repositories
{
	/// <summary>
	/// Defines CRUD and relationship operations for Dataverse entity sets.
	/// </summary>
	public interface IDataverseRepository<T> where T : DataverseTable
	{
		/// <summary>
		/// Creates a relationship between the source record and a target record in another set.
		/// </summary>
		/// <param name="sourceKey">Primary key of the source record.</param>
		/// <param name="relationshipName">Relationship logical name to traverse.</param>
		/// <param name="targetSetName">Target set logical name.</param>
		/// <param name="targetKey">Primary key of the target record.</param>
		/// <param name="cancellationToken">Cancellation token for the request.</param>
		ValueTask AssociateLinkEntityAsync(Guid sourceKey, string relationshipName, string targetSetName, Guid targetKey, CancellationToken cancellationToken);
		/// <summary>
		/// Creates a Dataverse record of type <typeparamref name="T"/>.
		/// </summary>
		/// <param name="record">Record payload to persist.</param>
		/// <param name="cancellationToken">Cancellation token for the request.</param>
		/// <returns>The identifier of the newly created record, when returned by Dataverse.</returns>
		ValueTask<Guid?> CreateAsync(T record, CancellationToken cancellationToken);
		/// <summary>
		/// Deletes a record from the Dataverse set.
		/// </summary>
		/// <param name="key">Primary key of the record to remove.</param>
		/// <param name="cancellationToken">Cancellation token for the request.</param>
		ValueTask DeleteAsync(Guid key, CancellationToken cancellationToken);
		/// <summary>
		/// Deletes a record from the Dataverse set.
		/// </summary>
		/// <param name="key">Dataverse key expression identifying the record to remove.</param>
		/// <param name="cancellationToken">Cancellation token for the request.</param>
		ValueTask DeleteAsync(DataverseKey key, CancellationToken cancellationToken);
		/// <summary>
		/// Removes the relationship between the source record and the specified target record.
		/// </summary>
		/// <param name="sourceKey">Primary key of the source record.</param>
		/// <param name="relationshipName">Relationship logical name.</param>
		/// <param name="targetKey">Primary key of the target record.</param>
		/// <param name="cancellationToken">Cancellation token for the request.</param>
		ValueTask DisassociateLinkEntityAsync(Guid sourceKey, string relationshipName, Guid targetKey, CancellationToken cancellationToken);
		/// <summary>
		/// Retrieves a single record by key using the provided query builder.
		/// </summary>
		/// <param name="key">Primary key identifying the record.</param>
		/// <param name="queryBuilder">Builder describing select, expand, or annotation options.</param>
		/// <param name="cancellationToken">Cancellation token for the request.</param>
		/// <returns>The matching record or <c>null</c> when not found.</returns>
		ValueTask<T?> GetAsync(Guid key, DataverseQueryBuilder queryBuilder, CancellationToken cancellationToken);
		/// <summary>
		/// Retrieves a single record by key using the provided query builder.
		/// </summary>
		/// <param name="key">Dataverse key expression identifying the record.</param>
		/// <param name="queryBuilder">Builder describing select, expand, or annotation options.</param>
		/// <param name="cancellationToken">Cancellation token for the request.</param>
		/// <returns>The matching record or <c>null</c> when not found.</returns>
		ValueTask<T?> GetAsync(DataverseKey key, DataverseQueryBuilder queryBuilder, CancellationToken cancellationToken);
		/// <summary>
		/// Streams records that satisfy the provided query definition.
		/// </summary>
		/// <param name="queryBuilder">Builder describing filters, selects, and pagination.</param>
		/// <param name="cancellationToken">Cancellation token for the request.</param>
		/// <returns>An async enumerable yielding each retrieved record.</returns>
		IAsyncEnumerable<T> GetListAsync(DataverseQueryBuilder queryBuilder, CancellationToken cancellationToken);
		/// <summary>
		/// Updates an existing record identified by the supplied key.
		/// </summary>
		/// <param name="key">Primary key of the record to update.</param>
		/// <param name="record">Payload containing the updated fields.</param>
		/// <param name="cancellationToken">Cancellation token for the request.</param>
		ValueTask UpdateAsync(Guid key, T record, CancellationToken cancellationToken);
		/// <summary>
		/// Updates an existing record identified by the supplied key.
		/// </summary>
		/// <param name="key">Dataverse key expression of the record to update.</param>
		/// <param name="record">Payload containing the updated fields.</param>
		/// <param name="cancellationToken">Cancellation token for the request.</param>
		ValueTask UpdateAsync(DataverseKey key, T record, CancellationToken cancellationToken);
		/// <summary>
		/// Upserts a record, creating or updating it based on the provided key.
		/// </summary>
		/// <param name="key">Primary key used for the upsert target.</param>
		/// <param name="record">Payload to create or update.</param>
		/// <param name="cancellationToken">Cancellation token for the request.</param>
		ValueTask UpsertAsync(Guid key, T record, CancellationToken cancellationToken);
		/// <summary>
		/// Upserts a record, creating or updating it based on the provided key.
		/// </summary>
		/// <param name="key">Dataverse key expression used for the upsert target.</param>
		/// <param name="record">Payload to create or update.</param>
		/// <param name="cancellationToken">Cancellation token for the request.</param>
		ValueTask UpsertAsync(DataverseKey key, T record, CancellationToken cancellationToken);
		/// <summary>
		/// Removes the reference value from a lookup property on the specified record.
		/// </summary>
		/// <param name="key">Primary key of the record containing the reference.</param>
		/// <param name="propertyName">Lookup property name.</param>
		/// <param name="cancellationToken">Cancellation token for the request.</param>
		ValueTask RemoveReferenceValueAsync(Guid key, string propertyName, CancellationToken cancellationToken);
	}
}
