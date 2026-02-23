using Mavrix.Common.Dataverse.AttributeTypes;
using Mavrix.Common.Dataverse.CustomAttributes;
using System.Text.Json.Serialization;

namespace Mavrix.Common.Dataverse.DTO
{
	[DataverseSetName("contacts")]
	public class Contact : DataverseTable
	{
		[JsonPropertyName("contactid")]
		public override Guid? Id { get; set; }

		[JsonPropertyName("ParentCustomerId_account@odata.bind")]
		public Lookup<Account>? ParentCustomerIdAccount { get; set; }

		[JsonPropertyName("ParentCustomerId_contact@odata.bind")]
		public Lookup<Contact>? ParentCustomerIdContact { get; set; }

		[JsonPropertyName("AccountId@odata.bind")]
		public Lookup<Account>? AccountId { get; set; }
	}
}
