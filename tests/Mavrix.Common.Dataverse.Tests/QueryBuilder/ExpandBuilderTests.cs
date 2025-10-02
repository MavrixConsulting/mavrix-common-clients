using Mavrix.Common.Dataverse.QueryBuilder;
using Xunit;

namespace Mavrix.Common.Dataverse.Tests.QueryBuilder;

public class ExpandBuilderTests
{
	[Fact]
	public void Build_WithPropertyOnly_ReturnsProperty()
	{
		var sut = new ExpandBuilder("primarycontactid");
		Assert.Equal("primarycontactid", sut.Build());
	}

	[Fact]
	public void Build_WithSelect_AddsSelectSegment()
	{
		var sut = new ExpandBuilder("primarycontactid")
			.WithSelect("fullname", "email");
		Assert.Equal("primarycontactid($select=fullname,email)", sut.Build());
	}

	[Fact]
	public void Build_WithFilter_AddsFilterSegment()
	{
		var sut = new ExpandBuilder("contacts")
			.WithFilter("statecode eq 0");
		Assert.Equal("contacts($filter=statecode eq 0)", sut.Build());
	}

	[Fact]
	public void Build_WithNestedExpand_AddsNestedCorrectly()
	{
		var nested = new ExpandBuilder("parentcustomerid").WithSelect("name");
		var sut = new ExpandBuilder("primarycontactid")
			.AddNestedExpand(nested);
		Assert.Equal("primarycontactid($expand=parentcustomerid($select=name))", sut.Build());
	}

	[Fact]
	public void Build_WithMultipleClauses_OrdersAndTrims()
	{
		var nested = new ExpandBuilder("parentcustomerid").WithSelect("name");
		var sut = new ExpandBuilder("primarycontactid")
			.WithSelect("fullname")
			.WithFilter("statecode eq 0")
			.AddNestedExpand(nested);
		Assert.Equal("primarycontactid($select=fullname;$filter=statecode eq 0;$expand=parentcustomerid($select=name))", sut.Build());
	}
}
