namespace Mavrix.Common.Dataverse.Batch
{
	internal static class DataverseBatchResponseParser
	{
		private const string CrLf = "\r\n";
		private const string DoubleCrLf = "\r\n\r\n";
		private const string BoundaryParameterName = "boundary";
		private const string BoundaryPrefix = "boundary=";
		private const string ContentTypeHeaderName = "Content-Type";
		private const string ContentIdHeaderName = "Content-ID";
		private const string ODataEntityIdHeaderName = "OData-EntityId";
		private const string MultipartMixedContentType = "multipart/mixed";
		private const string Http11Prefix = "HTTP/1.1";
		private static readonly ReadOnlyMemory<char> CrLfMemory = CrLf.AsMemory();

		public static async ValueTask<DataverseBatchResult> ParseAsync(HttpContent responseContent, CancellationToken cancellationToken)
		{
			var contentType = responseContent.Headers.ContentType;
			if (contentType is null)
			{
				return new DataverseBatchResult();
			}

			var boundaryParameter = contentType.Parameters
				.FirstOrDefault(parameter => parameter.Name?.Equals(BoundaryParameterName, StringComparison.OrdinalIgnoreCase) == true);

			var boundary = boundaryParameter?.Value?.Trim().Trim('"');
			if (string.IsNullOrWhiteSpace(boundary))
			{
				return new DataverseBatchResult();
			}

			var payload = await responseContent.ReadAsStringAsync(cancellationToken);
			var operationResults = new List<DataverseBatchOperationResult>();
			ParseMultipartSections(payload.AsSpan(), boundary.AsSpan(), operationResults);

			return new DataverseBatchResult
			{
				OperationResults = operationResults
			};
		}

		private static void ParseOperationResultsFromSection(ReadOnlySpan<char> section, List<DataverseBatchOperationResult> operationResults)
		{
			var headers = SplitHeadersAndBody(section, out var body);
			if (headers.TryGetValue(ContentTypeHeaderName, out var contentType) && contentType.StartsWith(MultipartMixedContentType, StringComparison.OrdinalIgnoreCase))
			{
				var innerBoundary = ExtractBoundary(contentType);
				if (!string.IsNullOrWhiteSpace(innerBoundary))
				{
					ParseMultipartSections(body, innerBoundary.AsSpan(), operationResults);
					return;
				}
			}

			if (!body.StartsWith(Http11Prefix.AsSpan(), StringComparison.OrdinalIgnoreCase))
			{
				return;
			}

			var responseRemainder = body;
			if (!TryReadLine(ref responseRemainder, out var statusLine))
			{
				return;
			}

			var statusCode = ParseStatusCode(statusLine);
			var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			while (TryReadLine(ref responseRemainder, out var line))
			{
				if (line.IsEmpty)
				{
					break;
				}

				if (TrySplitHeader(line, out var headerName, out var headerValue))
				{
					responseHeaders[headerName.ToString()] = headerValue.ToString();
				}
			}

			var trimmedResponseBody = responseRemainder.Trim();
			operationResults.Add(
				new DataverseBatchOperationResult
				{
					ContentId = ParseContentId(headers),
					StatusCode = statusCode,
					EntityId = responseHeaders.TryGetValue(ODataEntityIdHeaderName, out var entityIdHeader)
						? TryParseEntityId(entityIdHeader)
						: null,
					ResponseBody = trimmedResponseBody.IsEmpty ? null : trimmedResponseBody.ToString()
				});
		}

		private static void ParseMultipartSections(ReadOnlySpan<char> payload, ReadOnlySpan<char> boundary, List<DataverseBatchOperationResult> operationResults)
		{
			if (payload.IsEmpty || boundary.IsEmpty)
			{
				return;
			}

			var markerText = $"--{boundary}";
			var marker = markerText.AsSpan();
			var cursor = 0;

			while (cursor <= payload.Length)
			{
				var relativeBoundaryIndex = payload[cursor..].IndexOf(marker);
				if (relativeBoundaryIndex < 0)
				{
					return;
				}

				var boundaryIndex = cursor + relativeBoundaryIndex;
				var sectionStartIndex = boundaryIndex + marker.Length;

				var relativeNextBoundaryIndex = sectionStartIndex <= payload.Length
					? payload[sectionStartIndex..].IndexOf(marker)
					: -1;
				var isLastSection = relativeNextBoundaryIndex < 0;

				ReadOnlySpan<char> section;
				if (isLastSection)
				{
					section = payload[sectionStartIndex..];
					cursor = payload.Length;
				}
				else
				{
					var nextBoundaryIndex = sectionStartIndex + relativeNextBoundaryIndex;
					section = payload[sectionStartIndex..nextBoundaryIndex];
					cursor = nextBoundaryIndex;
				}

				var trimmedSection = section.Trim();
				if (!trimmedSection.IsEmpty && !trimmedSection.SequenceEqual("--".AsSpan()))
				{
					ParseOperationResultsFromSection(trimmedSection, operationResults);
				}

				if (isLastSection)
				{
					return;
				}
			}
		}

		private static Dictionary<string, string> SplitHeadersAndBody(ReadOnlySpan<char> section, out ReadOnlySpan<char> body)
		{
			var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			var splitIndex = section.IndexOf(DoubleCrLf.AsSpan(), StringComparison.Ordinal);

			if (splitIndex < 0)
			{
				body = section;
				return headers;
			}

			var headerBlock = section[..splitIndex];
			while (!headerBlock.IsEmpty)
			{
				if (!TryReadLine(ref headerBlock, out var line))
				{
					break;
				}

				if (line.IsEmpty)
				{
					continue;
				}

				if (TrySplitHeader(line, out var headerName, out var headerValue))
				{
					headers[headerName.ToString()] = headerValue.ToString();
				}
			}

			body = section[(splitIndex + DoubleCrLf.Length)..];
			return headers;
		}

		private static string? ExtractBoundary(string contentType)
		{
			var segments = contentType.AsSpan();
			while (!segments.IsEmpty)
			{
				var separatorIndex = segments.IndexOf(';');
				ReadOnlySpan<char> part;
				if (separatorIndex < 0)
				{
					part = segments;
					segments = [];
				}
				else
				{
					part = segments[..separatorIndex];
					segments = segments[(separatorIndex + 1)..];
				}

				part = part.Trim();
				if (!part.StartsWith(BoundaryPrefix, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				var boundary = part[BoundaryPrefix.Length..].Trim();
				if (boundary.Length >= 2 && boundary[0] == '"' && boundary[^1] == '"')
				{
					boundary = boundary[1..^1];
				}

				return boundary.ToString();
			}

			return null;
		}

		private static int ParseStatusCode(ReadOnlySpan<char> statusLine)
		{
			if (statusLine.IsEmpty)
			{
				return 0;
			}

			statusLine = statusLine.Trim();
			var firstSpace = statusLine.IndexOf(' ');
			if (firstSpace < 0)
			{
				return 0;
			}

			var remainder = statusLine[(firstSpace + 1)..];
			remainder = remainder.TrimStart();
			var nextSpace = remainder.IndexOf(' ');
			var statusCodeSpan = nextSpace >= 0 ? remainder[..nextSpace] : remainder;

			return int.TryParse(statusCodeSpan, out var statusCode) ? statusCode : 0;
		}

		private static Guid? TryParseEntityId(string entityIdHeader)
		{
			var openParenIndex = entityIdHeader.IndexOf('(');
			if (openParenIndex < 0)
			{
				return null;
			}

			var closeParenIndex = entityIdHeader.LastIndexOf(')');
			if (closeParenIndex <= openParenIndex)
			{
				return null;
			}

			var id = entityIdHeader[(openParenIndex + 1)..closeParenIndex];
			return Guid.TryParse(id, out var guid) ? guid : null;
		}

		private static int? ParseContentId(Dictionary<string, string> headers)
		{
			if (headers.TryGetValue(ContentIdHeaderName, out var contentIdValue) && int.TryParse(contentIdValue, out var contentId))
			{
				return contentId;
			}

			return null;
		}

		private static bool TrySplitHeader(ReadOnlySpan<char> line, out ReadOnlySpan<char> name, out ReadOnlySpan<char> value)
		{
			var separatorIndex = line.IndexOf(':');
			if (separatorIndex < 0)
			{
				name = default;
				value = default;
				return false;
			}

			name = line[..separatorIndex].Trim();
			value = line[(separatorIndex + 1)..].Trim();
			return !name.IsEmpty;
		}

		private static bool TryReadLine(ref ReadOnlySpan<char> text, out ReadOnlySpan<char> line)
		{
			if (text.IsEmpty)
			{
				line = default;
				return false;
			}

			var lineBreakIndex = text.IndexOf(CrLfMemory.Span);
			if (lineBreakIndex < 0)
			{
				line = text;
				text = [];
				return true;
			}

			line = text[..lineBreakIndex];
			text = text[(lineBreakIndex + CrLfMemory.Length)..];
			return true;
		}
	}
}