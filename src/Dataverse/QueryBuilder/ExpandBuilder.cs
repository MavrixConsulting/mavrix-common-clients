using System.Text;

namespace Mavrix.Common.Dataverse.QueryBuilder
{
	/// <summary>
	/// Builds <c>$expand</c> clauses for Dataverse OData queries, including nested expands.
	/// </summary>
	/// <remarks>
	/// Inputs should be valid OData fragments and pre-encoded as needed; escaping is not performed.
	/// Instances are not thread-safe and are intended for one-time use per query.
	/// </remarks>
	public class ExpandBuilder(string property)
	{
		/// <summary>
		/// Gets the primary navigation property to expand.
		/// </summary>
		private string Property { get; set; } = property;
		/// <summary>
		/// Gets the properties to select within the expanded entity.
		/// </summary>
		private string[] Select { get; set; } = [];
		/// <summary>
		/// Gets the filter applied within the expanded entity.
		/// </summary>
		private string? Filter { get; set; }
		/// <summary>
		/// Gets nested expand definitions.
		/// </summary>
		private List<ExpandBuilder> NestedExpands { get; set; } = [];

		/// <summary>
		/// Specifies the properties to select in the expanded entity.
		/// </summary>
		/// <param name="select">Property names to include.</param>
		/// <returns>The current builder instance.</returns>
		public ExpandBuilder WithSelect(params string[] select)
		{
			Select = select;
			return this;
		}

		/// <summary>
		/// Applies a filter within the expanded entity.
		/// </summary>
		/// <param name="filter">The OData filter expression.</param>
		/// <returns>The current builder instance.</returns>
		public ExpandBuilder WithFilter(string filter)
		{
			Filter = filter;
			return this;
		}

		/// <summary>
		/// Adds a nested expand to include related entities of the expanded entity.
		/// </summary>
		/// <param name="nestedExpand">The nested expand builder.</param>
		/// <returns>The current builder instance.</returns>
		public ExpandBuilder AddNestedExpand(ExpandBuilder nestedExpand)
		{
			NestedExpands.Add(nestedExpand);
			return this;
		}

		/// <summary>
		/// Builds the <c>$expand</c> expression for this builder and any nested builders.
		/// </summary>
		/// <returns>The composed expand expression.</returns>
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

		/// <summary>
		/// Returns the built <c>$expand</c> expression.
		/// </summary>
		/// <returns>The composed expand expression.</returns>
		public override string ToString() => Build();
	}
}
