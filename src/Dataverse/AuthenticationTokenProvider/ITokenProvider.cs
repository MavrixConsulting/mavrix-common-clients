using Azure.Core;

namespace Mavrix.Common.Dataverse.AuthenticationTokenProvider
{
	/// <summary>
	/// Provides access tokens for authenticated HTTP requests.
	/// </summary>
	public interface ITokenProvider
	{	
		/// <summary>
		/// Gets an access token for the specified scope.
		/// </summary>
		/// <param name="scope">The resource scope.</param>
		/// <param name="cancellationToken">Token to cancel the operation.</param>
		/// <returns>The access token string.</returns>
		ValueTask<string> GetTokenAsync(string scope, CancellationToken cancellationToken);
	}
}
