using System.Text.Json.Serialization;

namespace Mavrix.Common.Dataverse.DTO
{
	/// <summary>
	/// Base type for Dataverse table DTOs providing a consistent identifier contract.
	/// </summary>
	public abstract class DataverseTable
	{
		/// <summary>
		/// Gets or sets the unique identifier of the table record.
		/// </summary>
		[JsonIgnore]
		public abstract Guid? Id { get; set; }
	}
}
