using Mavrix.Common.Dataverse.AttributeTypes;
using Mavrix.Common.Dataverse.DTO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mavrix.Common.Dataverse.Serialization
{
	internal sealed class DataverseLookupJsonConverterFactory : JsonConverterFactory
	{
		public override bool CanConvert(Type typeToConvert)
		{
			return typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Lookup<>);
		}

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			var entityType = typeToConvert.GetGenericArguments()[0];
			var converterType = typeof(DataverseLookupJsonConverter<>).MakeGenericType(entityType);
			return (JsonConverter)Activator.CreateInstance(converterType)!;
		}
	}

	internal sealed class DataverseLookupJsonConverter<T> : JsonConverter<Lookup<T>> where T : DataverseTable
	{
		public override Lookup<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			throw new NotSupportedException("Lookup deserialization is not supported for create/update attributes.");
		}

		public override void Write(Utf8JsonWriter writer, Lookup<T> value, JsonSerializerOptions options)
		{
			writer.WriteStringValue(value.ToODataBind());
		}
	}
}
