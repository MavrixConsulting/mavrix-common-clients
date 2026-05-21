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
		public async Task CreateChangeSet_Exposes_Inspectable_Operations_Before_Execution()
		{
			// Arrange
			var client = Substitute.For<IDataverseHttpClient>();
			client.ApiUrl.Returns("https://org.crm4.dynamics.com/api/data/v9.2");

			var service = new DataverseBatchService(client, JsonOptions);
			var contactParentId = Guid.NewGuid();

			// Act
			IDataverseChangeSet changeSet = service.CreateChangeSet();
			changeSet
				.Update(
					new DataverseKey("emailaddress1", "ada@example.com"),
					new Contact { ParentCustomerIdContact = contactParentId },
					contentId: 11,
					headers: new Dictionary<string, string> { ["x-custom-update"] = "update-header" })
				.Create(
					new Contact { ParentCustomerIdAccount = new DataverseKey("accountnumber", "ACME-001") },
					contentId: 12,
					headers: new Dictionary<string, string> { ["x-custom-create"] = "create-header" });

			// Assert
			Assert.Equal(2, changeSet.Operations.Count);

			var updateOperation = changeSet.Operations.First();
			Assert.Equal(HttpMethod.Patch, updateOperation.Method);
			Assert.Equal("contacts(emailaddress1='ada@example.com')", updateOperation.Uri);
			Assert.Equal(11, updateOperation.ContentId);
			Assert.Equal("*", updateOperation.Headers["If-Match"]);
			Assert.Equal("update-header", updateOperation.Headers["x-custom-update"]);
			Assert.NotNull(updateOperation.Content);
			var updateBody = await updateOperation.Content!.ReadAsStringAsync();
			Assert.Contains("ParentCustomerId_contact@odata.bind", updateBody);
			Assert.Contains(contactParentId.ToString(), updateBody);

			var createOperation = changeSet.Operations.Last();
			Assert.Equal(HttpMethod.Post, createOperation.Method);
			Assert.Equal("contacts", createOperation.Uri);
			Assert.Equal(12, createOperation.ContentId);
			Assert.Equal("create-header", createOperation.Headers["x-custom-create"]);
			Assert.NotNull(createOperation.Content);
			var createBody = await createOperation.Content!.ReadAsStringAsync();
			Assert.Contains("ParentCustomerId_account@odata.bind", createBody);
			Assert.Contains("ACME-001", createBody);
		}

		[Fact]
		public async Task Inspecting_Operations_Does_Not_Change_Execute_Request_Behavior()
		{
			// Arrange
			var client = Substitute.For<IDataverseHttpClient>();
			client.ApiUrl.Returns("https://org.crm4.dynamics.com/api/data/v9.2");

			HttpContent? capturedBatchContent = null;
			client.ExecuteBatchAsync(Arg.Do<HttpContent>(content => capturedBatchContent = content), Arg.Any<CancellationToken>())
				.Returns(new HttpResponseMessage(HttpStatusCode.OK));

			var service = new DataverseBatchService(client, JsonOptions);
			var parentContactId = Guid.NewGuid();

			// Act
			var changeSet = service
				.CreateChangeSet()
				.Create(new Contact { ParentCustomerIdContact = parentContactId }, contentId: 1);

			var operationBody = await ((IDataverseChangeSet)changeSet).Operations.Single().Content!.ReadAsStringAsync();
			var result = await changeSet.ExecuteAsync(CancellationToken.None);

			// Assert
			Assert.Contains(parentContactId.ToString(), operationBody);
			Assert.NotNull(capturedBatchContent);
			var payload = await capturedBatchContent!.ReadAsStringAsync();
			Assert.Contains("POST /api/data/v9.2/contacts HTTP/1.1", payload);
			Assert.Contains("ParentCustomerId_contact@odata.bind", payload);
			Assert.Contains(parentContactId.ToString(), payload);
			Assert.Empty(result.OperationResults);
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
