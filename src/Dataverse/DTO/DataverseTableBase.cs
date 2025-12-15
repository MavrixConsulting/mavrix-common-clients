using System.Text.Json.Serialization;

namespace Mavrix.Common.Dataverse.DTO
{
	public abstract class DataverseTable
	{
		[JsonIgnore]
		public abstract Guid? Id { get; set; }
	}
}
