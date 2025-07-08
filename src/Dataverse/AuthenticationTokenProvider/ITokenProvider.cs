using Azure.Core;

namespace Mavrix.Common.Dataverse.AuthenticationTokenProvider
{
	public interface ITokenProvider
	{
		ValueTask<AccessToken> GetAccessTokenAsync(string scope, CancellationToken cancellationToken);
		ValueTask<string> GetTokenAsync(string scope, CancellationToken cancellationToken);
		TokenCredential GetTokenCredential();
	}
}
