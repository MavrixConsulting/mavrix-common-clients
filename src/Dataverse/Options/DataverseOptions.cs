namespace Mavrix.Common.Dataverse.Options
{
	/// <summary>
	/// Configuration options for accessing Dataverse APIs.
	/// </summary>
	public class DataverseOptions
	{
		/// <summary>
		/// Configuration section name for binding <see cref="DataverseOptions"/>.
		/// </summary>
		public const string SectionName = "Dataverse";
		/// <summary>
		/// Gets or sets the base URL of the Dataverse environment.
		/// </summary>
		public required string BaseUrl { get; set; }
	}
}
