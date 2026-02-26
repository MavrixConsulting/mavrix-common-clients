using Mavrix.Common.Dataverse.Batch;
using Mavrix.Common.Dataverse.Clients;
using Mavrix.Common.Dataverse.DTO;
using NSubstitute;
using System.Net;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace Mavrix.Common.Dataverse.Tests
{
	public class DataverseBatchServiceTests
	{
		private static readonly JsonSerializerOptions JsonOptions = new();

		[Fact]
		public async Task ExecuteChangeSetAsync_Builds_Expected_Atomic_Request_Payload()
		{
			// Arrange
			var client = Substitute.For<IDataverseHttpClient>();
			client.ApiUrl.Returns("https://org.crm4.dynamics.com/api/data/v9.2");

			HttpContent? capturedBatchContent = null;
			client.ExecuteBatchAsync(Arg.Do<HttpContent>(content => capturedBatchContent = content), Arg.Any<CancellationToken>())
				.Returns(new HttpResponseMessage(HttpStatusCode.OK));

			var service = new DataverseBatchService(client, JsonOptions);

			var updateOperation = DataverseBatchOperation.Update(
				new DataverseKey("emailaddress1", "ada.o'brian@example.com"),
				new Contact
				{
					ParentCustomerIdContact = Guid.NewGuid()
				},
				JsonOptions,
				contentId: 1);

			var createOperation = DataverseBatchOperation.Create(
				new Account(),
				JsonOptions,
				contentId: 2);

			// Act
			var result = await service.ExecuteChangeSetAsync([updateOperation, createOperation], CancellationToken.None);

			// Assert
			Assert.NotNull(capturedBatchContent);
			Assert.NotNull(capturedBatchContent!.Headers.ContentType);
			Assert.Equal("multipart/mixed", capturedBatchContent.Headers.ContentType!.MediaType);

			var payload = await capturedBatchContent.ReadAsStringAsync();

			Assert.Contains("Content-Type: multipart/mixed; boundary=\"changeset_", payload);
			Assert.Contains("PATCH /api/data/v9.2/contacts(emailaddress1='ada.o''brian@example.com') HTTP/1.1", payload);
			Assert.Contains("POST /api/data/v9.2/accounts HTTP/1.1", payload);
			Assert.Contains("Content-ID: 1", payload);
			Assert.Contains("Content-ID: 2", payload);
			Assert.Contains("If-Match: *", payload);
			Assert.Contains("ParentCustomerId_contact@odata.bind", payload);
			Assert.Contains("\r\n", payload);
			Assert.Empty(result.OperationResults);
		}

		[Fact]
		public async Task ExecuteChangeSetAsync_Returns_Created_EntityIds_From_Batch_Response()
		{
			// Arrange
			var client = Substitute.For<IDataverseHttpClient>();
			client.ApiUrl.Returns("https://org.crm4.dynamics.com/api/data/v9.2");

			var accountId = Guid.NewGuid();
			var contactId = Guid.NewGuid();
			var batchResponse =
				"--batchresponse_test\r\n" +
				"Content-Type: multipart/mixed; boundary=changesetresponse_test\r\n\r\n" +
				"--changesetresponse_test\r\n" +
				"Content-Type: application/http\r\n" +
				"Content-Transfer-Encoding: binary\r\n" +
				"Content-ID: 1\r\n\r\n" +
				"HTTP/1.1 204 No Content\r\n" +
				$"OData-EntityId: https://org.crm4.dynamics.com/api/data/v9.2/accounts({accountId})\r\n\r\n" +
				"--changesetresponse_test\r\n" +
				"Content-Type: application/http\r\n" +
				"Content-Transfer-Encoding: binary\r\n" +
				"Content-ID: 2\r\n\r\n" +
				"HTTP/1.1 204 No Content\r\n" +
				$"OData-EntityId: https://org.crm4.dynamics.com/api/data/v9.2/contacts({contactId})\r\n\r\n" +
				"--changesetresponse_test--\r\n" +
				"--batchresponse_test--\r\n";

			var response = new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(batchResponse)
			};
			response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("multipart/mixed");
			response.Content.Headers.ContentType.Parameters.Add(new System.Net.Http.Headers.NameValueHeaderValue("boundary", "batchresponse_test"));

			client.ExecuteBatchAsync(Arg.Any<HttpContent>(), Arg.Any<CancellationToken>())
				.Returns(response);

			var service = new DataverseBatchService(client, JsonOptions);

			var createAccount = DataverseBatchOperation.Create(new Account(), JsonOptions, contentId: 1);
			var createContact = DataverseBatchOperation.Create(new Contact(), JsonOptions, contentId: 2);

			// Act
			var result = await service.ExecuteChangeSetAsync([createAccount, createContact], CancellationToken.None);

			// Assert
			Assert.Equal(2, result.OperationResults.Count);
			Assert.Equal(accountId, result.GetCreatedEntityId(1));
			Assert.Equal(contactId, result.GetCreatedEntityId(2));
			Assert.All(result.OperationResults, operationResult => Assert.True(operationResult.IsSuccessStatusCode));
		}

		[Fact]
		public async Task Fluent_ChangeSet_Builder_Uses_Service_For_Execution()
		{
			// Arrange
			var client = Substitute.For<IDataverseHttpClient>();
			client.ApiUrl.Returns("https://org.crm4.dynamics.com/api/data/v9.2");

			HttpContent? capturedBatchContent = null;
			client.ExecuteBatchAsync(Arg.Do<HttpContent>(content => capturedBatchContent = content), Arg.Any<CancellationToken>())
				.Returns(new HttpResponseMessage(HttpStatusCode.OK));

			var service = new DataverseBatchService(client, JsonOptions);

			// Act
			var result = await service
				.CreateChangeSet()
				.Create(new Contact { ParentCustomerIdContact = Guid.NewGuid() }, contentId: 1)
				.Create(new Account(), contentId: 2)
				.ExecuteAsync(CancellationToken.None);

			// Assert
			Assert.NotNull(capturedBatchContent);
			var payload = await capturedBatchContent!.ReadAsStringAsync();
			Assert.Contains("POST /api/data/v9.2/contacts HTTP/1.1", payload);
			Assert.Contains("POST /api/data/v9.2/accounts HTTP/1.1", payload);
			Assert.Contains("Content-ID: 1", payload);
			Assert.Contains("Content-ID: 2", payload);
			Assert.Empty(result.OperationResults);
		}

		[Fact]
		public async Task Fluent_ChangeSet_Builder_Supports_Preferred_Update_Then_Create_Pattern()
		{
			// Arrange
			var client = Substitute.For<IDataverseHttpClient>();
			client.ApiUrl.Returns("https://org.crm4.dynamics.com/api/data/v9.2");

			HttpContent? capturedBatchContent = null;
			client.ExecuteBatchAsync(Arg.Do<HttpContent>(content => capturedBatchContent = content), Arg.Any<CancellationToken>())
				.Returns(new HttpResponseMessage(HttpStatusCode.OK));

			var service = new DataverseBatchService(client, JsonOptions);

			// Act
			var result = await service
				.CreateChangeSet()
				.Update(
					new DataverseKey("emailaddress1", "ada@example.com"),
					new Contact { ParentCustomerIdContact = Guid.NewGuid() },
					contentId: 1)
				.Create(new Account(), contentId: 2)
				.ExecuteAsync(CancellationToken.None);

			// Assert
			Assert.NotNull(capturedBatchContent);
			var payload = await capturedBatchContent!.ReadAsStringAsync();
			Assert.Contains("PATCH /api/data/v9.2/contacts(emailaddress1='ada@example.com') HTTP/1.1", payload);
			Assert.Contains("POST /api/data/v9.2/accounts HTTP/1.1", payload);
			Assert.Contains("If-Match: *", payload);
			Assert.Contains("Content-ID: 1", payload);
			Assert.Contains("Content-ID: 2", payload);
			Assert.Empty(result.OperationResults);
		}

		[Fact]
		public async Task ExecuteChangeSetAsync_Throws_When_Get_Operation_Is_Present()
		{
			// Arrange
			var client = Substitute.For<IDataverseHttpClient>();
			client.ApiUrl.Returns("https://org.crm4.dynamics.com/api/data/v9.2");

			var service = new DataverseBatchService(client, JsonOptions);
			var constructor = typeof(DataverseBatchOperation).GetConstructor(
				BindingFlags.Instance | BindingFlags.NonPublic,
				binder: null,
				types:
				[
					typeof(HttpMethod),
					typeof(string),
					typeof(System.Net.Http.Json.JsonContent),
					typeof(int?),
					typeof(IReadOnlyDictionary<string, string>)
				],
				modifiers: null);

			Assert.NotNull(constructor);
			var invalidOperation = (DataverseBatchOperation?)constructor!.Invoke([HttpMethod.Get, "contacts", null, null, null]);
			Assert.NotNull(invalidOperation);

			// Act / Assert
			await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteChangeSetAsync([invalidOperation!], CancellationToken.None).AsTask());
		}

		[Fact]
		public async Task ExecuteChangeSetAsync_Throws_When_Operation_List_Is_Empty()
		{
			// Arrange
			var client = Substitute.For<IDataverseHttpClient>();
			client.ApiUrl.Returns("https://org.crm4.dynamics.com/api/data/v9.2");

			var service = new DataverseBatchService(client, JsonOptions);

			// Act / Assert
			await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteChangeSetAsync([], CancellationToken.None).AsTask());
		}
	}
}
