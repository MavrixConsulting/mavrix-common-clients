using System.Text;

namespace Mavrix.Common.Dataverse.QueryBuilder
{
	public class DataverseQueryBuilder()
	{
		public string[]? SelectProperties { get; private set; }
		public string? FilterExpression { get; private set; }
		public List<ExpandBuilder> ExpandBuilders { get; private set; } = [];
		public string[]? OrderByProperties { get; private set; }
		public string[]? ApplyAggregate { get; private set; }
		public int? TopCount { get; private set; }
		public bool? ReturnCount { get; private set; }
		public bool InludeAnnotations { get; private set; }

		public DataverseQueryBuilder Select(params string[] properties)
		{
			SelectProperties = properties;
			return this;
		}

		public DataverseQueryBuilder Filter(string filterExpression)
		{
			FilterExpression = filterExpression;
			return this;
		}

		public DataverseQueryBuilder Filter(FilterBuilder filterBuilder)
		{
			FilterExpression = filterBuilder.Build();
			return this;
		}
		public DataverseQueryBuilder AddExpand(ExpandBuilder expandBuilder)
		{
			ExpandBuilders?.Add(expandBuilder);
			return this;
		}


		public DataverseQueryBuilder OrderBy(params string[] properties)
		{
			OrderByProperties = properties;
			return this;
		}

		public DataverseQueryBuilder Apply(params string[] aggregates)
		{
			ApplyAggregate = aggregates;
			return this;
		}

		public DataverseQueryBuilder Top(int count)
		{
			TopCount = count;
			return this;
		}

		public DataverseQueryBuilder Count()
		{
			ReturnCount = true;
			return this;
		}

		public DataverseQueryBuilder SetInludeAnnotations()
		{
			InludeAnnotations = true;
			return this;
		}

		public string Build(string? resource)
		{
			return BuildCore(new StringBuilder(resource).Append('?'));
		}

		public string Build(string? resource, Guid? id)
		{
			return BuildCore(new StringBuilder(resource).Append('(').Append(id).Append(')').Append('?'));
		}

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
