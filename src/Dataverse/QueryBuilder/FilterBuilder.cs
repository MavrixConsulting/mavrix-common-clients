using System.Text;

namespace Mavrix.Common.Dataverse.QueryBuilder
{
	/// <summary>
	/// Builds OData filter expressions with support for chaining <c>and</c> clauses.
	/// </summary>
	/// <remarks>
	/// Values should be valid OData fragments and pre-encoded as needed; escaping is not performed.
	/// Instances are not thread-safe and intended for one-time use per query.
	/// </remarks>
	public class FilterBuilder(string filter)
	{
		/// <summary>
		/// Gets the base filter expression.
		/// </summary>
		private string Filter { get; set; } = filter;

		/// <summary>
		/// Gets additional filters combined with <c>and</c>.
		/// </summary>
		private List<string> AndFilters { get; set; } = [];

		/// <summary>
		/// Adds an additional filter combined with <c>and</c>.
		/// </summary>
		/// <param name="filter">The filter expression to append.</param>
		/// <returns>The current builder instance.</returns>
		public FilterBuilder And(string filter)
		{
			AndFilters.Add(filter);
			return this;
		}

		/// <summary>
		/// Builds the composed filter expression.
		/// </summary>
		/// <returns>The complete filter string.</returns>
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

		/// <summary>
		/// Returns the composed filter expression.
		/// </summary>
		/// <returns>The complete filter string.</returns>
		public override string ToString() => Build();
	}
}
