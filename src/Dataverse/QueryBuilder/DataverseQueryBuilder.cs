using System.Text;

namespace Mavrix.Common.Dataverse.QueryBuilder
{
	/// <summary>
	/// Provides a fluent API for building Dataverse OData query strings.
	/// </summary>
	/// <remarks>
	/// Values passed into the builder should be pre-encoded or valid OData fragments; no escaping is performed.
	/// Instances are not thread-safe and are intended for one-time use per query.
	/// </remarks>
	/// <example>
	/// <code>
	/// var query = new DataverseQueryBuilder()
	///     .Select("name", "accountnumber")
	///     .Filter("statecode eq 0")
	///     .OrderBy("name")
	///     .Top(10)
	///     .Build("accounts");
	/// </code>
	/// </example>
	public class DataverseQueryBuilder()
	{
		/// <summary>
		/// Gets the selected properties for the query.
		/// </summary>
		public string[]? SelectProperties { get; private set; }
		/// <summary>
		/// Gets the filter expression for the query.
		/// </summary>
		public string? FilterExpression { get; private set; }
		/// <summary>
		/// Gets the collection of expand clauses for the query.
		/// </summary>
		public List<ExpandBuilder> ExpandBuilders { get; private set; } = [];
		/// <summary>
		/// Gets the order by properties for the query.
		/// </summary>
		public string[]? OrderByProperties { get; private set; }
		/// <summary>
		/// Gets the apply aggregate clauses for the query.
		/// </summary>
		public string[]? ApplyAggregate { get; private set; }
		/// <summary>
		/// Gets the maximum number of records to return.
		/// </summary>
		public int? TopCount { get; private set; }
		/// <summary>
		/// Gets a value indicating whether to return a record count.
		/// </summary>
		public bool? ReturnCount { get; private set; }
		/// <summary>
		/// Gets a value indicating whether to include annotations in the response.
		/// </summary>
		public bool IncludeAnnotations { get; private set; }

		/// <summary>
		/// Specifies the properties to select.
		/// </summary>
		/// <param name="properties">The property names to include.</param>
		/// <returns>The current builder instance.</returns>
		public DataverseQueryBuilder Select(params string[] properties)
		{
			SelectProperties = properties;
			return this;
		}

		/// <summary>
		/// Applies a filter expression.
		/// </summary>
		/// <param name="filterExpression">The OData filter expression.</param>
		/// <returns>The current builder instance.</returns>
		public DataverseQueryBuilder Filter(string filterExpression)
		{
			FilterExpression = filterExpression;
			return this;
		}

		/// <summary>
		/// Applies a filter using a filter builder.
		/// </summary>
		/// <param name="filterBuilder">The filter builder to use.</param>
		/// <returns>The current builder instance.</returns>
		public DataverseQueryBuilder Filter(FilterBuilder filterBuilder)
		{
			FilterExpression = filterBuilder.Build();
			return this;
		}
		/// <summary>
		/// Adds an expand clause for related entities.
		/// </summary>
		/// <param name="expandBuilder">The expand builder to add.</param>
		/// <returns>The current builder instance.</returns>
		public DataverseQueryBuilder AddExpand(ExpandBuilder expandBuilder)
		{
			ExpandBuilders?.Add(expandBuilder);
			return this;
		}


		/// <summary>
		/// Specifies the order by properties.
		/// </summary>
		/// <param name="properties">The property names to sort by.</param>
		/// <returns>The current builder instance.</returns>
		public DataverseQueryBuilder OrderBy(params string[] properties)
		{
			OrderByProperties = properties;
			return this;
		}

		/// <summary>
		/// Applies aggregate expressions.
		/// </summary>
		/// <param name="aggregates">The aggregate expressions to apply.</param>
		/// <returns>The current builder instance.</returns>
		public DataverseQueryBuilder Apply(params string[] aggregates)
		{
			ApplyAggregate = aggregates;
			return this;
		}

		/// <summary>
		/// Limits the number of returned records.
		/// </summary>
		/// <param name="count">The maximum number of records.</param>
		/// <returns>The current builder instance.</returns>
		public DataverseQueryBuilder Top(int count)
		{
			TopCount = count;
			return this;
		}

		/// <summary>
		/// Requests the total record count in the response.
		/// </summary>
		/// <returns>The current builder instance.</returns>
		public DataverseQueryBuilder Count()
		{
			ReturnCount = true;
			return this;
		}

	/// <summary>
	/// Includes annotations in the query response (for example, <c>odata.*</c> metadata fields).
	/// </summary>
		/// <returns>The current builder instance.</returns>
		public DataverseQueryBuilder SetIncludeAnnotations()
		{
			IncludeAnnotations = true;
			return this;
		}

		/// <summary>
		/// Builds the query string for the specified resource.
		/// </summary>
		/// <param name="resource">The resource to query.</param>
		/// <returns>The composed query string.</returns>
		public string Build(string? resource)
		{
			return BuildCore(new StringBuilder(resource).Append('?'));
		}

		/// <summary>
		/// Builds the query string for the specified resource and identifier.
		/// </summary>
		/// <param name="resource">The resource to query.</param>
		/// <param name="id">The unique identifier of the record.</param>
		/// <returns>The composed query string.</returns>
		public string Build(string? resource, Guid? id)
		{
			return BuildCore(new StringBuilder(resource).Append('(').Append(id).Append(')').Append('?'));
		}

		/// <summary>
		/// Builds the query string using the configured options.
		/// </summary>
		/// <param name="queryBuilder">The <see cref="StringBuilder"/> instance to append to.</param>
		/// <returns>The composed query string.</returns>
		private string BuildCore(StringBuilder queryBuilder)
		{
			if (TopCount is not null && TopCount > 0)
			{
				queryBuilder.Append("$top=");
				queryBuilder.Append(TopCount);
				queryBuilder.Append('&');
			}

			if (SelectProperties is not null && SelectProperties.Length > 0)
			{
				queryBuilder.Append("$select=");
				queryBuilder.Append(string.Join(",", SelectProperties));
				queryBuilder.Append('&');
			}

			if (!string.IsNullOrEmpty(FilterExpression))
			{
				queryBuilder.Append("$filter=");
				queryBuilder.Append(FilterExpression);
				queryBuilder.Append('&');
			}

			if (ExpandBuilders.Count > 0)
			{
				queryBuilder.Append("$expand=");
				var expandQueries = ExpandBuilders.Select(eb => eb.Build()).ToList();
				queryBuilder.Append(string.Join(",", expandQueries));
				queryBuilder.Append('&');
			}

			if (OrderByProperties is not null && OrderByProperties.Length > 0)
			{
				queryBuilder.Append("$orderby=");
				queryBuilder.Append(string.Join(",", OrderByProperties));
				queryBuilder.Append('&');
			}

			if (ApplyAggregate is not null && ApplyAggregate.Length > 0)
			{
				queryBuilder.Append("$apply=");
				queryBuilder.Append(string.Join(",", ApplyAggregate));
				queryBuilder.Append('&');
			}

			if (ReturnCount == true)
			{
				queryBuilder.Append("$count=true");
				queryBuilder.Append('&');
			}


			if (queryBuilder.Length > 0)
			{
				// Remove the trailing "&" or "?" character
				queryBuilder.Length -= 1;
			}

			return queryBuilder.ToString();
		}
	}
}
