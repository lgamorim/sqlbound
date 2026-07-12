namespace SqlBound.Cli.UnitTests;

public sealed class MigrationScaffolderSlugTests
{
    [Theory]
    [InlineData("create items", "create_items")]
    [InlineData("add-users", "add_users")]
    [InlineData("  Multiple   spaces  ", "multiple_spaces")]
    [InlineData("MixedCASE", "mixedcase")]
    [InlineData("already_snake", "already_snake")]
    [InlineData("tags & categories!", "tags_categories")]
    public void Should_ProduceSnakeCaseSlug_When_NameHasLettersOrDigits(string name, string expected)
    {
        Assert.Equal(expected, MigrationScaffolder.Slugify(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    public void Should_ProduceEmptySlug_When_NameHasNoLettersOrDigits(string name)
    {
        Assert.Empty(MigrationScaffolder.Slugify(name));
    }
}
