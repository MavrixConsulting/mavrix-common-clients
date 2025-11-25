using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mavrix.Common.Dataverse.Context
{
	public class DataCollectionConverter : JsonConverter<DataCollection>
	{
		public override DataCollection? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Expected start array token");

			var data = new DataCollection();

			while (reader.Read())
			{
				if (reader.TokenType == JsonTokenType.EndArray) return data;
				if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("Expected start object token");

				string key = null!;
				object? value = null;

				while (reader.Read())
				{
					if (reader.TokenType == JsonTokenType.EndObject) break;
					if (reader.TokenType == JsonTokenType.PropertyName)
					{
						var propertyName = reader.GetString();
						reader.Read();

						if (propertyName == "key")
						{
							key = reader.GetString()!;
						}
						else if (propertyName == "value")
						{
							value = ReadValue(ref reader, options);
						}
						else reader.Skip();
					}
				}

				if (key is null) throw new JsonException("Key is required");
				data[key] = value;
			}

			throw new JsonException("Expected end array token");
		}

		private static object? ReadValue(ref Utf8JsonReader reader, JsonSerializerOptions options)
		{
			#region Complex Type Handling
			if (reader.TokenType == JsonTokenType.StartObject)
			{
				using JsonDocument document = JsonDocument.ParseValue(ref reader);
				var root = document.RootElement;

				if (root.TryGetProperty("__type", out var typeProperty))
				{
					var type = typeProperty.GetString() ?? throw new JsonException("Type is required");
					type = type[..type.IndexOf(':')];
					switch (type)
					{
						case nameof(Entity):
							{
								return JsonSerializer.Deserialize<Entity>(root.GetRawText(), options);
							}
						case nameof(OptionSetValue):
							{
								return JsonSerializer.Deserialize<OptionSetValue>(root.GetRawText(), options);
							}
						case nameof(EntityReference):
							{
								return JsonSerializer.Deserialize<EntityReference>(root.GetRawText(), options);
							}
						case nameof(Money):
							{
								return JsonSerializer.Deserialize<Money>(root.GetRawText(), options);
							}
						case nameof(Relationship):
							{
								return JsonSerializer.Deserialize<Relationship>(root.GetRawText(), options);
							}
						case nameof(EntityCollection):
							{
								return JsonSerializer.Deserialize<EntityCollection>(root.GetRawText(), options);
							}
						case nameof(ColumnSet):
							{
								return JsonSerializer.Deserialize<ColumnSet>(root.GetRawText(), options);
							}
						case nameof(BooleanManagedProperty):
							{
								return JsonSerializer.Deserialize<BooleanManagedProperty>(root.GetRawText(), options);
							}
						default:
							{
								throw new JsonException($"Unexpected type: {type}");
							}
					}
				}

				var entity = JsonSerializer.Deserialize<Entity>(root.GetRawText(), options);
				if (entity is not null) return entity;

				throw new NotImplementedException($"Complex type missing __type property: {root.GetRawText()}");
			}
			#endregion

			#region Primitive Type Handling
			return reader.TokenType switch
			{
				JsonTokenType.String => HandleStringType(ref reader),
				JsonTokenType.Number => HandleNumberType(ref reader),
				JsonTokenType.True => true,
				JsonTokenType.False => false,
				JsonTokenType.Null => null,
				JsonTokenType.StartArray => HandleArrayType(ref reader, options),
				_ => throw new JsonException($"Unexpected token: {reader.TokenType}"),
			};
			#endregion
		}

		private static List<object?> HandleArrayType(ref Utf8JsonReader reader, JsonSerializerOptions options)
		{
			var list = new List<object?>();
			while (reader.Read())
			{
				if (reader.TokenType == JsonTokenType.EndArray) break;
				var value = ReadValue(ref reader, options);
				list.Add(value);
			}
			return list;
		}

		private static object? HandleNumberType(ref Utf8JsonReader reader)
		{
			if (reader.TryGetInt32(out var value)) return value;
			if (reader.TryGetDecimal(out var decimalValue)) return decimalValue;
			return reader.GetDouble();
		}

		private static object? HandleStringType(ref Utf8JsonReader reader)
		{
			if (reader.TryGetGuid(out var guidValue)) return guidValue;
			if (reader.TryGetDateTime(out var dateTimeValue)) return dateTimeValue;

			var stringValue = reader.GetString();
			if (stringValue is null) return null;

			if (CustomJsonReaders.TryGetDateTimeOffset(stringValue, out var dateTimeOffset)) return dateTimeOffset;

			return stringValue;
		}

		public override void Write(Utf8JsonWriter writer, DataCollection value, JsonSerializerOptions options)
		{
			throw new NotImplementedException();
		}
	}

	public class MicrosoftJsonDateConverter : JsonConverter<DateTimeOffset>
	{
		public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var value = reader.GetString();
			if (value is not null && value.TryGetDateTimeOffset(out var dateTimeOffset))
			{
				return dateTimeOffset;
			}
			throw new JsonException("Invalid date format");
		}

		public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
		{
			throw new NotImplementedException();
		}
	}

	internal static class CustomJsonReaders
	{
		public static bool TryGetDateTimeOffset(this string value, out DateTimeOffset dateTimeOffset)
		{
			if (value.Length > 8 && value.StartsWith("/Date(", StringComparison.InvariantCulture))
			{
				ReadOnlySpan<char> span = value.AsSpan()[6..^2];
				var offsetIndex = span.IndexOf('+');
				if (offsetIndex >= 0)
				{
					span = span[..offsetIndex];
				}

				if (long.TryParse(span, out var timestamp))
				{
					dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
					return true;
				}
			}
			dateTimeOffset = default;
			return false;
		}
	}
}
