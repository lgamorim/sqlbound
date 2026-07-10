using System.Reflection;

namespace SqlBound.UnitTests;

public class AssemblyLoadTests
{
    [Fact]
    public void Should_LoadSqlBoundAssembly_When_TestProjectBuilds()
    {
        var assembly = Assembly.Load("SqlBound");

        Assert.Equal("SqlBound", assembly.GetName().Name);
    }
}
