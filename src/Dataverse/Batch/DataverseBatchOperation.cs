using Mavrix.Common.Dataverse.CustomAttributes;
using Mavrix.Common.Dataverse.DTO;
using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;

namespace Mavrix.Common.Dataverse.Batch
{
	/// <summary>
	/// Represents a single write operation in a Dataverse change set.
	/// </summary>
	public sealed class DataverseBatchOperation
	{
		private const string IfMatchHeaderName = "If-Match";
		private const string WildcardIfMatchValue = "*";

		private readonly IReadOnlyDictionary<string, string> _headers;

		/// <summary>
		/// Initializes a new instance of the <see cref="DataverseBatchOperation"/> class.
		/// </summary>
		/// <param name="method">HTTP method for the operation (for example <see cref="HttpMethod.Post"/>).</param>
		/// <param name="uri">Operation URI relative to the Dataverse API root, or a content-id reference path like <c>$1/field</c>.</param>
		/// <param name="content">Optional request body content.</param>
		/// <param name="contentId">Optional content-id used for later <c>$n</c> references.</param>
		/// <param name="headers">Optional operation headers.</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="method"/> or <paramref name="uri"/> is null.</exception>
		/// <exception cref="ArgumentException">Thrown when <paramref name="uri"/> is empty or whitespace.</exception>
		private DataverseBatchOperation(
			HttpMethod method,
			string uri,
			JsonContent? content = null,
			int? contentId = null,
			IReadOnlyDictionary<string, string>? headers = null)
		{
			Method = method ?? throw new ArgumentNullException(nameof(method));
			if (string.IsNullOrWhiteSpace(uri))
			{
				throw new ArgumentException("Operation URI must be a non-empty string representing a relative Dataverse resource path or a content-id reference (for example, \"$1/field\").", nameof(uri));
			}

			Uri = uri;
			Content = content;
			ContentId = contentId;
			_headers = headers is null
				? new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
				: new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase));
		}

		/// <summary>
		/// Creates a <c>POST</c> batch operation for a Dataverse table set resolved from <typeparamref name="TTable"/>.
		/// </summary>
		/// <typeparam name="TTable">Dataverse table type used to resolve set name.</typeparam>
		/// <param name="payload">DTO payload to serialize as JSON.</param>
		/// <param name="jsonSerializerOptions">JSON serializer options.</param>
		/// <param name="contentId">Optional content-id used for later <c>$n</c> references.</param>
		/// <param name="headers">Optional operation headers.</param>
		/// <returns>A configured batch operation.</returns>
		public static DataverseBatchOperation Create<TTable>(
			TTable payload,
			JsonSerializerOptions jsonSerializerOptions,
			int? contentId = null,
			IReadOnlyDictionary<string, string>? headers = null)
			where TTable : DataverseTable
		{
			return new DataverseBatchOperation(HttpMethod.Post, GetSetName<TTable>(), CreateJsonContent(payload, jsonSerializerOptions), contentId, headers);
		}

		/// <summary>
		/// Creates a <c>PATCH</c> batch operation for a Dataverse table set resolved from <typeparamref name="TTable"/> and key.
		/// </summary>
		/// <typeparam name="TTable">Dataverse table type used to resolve set name.</typeparam>
		/// <param name="key">Dataverse key identifying the target record.</param>
		/// <param name="payload">DTO payload to serialize as JSON.</param>
		/// <param name="jsonSerializerOptions">JSON serializer options.</param>
		/// <param name="contentId">Optional content-id used for later <c>$n</c> references.</param>
		/// <param name="headers">Optional operation headers. This operation always applies <c>If-Match: *</c>.</param>
		/// <returns>A configured batch operation.</returns>
		public static DataverseBatchOperation Update<TTable>(
			DataverseKey key,
			TTable payload,
			JsonSerializerOptions jsonSerializerOptions,
			int? contentId = null,
			IReadOnlyDictionary<string, string>? headers = null)
			where TTable : DataverseTable
		{
			return new DataverseBatchOperation(
				HttpMethod.Patch,
				BuildEntityUri<TTable>(key),
				CreateJsonContent(payload, jsonSerializerOptions),
				contentId,
				EnsureUpdateHeaders(headers));
		}

		/// <summary>
		/// Creates a <c>PUT</c> batch operation for a Dataverse table set resolved from <typeparamref name="TTable"/> and key.
		/// </summary>
		/// <typeparam name="TTable">Dataverse table type used to resolve set name.</typeparam>
		/// <param name="key">Dataverse key identifying the target record.</param>
		/// <param name="payload">DTO payload to serialize as JSON.</param>
		/// <param name="jsonSerializerOptions">JSON serializer options.</param>
		/// <param name="contentId">Optional content-id used for later <c>$n</c> references.</param>
		/// <param name="headers">Optional operation headers.</param>
		/// <returns>A configured batch operation.</returns>
		public static DataverseBatchOperation Upsert<TTable>(
			DataverseKey key,
			TTable payload,
			JsonSerializerOptions jsonSerializerOptions,
			int? contentId = null,
			IReadOnlyDictionary<string, string>? headers = null)
			where TTable : DataverseTable
		{
			return new DataverseBatchOperation(HttpMethod.Put, BuildEntityUri<TTable>(key), CreateJsonContent(payload, jsonSerializerOptions), contentId, headers);
		}

		/// <summary>
		/// Creates a <c>DELETE</c> batch operation for a Dataverse table set resolved from <typeparamref name="TTable"/> and key.
		/// </summary>
		/// <typeparam name="TTable">Dataverse table type used to resolve set name.</typeparam>
		/// <param name="key">Dataverse key identifying the target record.</param>
		/// <param name="contentId">Optional content-id used for later <c>$n</c> references.</param>
		/// <param name="headers">Optional operation headers.</param>
		/// <returns>A configured batch operation.</returns>
		public static DataverseBatchOperation Delete<TTable>(
			DataverseKey key,
			int? contentId = null,
			IReadOnlyDictionary<string, string>? headers = null)
			where TTable : DataverseTable
		{
			return new DataverseBatchOperation(HttpMethod.Delete, BuildEntityUri<TTable>(key), null, contentId, headers);
		}

		/// <summary>
		/// Gets the HTTP method for the operation.
		/// </summary>
		public HttpMethod Method { get; }

		/// <summary>
		/// Gets the operation URI relative to Dataverse API root, or a content-id reference path.
		/// </summary>
		public string Uri { get; }

		/// <summary>
		/// Gets optional request body content.
		/// </summary>
		public JsonContent? Content { get; }

		/// <summary>
		/// Gets optional content-id for referencing this operation in later requests.
		/// </summary>
		public int? ContentId { get; }

		/// <summary>
		/// Gets optional operation-specific headers.
		/// </summary>
		public IReadOnlyDictionary<string, string> Headers => _headers;

		private static JsonContent CreateJsonContent<TPayload>(TPayload payload, JsonSerializerOptions jsonSerializerOptions)
		{
			ArgumentNullException.ThrowIfNull(payload);
			ArgumentNullException.ThrowIfNull(jsonSerializerOptions);
			return JsonContent.Create(payload, options: jsonSerializerOptions);
		}

		private static string BuildEntityUri<TTable>(DataverseKey key) where TTable : DataverseTable
		{
			return $"{GetSetName<TTable>()}({key.KeyExpression})";
		}

		private static Dictionary<string, string> EnsureUpdateHeaders(IReadOnlyDictionary<string, string>? headers)
		{
			var result = headers is null
				? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
				: new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);

			result[IfMatchHeaderName] = WildcardIfMatchValue;
			return result;
		}

		private static string GetSetName<TTable>() where TTable : DataverseTable
		{
			var setNameAttribute = typeof(TTable).GetCustomAttribute<DataverseSetNameAttribute>();
			if (setNameAttribute is null || string.IsNullOrWhiteSpace(setNameAttribute.SetName))
			{
				throw new InvalidOperationException($"Type {typeof(TTable).Name} must be decorated with DataverseSetNameAttribute.");
			}

			return setNameAttribute.SetName;
		}
	}
}
