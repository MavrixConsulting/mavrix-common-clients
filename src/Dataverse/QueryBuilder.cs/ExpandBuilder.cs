using System.Text;

namespace Mavrix.Common.Dataverse.QueryBuilder
{
	public class ExpandBuilder(string property)
	{
		private string Property { get; set; } = property;
		private string[] Select { get; set; } = [];
		private string? Filter { get; set; }
		private List<ExpandBuilder> NestedExpands { get; set; } = [];

		public ExpandBuilder WithSelect(params string[] select)
		{
			Select = select;
			return this;
		}

		public ExpandBuilder WithFilter(string filter)
		{
			Filter = filter;
			return this;
		}

		public ExpandBuilder AddNestedExpand(ExpandBuilder nestedExpand)
		{
			NestedExpands.Add(nestedExpand);
			return this;
		}

		public string Build()
		{
			var expandExpression = new StringBuilder(Property);
			var hasInnerQuery = Select.Length > 0 || !string.IsNullOrEmpty(Filter) || NestedExpands.Count > 0;

			if (hasInnerQuery)
			{
				expandExpression.Append('(');

				if (Select.Length > 0)
				{
					expandExpression.Append("$select=");
					expandExpression.Append(string.Join(",", Select));
					expandExpression.Append(';');
				}

				if (!string.IsNullOrEmpty(Filter))
				{
					expandExpression.Append("$filter=");
					expandExpression.Append(Filter);
					expandExpression.Append(';');
				}

				// Correctly handle nested expands with $expand keyword
				if (NestedExpands.Count > 0)
				{
					expandExpression.Append("$expand=");
					var nestedQueries = NestedExpands.Select(nestedExpand => nestedExpand.Build()).ToList();
					expandExpression.Append(string.Join(",", nestedQueries));
					expandExpression.Append(';');
				}

				expandExpression.Length -= 1; // Remove the last semicolon
				expandExpression.Append(')');
			}

			return expandExpression.ToString();
		}

		public override string ToString() => Build();
	}
}
