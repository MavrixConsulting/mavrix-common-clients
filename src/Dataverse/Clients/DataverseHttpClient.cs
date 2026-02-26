using Mavrix.Common.Dataverse.AuthenticationTokenProvider;
using Mavrix.Common.Dataverse.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Mavrix.Common.Dataverse.Clients
{
	/// <summary>
	/// Abstraction for sending HTTP requests to Dataverse endpoints.
	/// </summary>
	public interface IDataverseHttpClient
	{
		/// <summary>
		/// Creates a Dataverse record and returns its identifier.
		/// </summary>
		/// <param name="uri">Relative resource path (entity set and optional query).</param>
		/// <param name="content">HTTP content to post.</param>
		/// <param name="cancellationToken">Token to cancel the operation.</param>
		/// <returns>The created record identifier if returned.</returns>
		ValueTask<Guid?> CreateAsync(string uri, HttpContent content, CancellationToken cancellationToken);
		/// <summary>
		/// Retrieves a single resource.
		/// </summary>
		/// <typeparam name="T">Type to deserialize the response into.</typeparam>
		/// <param name="uri">Relative resource path.</param>
		/// <param name="cancellationToken">Token to cancel the operation.</param>
		/// <param name="includeAnnotations">Whether to request OData annotations.</param>
		/// <returns>The deserialized resource or <see langword="null"/>.</returns>
		ValueTask<T?> GetAsync<T>(string uri, CancellationToken cancellationToken, bool includeAnnotations = false);
		/// <summary>
		/// Streams a paged list of resources.
		/// </summary>
		/// <typeparam name="T">Type to deserialize each item into.</typeparam>
		/// <param name="uri">Relative resource path.</param>
		/// <param name="cancellationToken">Token to cancel the operation.</param>
		/// <param name="includeAnnotations">Whether to request OData annotations.</param>
		/// <returns>An async sequence of deserialized items.</returns>
		IAsyncEnumerable<T> GetListAsync<T>(string uri, CancellationToken cancellationToken, bool includeAnnotations = false);
		/// <summary>
		/// Updates an existing record.
		/// </summary>
		/// <param name="uri">Relative resource path.</param>
		/// <param name="content">HTTP content to patch.</param>
		/// <param name="cancellationToken">Token to cancel the operation.</param>
		ValueTask UpdateAsync(string uri, HttpContent content, CancellationToken cancellationToken);
		/// <summary>
		/// Creates or updates a record using PUT semantics.
		/// </summary>
		/// <param name="uri">Relative resource path.</param>
		/// <param name="content">HTTP content to put.</param>
		/// <param name="cancellationToken">Token to cancel the operation.</param>
		ValueTask UpsertAsync(string uri, HttpContent content, CancellationToken cancellationToken);
		/// <summary>
		/// Deletes a record, ignoring not found responses.
		/// </summary>
		/// <param name="uri">Relative resource path.</param>
		/// <param name="cancellationToken">Token to cancel the operation.</param>
		ValueTask DeleteAsync(string uri, CancellationToken cancellationToken);
		/// <summary>
		/// Executes a Dataverse <c>$batch</c> request.
		/// </summary>
		/// <param name="content">Multipart batch request content.</param>
		/// <param name="cancellationToken">Token to cancel the operation.</param>
		/// <returns>The batch response message.</returns>
		ValueTask<HttpResponseMessage> ExecuteBatchAsync(HttpContent content, CancellationToken cancellationToken);
		/// <summary>
		/// Gets the base API URL used by the client.
		/// </summary>
		string ApiUrl { get; }
	}

	/// <summary>
	/// HTTP client wrapper for interacting with Dataverse APIs including authentication, paging, and error handling.
	/// </summary>
	public class DataverseHttpClient : IDataverseHttpClient
	{
		private readonly ILogger<DataverseHttpClient> _logger;

		private readonly HttpClient _httpClient;

		private readonly DataverseOptions _dataverseOptions;

		private readonly IAzureTokenProvider _tokenProvider;

		private readonly string Scope;
		private readonly string ApiUrl;

		/// <inheritdoc />
		string IDataverseHttpClient.ApiUrl => ApiUrl;

		/// <summary>
		/// Initializes a new instance of <see cref="DataverseHttpClient"/>.
		/// </summary>
		/// <param name="logger">Logger for diagnostics.</param>
		/// <param name="httpClient">HTTP client instance.</param>
		/// <param name="options">Dataverse options containing base URL.</param>
		/// <param name="tokenProvider">Token provider for acquiring access tokens.</param>
		public DataverseHttpClient(ILogger<DataverseHttpClient> logger, HttpClient httpClient, IOptions<DataverseOptions> options, IAzureTokenProvider tokenProvider)
		{
			_logger = logger;
			_httpClient = httpClient;
			_dataverseOptions = options.Value;
			_tokenProvider = tokenProvider;

			var baseUrl = _dataverseOptions.BaseUrl.TrimEnd('/');
			Scope = $"{baseUrl}/.default";
			ApiUrl = $"{baseUrl}/api/data/v9.2";
		}

		/// <summary>
		/// Header for requesting all annotations from Dataverse responses.
		/// </summary>
		internal static readonly KeyValuePair<string, string> IncludeAnnotations = new("Prefer", "odata.include-annotations=\"*\"");

		/// <inheritdoc />
		public async ValueTask<Guid?> CreateAsync(string uri, HttpContent content, CancellationToken cancellationToken)
		{
			var requestUri = new Uri($"{ApiUrl}/{uri}");

			var response = await SendAsync(HttpMethod.Post, requestUri, content, [], cancellationToken);

			return TryParseEntityId(response);
		}

		/// <inheritdoc />
		public async ValueTask<T?> GetAsync<T>(string uri, CancellationToken cancellationToken, bool includeAnnotations = false)
		{
			var requestUri = new Uri($"{ApiUrl}/{uri}");

			KeyValuePair<string, string>[] headers = includeAnnotations ? [IncludeAnnotations] : [];

			var response = await SendAsync(HttpMethod.Get, requestUri, null, headers, cancellationToken);

			return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
		}

		/// <inheritdoc />
		public async IAsyncEnumerable<T> GetListAsync<T>(string uri, [EnumeratorCancellation] CancellationToken cancellationToken, bool includeAnnotations = false)
		{
			Uri? requestUri = new($"{ApiUrl}/{uri}");

			KeyValuePair<string, string>[] headers = includeAnnotations ? [IncludeAnnotations] : [];

			while (requestUri is not null)
			{
				var response = await SendAsync(HttpMethod.Get, requestUri, null, headers, cancellationToken);
				var result = await response.Content.ReadFromJsonAsync<ODataResponse<T>>(cancellationToken);
				if (result is null)
				{
					yield break;
				}
				foreach (var item in result.Values)
				{
					yield return item;
				}

				requestUri = string.IsNullOrEmpty(result.NextLink) ? null : new Uri(result.NextLink);
			}
		}

		/// <inheritdoc />
		public async ValueTask UpdateAsync(string uri, HttpContent content, CancellationToken cancellationToken)
		{
			var requestUri = new Uri($"{ApiUrl}/{uri}");

			KeyValuePair<string, string>[] headers = [new("If-Match", "*")];

			await SendAsync(HttpMethod.Patch, requestUri, content, headers, cancellationToken);
		}

		/// <inheritdoc />
		public async ValueTask UpsertAsync(string uri, HttpContent content, CancellationToken cancellationToken)
		{
			var requestUri = new Uri($"{ApiUrl}/{uri}");

			await SendAsync(HttpMethod.Put, requestUri, content, [], cancellationToken);
		}

		/// <inheritdoc />
		public async ValueTask DeleteAsync(string uri, CancellationToken cancellationToken)
		{
			var requestUri = new Uri($"{ApiUrl}/{uri}");

			try
			{
				await SendAsync(HttpMethod.Delete, requestUri, null, [], cancellationToken);
			}
			catch (DataverseException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
			{
				return;
			}
		}

		/// <inheritdoc />
		public async ValueTask<HttpResponseMessage> ExecuteBatchAsync(HttpContent content, CancellationToken cancellationToken)
		{
			var requestUri = new Uri($"{ApiUrl}/$batch");

			var request = new HttpRequestMessage
			{
				Method = HttpMethod.Post,
				RequestUri = requestUri,
				Content = content
			};

			request.Headers.Add("OData-Version", "4.0");
			request.Headers.Add("OData-MaxVersion", "4.0");

			await SetAuthorizationHeader(request, cancellationToken);

			var response = await _httpClient.SendAsync(request, cancellationToken);

			if (response.IsSuccessStatusCode)
			{
				return response;
			}

			var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

			_logger.LogError("Dataverse batch request failed. Statuscode: {Statuscode}, Message: {Message}", response.StatusCode, errorContent);
			throw new DataverseHttpClientException(response.StatusCode, errorContent);
		}

		/// <summary>
		/// Sends an HTTP request with authorization and custom headers, handling Dataverse error responses.
		/// </summary>
		/// <param name="method">HTTP method.</param>
		/// <param name="requestUri">Absolute request URI.</param>
		/// <param name="content">Optional request content.</param>
		/// <param name="headers">Additional headers to include.</param>
		/// <param name="cancellationToken">Token to cancel the operation.</param>
		/// <returns>The HTTP response message.</returns>
		/// <exception cref="DataverseException">Thrown when Dataverse returns an error payload.</exception>
		/// <exception cref="DataverseHttpClientException">Thrown when an error occurs without a Dataverse payload.</exception>
		public async ValueTask<HttpResponseMessage> SendAsync(HttpMethod method, Uri requestUri, HttpContent? content, KeyValuePair<string, string>[] headers, CancellationToken cancellationToken)
		{
			var request = new HttpRequestMessage()
			{
				Method = method,
				RequestUri = requestUri,
				Content = content
			};

			foreach (var header in headers)
			{
				request.Headers.Add(header.Key, header.Value);
			}

			await SetAuthorizationHeader(request, cancellationToken);

			var response = await _httpClient.SendAsync(request, cancellationToken);

			if (response.IsSuccessStatusCode)
			{
				return response;
			}

			var errorMessage = await response.Content.ReadFromJsonAsync<ErrorMessage>(cancellationToken);

			if (errorMessage is not null)
			{
				_logger.LogError("Dataverse error. Statuscode: {Statuscode}, Code: {Code}, Message: {Message}", response.StatusCode, errorMessage.Error?.Code, errorMessage.Error?.Message);
				throw new DataverseException(response.StatusCode, errorMessage.Error?.Code, errorMessage.Error?.Message);
			}

			var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

			_logger.LogError("Dataverse http client error. Statuscode: {Statuscode}, Message: {Message}", response.StatusCode, errorContent);
			throw new DataverseHttpClientException(response.StatusCode, errorContent);
		}

		/// <summary>
		/// Adds an authorization header using the configured token provider.
		/// </summary>
		/// <param name="request">The request to modify.</param>
		/// <param name="cancellationToken">Token to cancel the operation.</param>
		/// <returns>The modified request.</returns>
		private async ValueTask<HttpRequestMessage> SetAuthorizationHeader(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var token = await _tokenProvider.GetTokenAsync(Scope, cancellationToken);
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
			return request;
		}

		/// <summary>
		/// Attempts to extract an entity identifier from the Dataverse response header.
		/// </summary>
		/// <param name="response">The HTTP response.</param>
		/// <returns>The parsed identifier if present; otherwise <see langword="null"/>.</returns>
		private static Guid? TryParseEntityId(HttpResponseMessage response)
		{
			if (response.Headers.TryGetValues("OData-EntityId", out var values))
			{
				var resourceUri = values.FirstOrDefault().AsSpan();

				var start = resourceUri.IndexOf('(') + 1;
				var slice = resourceUri[start..];
				var end = slice.IndexOf(')');

				if (Guid.TryParse(slice[..end], out var guid))
				{
					return guid;
				}
			}

			return default;
		}

		/// <summary>
		/// Represents a paged OData response from Dataverse.
		/// </summary>
		public class ODataResponse<T>
		{
			/// <summary>
			/// Gets or sets the OData context URI.
			/// </summary>
			[JsonPropertyName("@odata.context")]
			public string? Context { get; set; }
			/// <summary>
			/// Gets or sets the total count when requested.
			/// </summary>
			[JsonPropertyName("@odata.count")]
			public int? Count { get; set; }
			/// <summary>
			/// Gets or sets the returned entities.
			/// </summary>
			[JsonPropertyName("value")]
			public List<T> Values { get; set; } = [];
			/// <summary>
			/// Gets or sets the next page link when more data is available.
			/// </summary>
			[JsonPropertyName("@odata.nextLink")]
			public string? NextLink { get; set; }
		}

		/// <summary>
		/// Represents a Dataverse error response payload.
		/// </summary>
		public class ErrorMessage
		{
			/// <summary>
			/// Gets or sets the error content.
			/// </summary>
			[JsonPropertyName("error")]
			public ErrorContent? Error { get; set; }
			/// <summary>
			/// Dataverse error detail.
			/// </summary>
			public class ErrorContent
			{
				/// <summary>
				/// Gets or sets the error code.
				/// </summary>
				[JsonPropertyName("code")]
				public string? Code { get; set; }
				/// <summary>
				/// Gets or sets the error message.
				/// </summary>
				[JsonPropertyName("message")]
				public string? Message { get; set; }
			}
		}

		/// <summary>
		/// Exception representing a Dataverse error payload.
		/// </summary>
		public class DataverseException(HttpStatusCode statusCode, string? errorCode, string? errorMessage) : Exception(errorMessage)
		{
			/// <summary>
			/// Gets or sets the HTTP status code.
			/// </summary>
			public HttpStatusCode StatusCode { get; set; } = statusCode;
			/// <summary>
			/// Gets or sets the Dataverse error code.
			/// </summary>
			public string? ErrorCode { get; set; } = errorCode;
			/// <summary>
			/// Gets or sets the Dataverse error message.
			/// </summary>
			public string? ErrorMessage { get; set; } = errorMessage;
		}

		/// <summary>
		/// Exception representing an HTTP error without a Dataverse payload.
		/// </summary>
		public class DataverseHttpClientException(HttpStatusCode statusCode, string message) : Exception(message)
		{
			/// <summary>
			/// Gets or sets the HTTP status code.
			/// </summary>
			public HttpStatusCode HttpStatusCode { get; set; } = statusCode;
		}
	}
}
