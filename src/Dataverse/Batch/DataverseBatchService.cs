using Mavrix.Common.Dataverse.Clients;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Mavrix.Common.Dataverse.Batch
{
	/// <summary>
	/// Represents the parsed result of a Dataverse batch execution.
	/// </summary>
	public sealed class DataverseBatchResult
	{
		/// <summary>
		/// Gets the operation responses in server return order.
		/// </summary>
		public IReadOnlyList<DataverseBatchOperationResult> OperationResults { get; init; } = [];

		/// <summary>
		/// Gets the created entity identifier for a given content-id, if available.
		/// </summary>
		/// <param name="contentId">Content-id of the operation.</param>
		/// <returns>The entity identifier when present; otherwise <see langword="null"/>.</returns>
		public Guid? GetCreatedEntityId(int contentId)
		{
			var operationResult = OperationResults.FirstOrDefault(result => result.ContentId == contentId);
			return operationResult?.EntityId;
		}
	}

	/// <summary>
	/// Represents the parsed result of a single operation in a Dataverse batch response.
	/// </summary>
	public sealed class DataverseBatchOperationResult
	{
		/// <summary>
		/// Gets the operation content-id.
		/// </summary>
		public int? ContentId { get; init; }

		/// <summary>
		/// Gets the HTTP status code returned for the operation.
		/// </summary>
		public int StatusCode { get; init; }

		/// <summary>
		/// Gets a value indicating whether the operation succeeded.
		/// </summary>
		public bool IsSuccessStatusCode => StatusCode is >= 200 and <= 299;

		/// <summary>
		/// Gets the created/affected entity identifier from OData headers when present.
		/// </summary>
		public Guid? EntityId { get; init; }

		/// <summary>
		/// Gets the raw operation response body when present.
		/// </summary>
		public string? ResponseBody { get; init; }
	}

	/// <summary>
	/// Service for executing Dataverse batch requests with atomic change sets.
	/// </summary>
	public interface IDataverseBatchService
	{
		/// <summary>
		/// Creates a new fluent builder for composing a Dataverse change set.
		/// </summary>
		/// <example>
		/// <code>
		/// var result = await batchService
		///     .CreateChangeSet()
		///     .Update(new DataverseKey("emailaddress1", "ada@example.com"), new Contact(), contentId: 1)
		///     .Create(new Account(), contentId: 2)
		///     .ExecuteAsync(cancellationToken);
		/// </code>
		/// </example>
		/// <returns>A new change set builder instance.</returns>
		DataverseBatchBuilder CreateChangeSet();

		/// <summary>
		/// Executes write operations in a single Dataverse change set.
		/// </summary>
		/// <param name="operations">Operations to execute atomically in order.</param>
		/// <param name="cancellationToken">Cancellation token for the request.</param>
		/// <returns>Parsed operation results including content-id, status code, and created entity ids when returned.</returns>
		ValueTask<DataverseBatchResult> ExecuteChangeSetAsync(IReadOnlyCollection<DataverseBatchOperation> operations, CancellationToken cancellationToken);
	}

	/// <summary>
	/// Default implementation for Dataverse batch and change set execution.
	/// </summary>
	public sealed class DataverseBatchService : IDataverseBatchService
	{
		private const string CrLf = "\r\n";
		private readonly IDataverseHttpClient _dataverseHttpClient;
		private readonly JsonSerializerOptions _jsonSerializerOptions;
		private readonly string _apiPath;

		/// <summary>
		/// Initializes a new instance of the <see cref="DataverseBatchService"/> class.
		/// </summary>
		/// <param name="dataverseHttpClient">Dataverse HTTP client used to send the batch request.</param>
		/// <param name="jsonSerializerOptions">Shared serializer options used for fluent batch operation payloads.</param>
		public DataverseBatchService(IDataverseHttpClient dataverseHttpClient, JsonSerializerOptions jsonSerializerOptions)
		{
			ArgumentNullException.ThrowIfNull(dataverseHttpClient);
			ArgumentNullException.ThrowIfNull(jsonSerializerOptions);

			_dataverseHttpClient = dataverseHttpClient;
			_jsonSerializerOptions = jsonSerializerOptions;
			_apiPath = new Uri(_dataverseHttpClient.ApiUrl).AbsolutePath.TrimEnd('/');
		}

		/// <inheritdoc />
		public DataverseBatchBuilder CreateChangeSet()
		{
			return new DataverseBatchBuilder(this, _jsonSerializerOptions);
		}

		/// <inheritdoc />
		public async ValueTask<DataverseBatchResult> ExecuteChangeSetAsync(IReadOnlyCollection<DataverseBatchOperation> operations, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(operations);
			if (operations.Count == 0)
			{
				throw new ArgumentException("At least one operation is required to execute a change set.", nameof(operations));
			}

			var batchBoundary = $"batch_{Guid.NewGuid()}";
			var changeSetBoundary = $"changeset_{Guid.NewGuid()}";
			var payload = await BuildChangeSetPayloadAsync(batchBoundary, changeSetBoundary, operations, cancellationToken);

			var content = new StringContent(payload, Encoding.UTF8);
			content.Headers.ContentType = MediaTypeHeaderValue.Parse($"multipart/mixed; boundary=\"{batchBoundary}\"");

			using var response = await _dataverseHttpClient.ExecuteBatchAsync(content, cancellationToken);
			return await DataverseBatchResponseParser.ParseAsync(response.Content, cancellationToken);
		}

		private async ValueTask<string> BuildChangeSetPayloadAsync(string batchBoundary, string changeSetBoundary, IReadOnlyCollection<DataverseBatchOperation> operations, CancellationToken cancellationToken)
		{
			var payload = new StringBuilder();
			payload.Append("--").Append(batchBoundary).Append(CrLf);
			payload.Append("Content-Type: multipart/mixed; boundary=\"").Append(changeSetBoundary).Append('"').Append(CrLf).Append(CrLf);

			var operationIndex = 0;
			foreach (var operation in operations)
			{
				if (operation.Method == HttpMethod.Get)
				{
					throw new InvalidOperationException("GET operations are not allowed in a Dataverse change set.");
				}

				operationIndex++;
				payload.Append("--").Append(changeSetBoundary).Append(CrLf);
				payload.Append("Content-Type: application/http").Append(CrLf);
				payload.Append("Content-Transfer-Encoding: binary").Append(CrLf);

				var contentId = operation.ContentId ?? operationIndex;
				payload.Append("Content-ID: ").Append(contentId).Append(CrLf).Append(CrLf);

				payload.Append(operation.Method.Method).Append(' ').Append(ResolveOperationRequestUri(operation.Uri)).Append(" HTTP/1.1").Append(CrLf);

				foreach (var header in operation.Headers)
				{
					payload.Append(header.Key).Append(": ").Append(header.Value).Append(CrLf);
				}

				if (operation.Content is null)
				{
					payload.Append(CrLf);
					continue;
				}

				var contentType = operation.Content.Headers.ContentType?.ToString() ?? "application/json";
				payload.Append("Content-Type: ").Append(contentType).Append(CrLf).Append(CrLf);
				payload.Append(await operation.Content.ReadAsStringAsync(cancellationToken)).Append(CrLf);
			}

			payload.Append("--").Append(changeSetBoundary).Append("--").Append(CrLf);
			payload.Append("--").Append(batchBoundary).Append("--").Append(CrLf);
			return payload.ToString();
		}

		private string ResolveOperationRequestUri(string uri)
		{
			if (uri.StartsWith('$'))
			{
				return uri;
			}

			if (Uri.TryCreate(uri, UriKind.Absolute, out var absoluteUri))
			{
				return absoluteUri.PathAndQuery;
			}

			if (uri.StartsWith('/'))
			{
				return uri;
			}

			return $"{_apiPath}/{uri.TrimStart('/')}";
		}
	}
}