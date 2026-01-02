using System.Text.Json;

namespace Mavrix.Common.Dataverse.Serialization;

/// <summary>
/// Allows customization of <see cref="JsonSerializerOptions"/> used for Dataverse serialization.
/// </summary>
public interface IDataverseJsonSerializerOptionsConfigurator
{
	/// <summary>
	/// Applies configuration to the provided options instance.
	/// </summary>
	/// <param name="options">The serializer options to customize.</param>
	void Configure(JsonSerializerOptions options);
}
