using Mavrix.Common.Dataverse.CustomAttributes;
using System.Text.Json.Serialization;

namespace Mavrix.Common.Dataverse.DTO
{
	[DataverseSetName("accounts")]
	public class Account : DataverseTable
	{
		[JsonPropertyName("accountid")]
		public override Guid? Id { get; set; }
	}
}
