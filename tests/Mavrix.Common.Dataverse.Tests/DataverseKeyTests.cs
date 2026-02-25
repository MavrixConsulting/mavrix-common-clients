using Xunit;

namespace Mavrix.Common.Dataverse.Tests;

public class DataverseKeyTests
{
	[Fact]
	public void Constructor_WithGuid_UsesGuidStringAsKeyExpression()
	{
		var id = Guid.NewGuid();
		var key = new DataverseKey(id);

		Assert.Equal(id.ToString(), key.KeyExpression);
	}

	[Fact]
	public void Constructor_WithSingleAlternateKey_FormatsExpression()
	{
		var key = new DataverseKey("accountnumber", "ACC-001");

		Assert.Equal("accountnumber='ACC-001'", key.KeyExpression);
	}

	[Fact]
	public void Constructor_WithMultipleAlternateKeys_FormatsCompositeExpression()
	{
		var key = new DataverseKey(("tenant", "contoso"), ("code", "ABC"));

		Assert.Equal("tenant='contoso',code='ABC'", key.KeyExpression);
	}

	[Fact]
	public void Constructor_WithAlternateKey_EscapesSingleQuotes()
	{
		var key = new DataverseKey("name", "O'Brian");

		Assert.Equal("name='O''Brian'", key.KeyExpression);
	}

	[Fact]
	public void ImplicitOperator_FromGuid_UsesGuidStringAsKeyExpression()
	{
		var id = Guid.NewGuid();

		DataverseKey key = id;

		Assert.Equal(id.ToString(), key.KeyExpression);
	}
}