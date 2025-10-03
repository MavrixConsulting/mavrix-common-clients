using System.Text.Json;

namespace Mavrix.Common.Dataverse.Serialization;

public interface IDataverseJsonSerializerOptionsConfigurator
{
	void Configure(JsonSerializerOptions options);
}
