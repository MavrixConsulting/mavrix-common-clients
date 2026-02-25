using Mavrix.Common.Dataverse;
using Mavrix.Common.Dataverse.QueryBuilder;
using Xunit;

namespace Mavrix.Common.Dataverse.Tests.QueryBuilder;

public class DataverseQueryBuilderBuildTests
{
	[Fact]
	public void Build_WithOnlyResource_ReturnsResourceWithNoQuery()
	{
		var sut = new DataverseQueryBuilder();
		var result = sut.Build("accounts");
		Assert.Equal("accounts", result);
	}

	[Fact]
	public void Build_WithDataverseKey_FromGuid_AppendsParentheses()
	{
		var id = Guid.NewGuid();
		var sut = new DataverseQueryBuilder();
		var result = sut.Build("accounts", new DataverseKey(id));
		Assert.Equal($"accounts({id})", result);
	}

	[Fact]
	public void Build_WithDataverseKey_AppendsParentheses()
	{
		var sut = new DataverseQueryBuilder();
		var result = sut.Build("accounts", new DataverseKey("accountnumber", "ACC-001"));
		Assert.Equal("accounts(accountnumber='ACC-001')", result);
	}

	[Fact]
	public void Build_WithSelect_JoinsProperties()
	{
		var sut = new DataverseQueryBuilder()
			.Select("accountid", "name");
		var result = sut.Build("accounts");
		Assert.Equal("accounts?$select=accountid,name", result);
	}

	[Fact]
	public void Build_WithFilter_AddsFilter()
	{
		var sut = new DataverseQueryBuilder()
			.Filter("statecode eq 0");
		var result = sut.Build("accounts");
		Assert.Equal("accounts?$filter=statecode eq 0", result);
	}

	[Fact]
	public void Build_WithFilterBuilder_AddsCombinedFilters()
	{
		var filter = new FilterBuilder("statecode eq 0").And("name eq 'Test'");
		var sut = new DataverseQueryBuilder()
			.Filter(filter);
		var result = sut.Build("accounts");
		Assert.Equal("accounts?$filter=statecode eq 0 and name eq 'Test'", result);
	}

	[Fact]
	public void Build_WithExpand_AddsExpand()
	{
		var expand = new ExpandBuilder("primarycontactid").WithSelect("fullname", "email");
		var sut = new DataverseQueryBuilder()
			.AddExpand(expand);
		var result = sut.Build("accounts");
		Assert.Equal("accounts?$expand=primarycontactid($select=fullname,email)", result);
	}

	[Fact]
	public void Build_WithOrderBy_AddsOrderBy()
	{
		var sut = new DataverseQueryBuilder()
			.OrderBy("name asc");
		var result = sut.Build("accounts");
		Assert.Equal("accounts?$orderby=name asc", result);
	}

	[Fact]
	public void Build_WithApply_AddsApply()
	{
		var sut = new DataverseQueryBuilder()
			.Apply("aggregate(accountid with count as Count)");
		var result = sut.Build("accounts");
		Assert.Equal("accounts?$apply=aggregate(accountid with count as Count)", result);
	}

	[Fact]
	public void Build_WithTop_AddsTop()
	{
		var sut = new DataverseQueryBuilder()
			.Top(5);
		var result = sut.Build("accounts");
		Assert.Equal("accounts?$top=5", result);
	}

	[Fact]
	public void Build_WithCount_AddsCountTrue()
	{
		var sut = new DataverseQueryBuilder()
			.Count();
		var result = sut.Build("accounts");
		Assert.Equal("accounts?$count=true", result);
	}

	[Fact]
	public void Build_WithMultipleClauses_OrdersCorrectlyAndTrimsAmpersand()
	{
		var expand = new ExpandBuilder("primarycontactid").WithSelect("fullname");
		var sut = new DataverseQueryBuilder()
			.Top(10)
			.Select("accountid", "name")
			.Filter("statecode eq 0")
			.AddExpand(expand)
			.OrderBy("name asc")
			.Apply("aggregate(accountid with count as Count)")
			.Count();

		var result = sut.Build("accounts");

		var expected = "accounts?$top=10&$select=accountid,name&$filter=statecode eq 0&$expand=primarycontactid($select=fullname)&$orderby=name asc&$apply=aggregate(accountid with count as Count)&$count=true";

		Assert.Equal(expected, result);
	}

	[Fact]
	public void Build_DoesNotLeaveTrailingCharacters()
	{
		var sut = new DataverseQueryBuilder()
			.Select("x");
		var result = sut.Build("accounts");
		Assert.False(result.EndsWith("&") || result.EndsWith("?"));
	}
}
