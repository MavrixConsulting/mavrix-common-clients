namespace Mavrix.Common.Dataverse.CustomAttributes
{
	[AttributeUsage(AttributeTargets.Class, Inherited = false)]
	public sealed class DataverseSetNameAttribute(string setName) : Attribute
	{
		public string SetName { get; } = setName;
	}
}
