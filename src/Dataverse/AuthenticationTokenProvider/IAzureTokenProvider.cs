using Azure.Core;

namespace Mavrix.Common.Dataverse.AuthenticationTokenProvider
{
	public interface IAzureTokenProvider : ITokenProvider
	{
		ValueTask<AccessToken> GetAccessTokenAsync(string scope, CancellationToken cancellationToken);
		TokenCredential GetTokenCredential();
	}
}
