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
			Assert.DoesNotContain("\"Id\":", json);
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

		[Fact]
		public void Lookup_Field_Serializes_To_OData_Bind_Value()
		{
			// Arrange
			var accountId = Guid.NewGuid();
			var contact = new DTO.Contact
			{
				AccountId = accountId
			};

			// Act
			var json = System.Text.Json.JsonSerializer.Serialize(contact,
				Serialization.DataverseJsonSerializerOptionsFactory.Create(null, []));

			// Assert
			Assert.Contains("\"AccountId@odata.bind\":\"/accounts", json);
			Assert.Contains(accountId.ToString(), json);
		}

		[Fact]
		public void Lookup_Field_Serializes_To_OData_Bind_Value_With_Nullable_Guid_Constructor()
		{
			// Arrange
			Guid? accountId = Guid.NewGuid();
			var contact = new DTO.Contact
			{
				AccountId = new(accountId)
			};

			// Act
			var json = System.Text.Json.JsonSerializer.Serialize(contact,
				Serialization.DataverseJsonSerializerOptionsFactory.Create(null, []));

			// Assert
			using var document = System.Text.Json.JsonDocument.Parse(json);
			Assert.True(document.RootElement.TryGetProperty("AccountId@odata.bind", out var property));
			Assert.Equal($"/accounts({accountId})", property.GetString());
		}

		[Fact]
		public void Lookup_Field_Serializes_To_OData_Bind_Value_With_Alternate_Key()
		{
			// Arrange
			const string accountNumber = "ACC-001";
			var contact = new DTO.Contact
			{
				AccountId = new("AccountNumber", accountNumber)
			};

			// Act
			var json = System.Text.Json.JsonSerializer.Serialize(contact,
				Serialization.DataverseJsonSerializerOptionsFactory.Create(null, []));

			// Assert
			using var document = System.Text.Json.JsonDocument.Parse(json);
			Assert.True(document.RootElement.TryGetProperty("AccountId@odata.bind", out var property));
			Assert.Equal("/accounts(AccountNumber='ACC-001')", property.GetString());
		}

		[Fact]
		public void Lookup_Field_Serializes_To_OData_Bind_Value_With_Alternate_Key_Escaped_Single_Quote()
		{
			// Arrange
			const string accountNumber = "O'Brian";
			var contact = new DTO.Contact
			{
				AccountId = new("AccountNumber", accountNumber)
			};

			// Act
			var json = System.Text.Json.JsonSerializer.Serialize(contact,
				Serialization.DataverseJsonSerializerOptionsFactory.Create(null, []));

			// Assert
			using var document = System.Text.Json.JsonDocument.Parse(json);
			Assert.True(document.RootElement.TryGetProperty("AccountId@odata.bind", out var property));
			Assert.Equal("/accounts(AccountNumber='O''Brian')", property.GetString());
		}

		[Fact]
		public void Null_Lookup_Field_Is_Omitted_With_Default_Dataverse_Serializer_Options()
		{
			// Arrange
			var contact = new DTO.Contact
			{
				AccountId = null
			};

			// Act
			var json = System.Text.Json.JsonSerializer.Serialize(contact,
				Serialization.DataverseJsonSerializerOptionsFactory.Create(null, []));

			// Assert
			Assert.DoesNotContain("AccountId@odata.bind", json);
		}

		[Fact]
		public void ParentCustomerId_Account_Binding_Serializes_To_Account_Navigation_Property()
		{
			// Arrange
			var accountId = Guid.NewGuid();
			var contact = new DTO.Contact
			{
				ParentCustomerIdAccount = accountId
			};

			// Act
			var json = System.Text.Json.JsonSerializer.Serialize(contact,
				Serialization.DataverseJsonSerializerOptionsFactory.Create(null, []));

			// Assert
			Assert.Contains("ParentCustomerId_account@odata.bind", json);
			Assert.DoesNotContain("ParentCustomerId_contact@odata.bind", json);
		}

		[Fact]
		public void ParentCustomerId_Contact_Binding_Serializes_To_Contact_Navigation_Property()
		{
			// Arrange
			var parentContactId = Guid.NewGuid();
			var contact = new DTO.Contact
			{
				ParentCustomerIdContact = parentContactId
			};

			// Act
			var json = System.Text.Json.JsonSerializer.Serialize(contact,
				Serialization.DataverseJsonSerializerOptionsFactory.Create(null, []));

			// Assert
			Assert.Contains("ParentCustomerId_contact@odata.bind", json);
			Assert.DoesNotContain("ParentCustomerId_account@odata.bind", json);
		}
	}
}
