using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Mavrix.Common.Dataverse.Context
{
	/// <summary>
	/// Represents a Dataverse entity with logical name, attributes, and formatted values.
	/// </summary>
	public class Entity
	{
		/// <summary>
		/// Gets or sets the unique identifier of the entity record.
		/// </summary>
		public Guid Id { get; set; }
		/// <summary>
		/// Gets or sets the logical name of the entity.
		/// </summary>
		public required string LogicalName { get; set; }
		/// <summary>
		/// Gets or sets the attribute collection for the entity.
		/// </summary>
		[JsonConverter(typeof(DataCollectionConverter))]
		public DataCollection Attributes { get; set; } = [];
		/// <summary>
		/// Gets or sets the formatted values for the entity attributes.
		/// </summary>
		[JsonConverter(typeof(DataCollectionConverter))]
		public DataCollection FormattedValues { get; set; } = [];

		/// <summary>
		/// Attempts to retrieve an attribute value cast to the specified type.
		/// </summary>
		/// <typeparam name="T">The expected type of the attribute value.</typeparam>
		/// <param name="key">The attribute logical name.</param>
		/// <param name="value">When this method returns, contains the attribute value if found and castable; otherwise default.</param>
		/// <returns><see langword="true"/> if the value exists and is of the requested type; otherwise <see langword="false"/>.</returns>
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
