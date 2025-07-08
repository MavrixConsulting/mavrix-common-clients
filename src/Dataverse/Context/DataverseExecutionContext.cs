using System.Text.Json.Serialization;

namespace Mavrix.Common.Dataverse.Context
{
	/// <summary>
	/// Represents a minimal execution context for Dataverse operations, providing essential information about the current
	/// message, stage, and entity involved in the operation.
	/// </summary>
	public class DataverseExecutionContextMinimal
	{
		public required string MessageName { get; set; }
		public int Stage { get; set; }
		public required string PrimaryEntityName { get; set; }
		public Guid PrimaryEntityId { get; set; }
		public Guid RequestId { get; set; }
		public Guid OrganizationId { get; set; }
		public Guid UserId { get; set; }
	}

	/// <summary>
	/// Represents the execution context for Dataverse operations, including additional details such as input parameters,
	/// entity images, and the parent context.
	/// </summary>
	public class DataverseExecutionContext : DataverseExecutionContextMinimal
	{
		[JsonConverter(typeof(DataCollectionConverter))]
		public DataCollection InputParameters { get; set; } = [];

		[JsonConverter(typeof(DataCollectionConverter))]
		public DataCollection PostEntityImages { get; set; } = [];

		[JsonConverter(typeof(DataCollectionConverter))]
		public DataCollection PreEntityImages { get; set; } = [];

		public DataverseExecutionContext? ParentContext { get; set; }

		[JsonConverter(typeof(MicrosoftJsonDateConverter))]
		public DateTimeOffset? OperationCreatedOn { get; set; }

		public Entity? Target => InputParameters.GetValueOrNull<Entity>("Target");
		public Entity? PreImage => PreEntityImages.GetValueOrNull<Entity>("PreImage");
		public Entity? PostImage => PostEntityImages.GetValueOrNull<Entity>("PostImage");
	}
}
