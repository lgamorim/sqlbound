namespace SqlBound.UnitTests;

public class SqlQueryAttributeTests
{
    [Fact]
    public void Should_ExposeCommandText_When_Constructed()
    {
        var attribute = new SqlQueryAttribute("SELECT id FROM items");

        Assert.Equal("SELECT id FROM items", attribute.CommandText);
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_CommandTextIsNull()
    {
        Assert.Throws<ArgumentNullException>("commandText", () => new SqlQueryAttribute(null!));
    }

    [Fact]
    public void Should_TargetMethodsOnlyAndDisallowMultiple_When_AttributeUsageInspected()
    {
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            typeof(SqlQueryAttribute), typeof(AttributeUsageAttribute))!;

        Assert.Equal(AttributeTargets.Method, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }

    [Fact]
    public void Should_BeSealed_When_TypeInspected()
    {
        Assert.True(typeof(SqlQueryAttribute).IsSealed);
    }
}
