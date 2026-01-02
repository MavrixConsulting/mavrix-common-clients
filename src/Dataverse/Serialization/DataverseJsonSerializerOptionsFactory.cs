using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Mavrix.Common.Dataverse.Serialization;

/// <summary>
/// Factory for creating <see cref="JsonSerializerOptions"/> configured for Dataverse payloads.
/// </summary>
public static class DataverseJsonSerializerOptionsFactory
{
	/// <summary>
	/// Creates serializer options with Dataverse defaults and applies custom configurators.
	/// </summary>
	/// <param name="configureSerializer">Optional callback to customize the options.</param>
	/// <param name="configurators">Registered configurators to apply sequentially.</param>
	/// <returns>A configured <see cref="JsonSerializerOptions"/> instance.</returns>
	public static JsonSerializerOptions Create(Action<JsonSerializerOptions>? configureSerializer,
		IEnumerable<IDataverseJsonSerializerOptionsConfigurator> configurators)
	{
		var options = CreateBase();

		configureSerializer?.Invoke(options);

		foreach (var configurator in configurators)
		{
			configurator.Configure(options);
		}

		return options;
	}

	private static JsonSerializerOptions CreateBase()
	{
		return new JsonSerializerOptions
		{
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			TypeInfoResolver = new DefaultJsonTypeInfoResolver
			{
				Modifiers =
				{
					typeInfo =>
					{
						if (typeInfo.Kind != JsonTypeInfoKind.Object) return;
						foreach (var property in typeInfo.Properties)
						{
							if (property.PropertyType == typeof(string)) continue;
							if (typeof(ICollection).IsAssignableFrom(property.PropertyType))
							{
								var existing = property.ShouldSerialize;
								property.ShouldSerialize = (obj, value) =>
								{
									if (existing is not null && !existing(obj, value)) return false;
									if (value is null) return false; // ignore null collections
									return ((ICollection)value).Count > 0; // ignore empty collections
								};
							}
						}
					}
				}
			}
		};
	}
}
