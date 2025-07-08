using Mavrix.Common.Dataverse.DTO;
using Mavrix.Common.Dataverse.QueryBuilder;

namespace Mavrix.Common.Dataverse.Repositories
{
	public interface IDataverseRepository<T> where T : DataverseTable
	{
		ValueTask AssociateLinkEntityAsync(Guid sourceKey, string relationshipName, string targetSetName, Guid targetKey, CancellationToken cancellationToken);
		ValueTask<Guid?> CreateAsync(T record, CancellationToken cancellationToken);
		ValueTask DeleteAsync(Guid key, CancellationToken cancellationToken);
		ValueTask DisassociateLinkEntityAsync(Guid sourceKey, string relationshipName, Guid targetKey, CancellationToken cancellationToken);
		ValueTask<T?> GetAsync(Guid key, DataverseQueryBuilder queryBuilder, CancellationToken cancellationToken);
		IAsyncEnumerable<T> GetListAsync(DataverseQueryBuilder queryBuilder, CancellationToken cancellationToken);
		ValueTask UpdateAsync(Guid key, T record, CancellationToken cancellationToken);
		ValueTask UpsertAsync(Guid key, T record, CancellationToken cancellationToken);
	}
}
