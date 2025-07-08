using Mavrix.Common.Dataverse.CustomAttributes;
using System.Text.Json.Serialization;

namespace Mavrix.Common.Dataverse.DTO
{
	[DataverseSetName("contacts")]
	public class Contact : DataverseTable
	{
		[JsonPropertyName("contactid")]
		public override Guid? Id { get; set; }
	}
}
