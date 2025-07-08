using System.Text;

namespace Mavrix.Common.Dataverse.QueryBuilder
{
	public class FilterBuilder(string filter)
	{
		private string Filter { get; set; } = filter;

		private List<string> AndFilters { get; set; } = [];

		public FilterBuilder And(string filter)
		{
			AndFilters.Add(filter);
			return this;
		}

		public string Build()
		{
			var builder = new StringBuilder(Filter);

			foreach (var filter in AndFilters)
			{
				builder.Append(" and ");
				builder.Append(filter);
			}

			return builder.ToString();
		}

		public override string ToString() => Build();
	}
}
