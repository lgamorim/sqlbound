namespace SqlBound.Migrations;

/// <summary>Which half of a migration a file represents: the forward script or its rollback.</summary>
internal enum MigrationDirection
{
    Up,
    Down,
}
