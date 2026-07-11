namespace SqlBound.UnitTests;

public class SqlParametersTests
{
    [Fact]
    public void Should_HaveZeroCount_When_ConstructedWithNoParameters()
    {
        var parameters = new SqlParameters();

        Assert.Equal(0, parameters.Count);
    }

    [Fact]
    public void Should_HaveZeroCount_When_UsingEmpty()
    {
        Assert.Equal(0, SqlParameters.Empty.Count);
    }

    [Fact]
    public void Should_StoreNameAndValue_When_GivenSingleParameter()
    {
        var parameters = new SqlParameters(("id", 5));

        Assert.Equal(1, parameters.Count);
        Assert.Equal("id", parameters.Items[0].Key);
        Assert.Equal(5, parameters.Items[0].Value);
    }

    [Fact]
    public void Should_NormalizeToDBNull_When_ValueIsNull()
    {
        var parameters = new SqlParameters(("name", null));

        Assert.Equal(DBNull.Value, parameters.Items[0].Value);
    }

    [Fact]
    public void Should_PreserveOrder_When_GivenMultipleParameters()
    {
        var parameters = new SqlParameters(("id", 1), ("name", "Ada"), ("active", true));

        Assert.Equal(["id", "name", "active"], parameters.Items.Select(item => item.Key));
    }

    [Fact]
    public void Should_ThrowArgumentException_When_ParameterNameIsNull()
    {
        Assert.Throws<ArgumentException>(() => new SqlParameters((null!, 1)));
    }

    [Fact]
    public void Should_ThrowArgumentException_When_ParameterNameIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => new SqlParameters(("", 1)));
    }

    [Fact]
    public void Should_ThrowArgumentException_When_ParameterNameIsWhitespace()
    {
        Assert.Throws<ArgumentException>(() => new SqlParameters(("   ", 1)));
    }

    [Fact]
    public void Should_ThrowArgumentException_When_DuplicateParameterNameGiven()
    {
        Assert.Throws<ArgumentException>(() => new SqlParameters(("id", 1), ("id", 2)));
    }
}
