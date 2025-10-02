using Azure.Core;

namespace Mavrix.Common.Dataverse.AuthenticationTokenProvider
{
	public interface ITokenProvider
	{	
		ValueTask<string> GetTokenAsync(string scope, CancellationToken cancellationToken);
	}
}
