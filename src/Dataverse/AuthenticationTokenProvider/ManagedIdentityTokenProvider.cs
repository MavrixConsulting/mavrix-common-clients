using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace Mavrix.Common.Dataverse.AuthenticationTokenProvider
{
	/// <summary>
	/// Provides Dataverse access tokens using Azure Managed Identity with in-memory caching.
	/// </summary>
	/// <remarks>
	/// Tokens are cached per scope until one minute before expiration. Instances are safe for concurrent calls; disposal is expected to be handled by the host at shutdown.
	/// </remarks>
	public class ManagedIdentityTokenProvider : IAzureTokenProvider, IDisposable
	{
		private readonly TokenCredential _credential;
		private readonly IMemoryCache _memoryCache;

		private readonly SemaphoreSlim SemaphoreSlim = new(1, 1);

		/// <summary>
		/// Initializes a new instance of <see cref="ManagedIdentityTokenProvider"/> using the default Azure credential chain.
		/// </summary>
		/// <param name="memoryCache">Cache for storing tokens per scope.</param>
		public ManagedIdentityTokenProvider(IMemoryCache memoryCache)
		{
			_credential = new DefaultAzureCredential();
			_memoryCache = memoryCache;
		}

		/// <summary>
		/// Gets the underlying <see cref="TokenCredential"/> used for token acquisition.
		/// </summary>
		public TokenCredential GetTokenCredential() => _credential;

		/// <inheritdoc />
		public async ValueTask<string> GetTokenAsync(string scope, CancellationToken cancellationToken) =>
			(await GetAccessTokenAsync(scope, cancellationToken)).Token;

		/// <inheritdoc />
		public async ValueTask<AccessToken> GetAccessTokenAsync(string scope, CancellationToken cancellationToken)
		{
			if (_memoryCache.TryGetValue(scope, out AccessToken accessToken))
			{
				return accessToken;
			}

			await SemaphoreSlim.WaitAsync(cancellationToken);
			try
			{
				if (_memoryCache.TryGetValue(scope, out accessToken))
				{
					return accessToken;
				}

				accessToken = await _credential.GetTokenAsync(new TokenRequestContext([scope]), cancellationToken);
				_memoryCache.Set(scope, accessToken, new MemoryCacheEntryOptions { AbsoluteExpiration = accessToken.ExpiresOn.AddMinutes(-1) });
				return accessToken;
			}
			finally
			{
				SemaphoreSlim.Release();
			}
		}

		/// <summary>
		/// Releases resources used by the token provider.
		/// </summary>
		public void Dispose()
		{
			GC.SuppressFinalize(this);
			SemaphoreSlim?.Dispose();
		}
	}
}
