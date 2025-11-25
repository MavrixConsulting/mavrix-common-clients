using System.Runtime.Serialization;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
namespace Mavrix.Common.Dataverse.Context
{
	public class OptionSetValue
	{
		public int Value { get; set; }
	}

	public class EntityReference
	{
		public Guid Id { get; set; }
		public required string LogicalName { get; set; }
		public string? Name { get; set; }
	}

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
	public class EntityCollection
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
	{
		public required string EntityName { get; set; }
		public List<Entity> Entities { get; set; } = [];
	}

	public class Money
	{
		public decimal Value { get; set; }
	}

	public class Relationship
	{
		public enum EntityRole
		{
			[EnumMember]
			Referencing = 1,
			[EnumMember]
			Referenced = 2
		}

		public EntityRole? PrimaryEntityRole { get; set; }

		public required string SchemaName { get; set; }
	}

	public class ColumnSet
	{
		public bool AllColumns { get; set; }
		public string[]? AttributeExpressions { get; set; }
		public string[]? Columns { get; set; } = [];
	}

	public class BooleanManagedProperty
	{
		public bool CanBeChanged { get; set; }
		public string? ManagedPropertyLogicalName { get; set; }
		public bool Value { get; set; }
	}
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member