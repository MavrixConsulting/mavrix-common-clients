using Azure.Core;

namespace Mavrix.Common.Dataverse.AuthenticationTokenProvider
{
	/// <summary>
	/// Extends token provider functionality with Azure credential access.
	/// </summary>
	public interface IAzureTokenProvider : ITokenProvider
	{
		/// <summary>
		/// Gets an <see cref="AccessToken"/> for the specified scope.
		/// </summary>
		/// <param name="scope">The resource scope.</param>
		/// <param name="cancellationToken">Token to cancel the operation.</param>
		/// <returns>The access token.</returns>
		ValueTask<AccessToken> GetAccessTokenAsync(string scope, CancellationToken cancellationToken);
		/// <summary>
		/// Retrieves the underlying <see cref="TokenCredential"/>.
		/// </summary>
		/// <returns>The Azure token credential.</returns>
		TokenCredential GetTokenCredential();
	}
}
