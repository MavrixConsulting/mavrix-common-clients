using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace Mavrix.Common.Dataverse.AuthenticationTokenProvider
{
	public class ManagedIdentityTokenProvider : ITokenProvider, IDisposable
	{
		private readonly TokenCredential _credential;
		private readonly IMemoryCache _memoryCache;

		private readonly SemaphoreSlim SemaphoreSlim = new(1, 1);

		public ManagedIdentityTokenProvider(IMemoryCache memoryCache)
		{
			_credential = new DefaultAzureCredential();
			_memoryCache = memoryCache;
		}

		public TokenCredential GetTokenCredential() => _credential;

		public async ValueTask<string> GetTokenAsync(string scope, CancellationToken cancellationToken) =>
			(await GetAccessTokenAsync(scope, cancellationToken)).Token;

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

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			SemaphoreSlim?.Dispose();
		}
	}
}
