using Mavrix.Common.Dataverse.QueryBuilder;
using Xunit;

namespace Mavrix.Common.Dataverse.Tests.QueryBuilder;

public class FilterBuilderTests
{
	[Fact]
	public void Build_SingleFilter_ReturnsFilter()
	{
		var sut = new FilterBuilder("statecode eq 0");
		Assert.Equal("statecode eq 0", sut.Build());
	}

	[Fact]
	public void Build_WithAndFilters_JoinsWithAnd()
	{
		var sut = new FilterBuilder("statecode eq 0")
			.And("name eq 'Test'")
			.And("address1_city eq 'London'");
		Assert.Equal("statecode eq 0 and name eq 'Test' and address1_city eq 'London'", sut.Build());
	}
}
