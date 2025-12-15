using Xunit;

namespace Mavrix.Common.Dataverse.Tests
{
	public class SerializerTests
	{
		[Fact]
		public void Serialized_DTO_Does_Not_Contain_Overridden_ID_Property_Name()
		{
			// Arrange
			var contact = new DTO.Contact
			{
				Id = Guid.NewGuid()
			};

			// Act
			var json = System.Text.Json.JsonSerializer.Serialize(contact);

			// Assert
			Assert.DoesNotContain("Id", json);
			Assert.Contains("contactid", json);
		}

		[Fact]
		public void Default_Dataverse_Serializer_Options_Ignore_Null_Values()
		{
			// Arrange
			var contact = new DTO.Contact
			{
				Id = null
			};

			// Act
			var json = System.Text.Json.JsonSerializer.Serialize(contact,
				Serialization.DataverseJsonSerializerOptionsFactory.Create(null, []));

			// Assert
			Assert.DoesNotContain("contactid", json);
		}
	}
}
