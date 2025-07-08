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
	public interface IDataverseHttpClient
	{
		ValueTask<Guid?> CreateAsync(string uri, HttpContent content, CancellationToken cancellationToken);
		ValueTask<T?> GetAsync<T>(string uri, CancellationToken cancellationToken, bool includeAnnotations = false);
		IAsyncEnumerable<T> GetListAsync<T>(string uri, CancellationToken cancellationToken, bool includeAnnotations = false);
		ValueTask UpdateAsync(string uri, HttpContent content, CancellationToken cancellationToken);
		ValueTask UpsertAsync(string uri, HttpContent content, CancellationToken cancellationToken);
		ValueTask DeleteAsync(string uri, CancellationToken cancellationToken);
		string ApiUrl { get; }
	}

	public class DataverseHttpClient : IDataverseHttpClient
	{
		private readonly ILogger<DataverseHttpClient> _logger;

		private readonly HttpClient _httpClient;

		private readonly DataverseOptions _dataverseOptions;

		private readonly ITokenProvider _tokenProvider;

		private readonly string Scope;
		private readonly string ApiUrl;

		string IDataverseHttpClient.ApiUrl => ApiUrl;

		public DataverseHttpClient(ILogger<DataverseHttpClient> logger, HttpClient httpClient, IOptions<DataverseOptions> options, ITokenProvider tokenProvider)
		{
			_logger = logger;
			_httpClient = httpClient;
			_dataverseOptions = options.Value;
			_tokenProvider = tokenProvider;

			Scope = $"{_dataverseOptions.BaseUrl}/.default";
			ApiUrl = $"{_dataverseOptions.BaseUrl}/api/data/v9.2";
		}

		internal static readonly KeyValuePair<string, string> IncludeAnnotations = new("Prefer", "odata.include-annotations=\"*\"");

		public async ValueTask<Guid?> CreateAsync(string uri, HttpContent content, CancellationToken cancellationToken)
		{
			var requestUri = new Uri($"{ApiUrl}/{uri}");

			var response = await SendAsync(HttpMethod.Post, requestUri, content, [], cancellationToken);

			return TryParseEntityId(response);
		}

		public async ValueTask<T?> GetAsync<T>(string uri, CancellationToken cancellationToken, bool includeAnnotations = false)
		{
			var requestUri = new Uri($"{ApiUrl}/{uri}");

			KeyValuePair<string, string>[] headers = includeAnnotations ? [IncludeAnnotations] : [];

			var response = await SendAsync(HttpMethod.Get, requestUri, null, headers, cancellationToken);

			return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
		}

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

		public async ValueTask UpdateAsync(string uri, HttpContent content, CancellationToken cancellationToken)
		{
			var requestUri = new Uri($"{ApiUrl}/{uri}");

			KeyValuePair<string, string>[] headers = [new("If-Match", "*")];

			await SendAsync(HttpMethod.Patch, requestUri, content, headers, cancellationToken);
		}

		public async ValueTask UpsertAsync(string uri, HttpContent content, CancellationToken cancellationToken)
		{
			var requestUri = new Uri($"{ApiUrl}/{uri}");

			await SendAsync(HttpMethod.Put, requestUri, content, [], cancellationToken);
		}

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

		private async ValueTask<HttpRequestMessage> SetAuthorizationHeader(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var token = await _tokenProvider.GetTokenAsync(Scope, cancellationToken);
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
			return request;
		}

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

		public class ODataResponse<T>
		{
			[JsonPropertyName("@odata.context")]
			public string? Context { get; set; }
			[JsonPropertyName("@odata.count")]
			public int? Count { get; set; }
			[JsonPropertyName("value")]
			public List<T> Values { get; set; } = [];
			[JsonPropertyName("@odata.nextLink")]
			public string? NextLink { get; set; }
		}

		public class ErrorMessage
		{
			[JsonPropertyName("error")]
			public ErrorContent? Error { get; set; }
			public class ErrorContent
			{
				[JsonPropertyName("code")]
				public string? Code { get; set; }
				[JsonPropertyName("message")]
				public string? Message { get; set; }
			}
		}

		public class DataverseException(HttpStatusCode statusCode, string? errorCode, string? errorMessage) : Exception(errorMessage)
		{
			public HttpStatusCode StatusCode { get; set; } = statusCode;
			public string? ErrorCode { get; set; } = errorCode;
			public string? ErrorMessage { get; set; } = errorMessage;
		}

		public class DataverseHttpClientException(HttpStatusCode statusCode, string message) : Exception(message)
		{
			public HttpStatusCode HttpStatusCode { get; set; } = statusCode;
		}
	}
}
