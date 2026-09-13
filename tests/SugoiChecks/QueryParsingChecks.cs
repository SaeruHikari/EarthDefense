using Sugoi.Data;

internal static class QueryParsingChecks
{
    private struct Position { }
    private struct Velocity { }
    private struct Shared { }

    internal static void Run()
    {
        using var runtime = new EcsRuntime();
        var position = runtime.Types.Register(new ComponentRegistration<Position>
            { Id = new("39acb512-1ec5-4a57-b8d6-d6bca9556911"), Name = "Pilot.Position0" });
        var velocity = runtime.Types.Register(new ComponentRegistration<Velocity>
            { Id = new("39acb512-1ec5-4a57-b8d6-d6bca9556912"), Name = "Pilot::Velocity" });
        var shared = runtime.Types.Register(new ComponentRegistration<Shared>
            { Id = new("39acb512-1ec5-4a57-b8d6-d6bca9556913"), Name = "Physics.Shared0" });
        using var world = runtime.CreateWorld();
        var plain = world.Create(new EntityType(position));
        var moving = world.Create(new EntityType(position, velocity));
        var provider = world.Create(new EntityType(shared));
        var sharedEntity = world.Create(new EntityType([position], [provider]));

        Check(QueryParser.TryParse(world, " [inout] <seq> Pilot.Position0, [in] <par> ?Pilot::Velocity, [has] !Physics.Shared0 ", out var parsed, out var error), error.ToString());
        Check(parsed!.Terms.Count == 3, "all DSL terms retained");
        Check(parsed.Terms[0].Reads && parsed.Terms[0].Writes && parsed.Terms[0].Sequence == QuerySequence.Sequential, "inout seq access");
        Check(parsed.Terms[1].Reads && !parsed.Terms[1].Writes && parsed.Terms[1].Selection == QuerySelection.Optional, "optional parallel read");
        Check(parsed.Terms[2].IsFilterOnly && !parsed.Terms[2].Reads && !parsed.Terms[2].Writes, "negative has only filters");
        using (var query = world.CreateQuery("[in]Pilot.Position0,!Pilot::Velocity"))
        {
            Check(query.Terms.Count == 2, "parsed query owns access metadata");
            Check(query.Matches(plain) && !query.Matches(moving), "required/excluded matching");
        }
        using (var query = world.CreateQuery("[in]$Physics.Shared0"))
        {
            Check(query.Matches(sharedEntity) && !query.Matches(plain), "bare shared selector has required semantics");
            Check(query.Terms[0].Shared && query.Terms[0].Sequence == QuerySequence.Sequential, "shared always sequential/read-only");
        }
        using (var query = world.CreateQuery("$!Physics.Shared0"))
            Check(!query.Matches(sharedEntity) && query.Matches(plain), "excluded shared selector");
        using (var query = world.CreateQuery("$?Physics.Shared0"))
            Check(query.Matches(sharedEntity) && query.Matches(plain), "optional shared selector does not filter");

        Check(QueryParser.TryParse(world, "<unseq>Pilot::Velocity", out parsed, out error), error.ToString());
        Check(parsed!.Terms[0].Selection == QuerySelection.Optional && parsed.Terms[0].Sequence == QuerySequence.Unsequenced, "unseq implies optional without obsolete rand alias");
        Check(QueryParser.TryParse(world, "[out]Pilot.Position0,[atomic]?Pilot::Velocity", out parsed, out error), error.ToString());
        Check(parsed!.Terms[0].Writes && !parsed.Terms[0].Reads && parsed.Terms[0].Phase == 0, "out writes phase zero");
        Check(parsed.Terms[1].Atomic && parsed.Terms[1].Writes, "atomic intent survives parsing");

        world.MakeAlias("Integrate", position);
        Check(QueryParser.TryParse(world, "[inout]Integrate", out parsed, out error), error.ToString());
        Check(parsed!.Terms[0].Type == position && parsed.Terms[0].Phase > 0, "alias resolves type and writer phase");
        using (var general = world.CreateQuery("[inout]Integrate"))
        using (var specific = world.CreateQuery("[inout]Integrate,[has]Pilot::Velocity"))
        {
            Check(general.Matches(plain) && !general.Matches(moving), "same-phase specific writer excludes its group from general writer");
            Check(specific.Matches(moving), "specific alias writer retains its match");
        }
        using (var all = world.CreateQuery("  ")) Check(all.Count == world.EntityCount, "empty description matches all alive entities");

        foreach (var invalid in new[]
        {
            "[in", "[bad]Pilot.Position0", "<seq", "<rand>Pilot.Position0", "|Pilot.Position0",
            "Pilot.Position0'", "[out]Integrate", "[inout]$Physics.Shared0", "<unseq>!Pilot.Position0",
            "Pilot.Position0,", ",Pilot.Position0", "Pilot.Position0,,Pilot::Velocity", "Unknown", "[in]", "$"
        })
        {
            Check(!world.TryCreateQuery(invalid, out var failed, out error) && failed is null, "reject invalid DSL: " + invalid);
            Check(error.Position >= 0 && error.Position <= invalid.Length && !string.IsNullOrEmpty(error.Message), "located diagnostic: " + invalid);
        }
        const string spacedError = " [in]    <rand> Pilot.Position0";
        Check(!QueryParser.TryParse(world, spacedError, out _, out error) && error.Position == spacedError.IndexOf("rand", StringComparison.Ordinal), "error offsets refer to original input despite whitespace stripping");
        Check(!QueryParser.TryParse(world, null, out _, out error), "null input is a parse failure");
        Console.WriteLine("PASS query DSL: access metadata, selectors, shared reads, aliases/phases, validation and original source locations");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Query parser: " + message);
    }
}
