namespace SqlBound.UnitTests;

public class SqlExecuteAttributeTests
{
    [Fact]
    public void Should_ExposeCommandText_When_Constructed()
    {
        var attribute = new SqlExecuteAttribute("DELETE FROM items");

        Assert.Equal("DELETE FROM items", attribute.CommandText);
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_CommandTextIsNull()
    {
        Assert.Throws<ArgumentNullException>("commandText", () => new SqlExecuteAttribute(null!));
    }

    [Fact]
    public void Should_TargetMethodsOnlyAndDisallowMultiple_When_AttributeUsageInspected()
    {
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            typeof(SqlExecuteAttribute), typeof(AttributeUsageAttribute))!;

        Assert.Equal(AttributeTargets.Method, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }

    [Fact]
    public void Should_BeSealed_When_TypeInspected()
    {
        Assert.True(typeof(SqlExecuteAttribute).IsSealed);
    }
}
