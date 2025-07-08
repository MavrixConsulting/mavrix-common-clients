using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Mavrix.Common.Dataverse.Context
{
	public class Entity
	{
		public Guid Id { get; set; }
		public required string LogicalName { get; set; }
		[JsonConverter(typeof(DataCollectionConverter))]
		public DataCollection Attributes { get; set; } = [];
		[JsonConverter(typeof(DataCollectionConverter))]
		public DataCollection FormattedValues { get; set; } = [];

		public bool TryGetAttributeValue<T>(string key, [MaybeNullWhen(false)] out T value)
		{
			ArgumentNullException.ThrowIfNull(key);

			if (Attributes.TryGetValue(key, out var obj) && obj is T typed)
			{
				value = typed;
				return true;
			}

			value = default;
			return false;
		}
	}
}
