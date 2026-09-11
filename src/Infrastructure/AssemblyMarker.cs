namespace Earthward.Infrastructure;
/// <summary>Managed runtime identity, also used by migration smoke checks.</summary>
public static class AssemblyMarker
{
    public const string Runtime = "Godot.NET 4.6.1 / C#";
    public const int MigrationVersion = 1;
}
