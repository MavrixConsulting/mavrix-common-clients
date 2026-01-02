namespace Mavrix.Common.Dataverse.CustomAttributes
{
	/// <summary>
	/// Marks a DTO with the Dataverse table set name used for requests (for example, <c>"accounts"</c>).
	/// </summary>
	/// <param name="setName">The logical set name of the Dataverse table.</param>
	/// <remarks>
	/// Apply to classes derived from <c>DataverseTable</c> (see <c>Account</c> DTO) to resolve table endpoints.
	/// </remarks>
	[AttributeUsage(AttributeTargets.Class, Inherited = false)]
	public sealed class DataverseSetNameAttribute(string setName) : Attribute
	{
		/// <summary>
		/// Gets the Dataverse set name.
		/// </summary>
		public string SetName { get; } = setName;
	}
}
