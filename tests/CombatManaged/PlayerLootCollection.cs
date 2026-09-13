using Earthward.Combat;
using Earthward.Domain;
using Godot;

/// <summary>Headless player reviews reserve world pickups; elapsed UI time completes their real flights.</summary>
internal sealed class PlayerLootCollection(Battlefield battle)
{
    private readonly DataMap _received = new();
    private long _clicks, _arrivals, _failures;

    public int Review()
    {
        int clicked = 0;
        foreach (var pickup in battle.LootPickups.Where(row => row.S("phase") == "world").ToArray())
            if (battle.BeginLootFlight(pickup.L("uid"), new Vector2(.5f, .5f))) clicked++;
        _clicks += clicked;
        return clicked;
    }

    public void Advance(double elapsed)
    {
        foreach (var receipt in battle.AdvanceLootFlights(elapsed))
        {
            if (!receipt.B("ok")) { _failures++; continue; }
            _arrivals++;
            string currency = receipt.S("currency");
            _received[currency] = _received.N(currency) + receipt.N("amount");
        }
    }

    public DataMap Report() => new()
    {
        ["clicks"] = _clicks, ["arrivals"] = _arrivals, ["failed_arrivals"] = _failures,
        ["received"] = _received.DeepClone(),
        ["world_items"] = battle.LootPickups.Count(row => row.S("phase") == "world"),
        ["flying_items"] = battle.LootPickups.Count(row => row.S("phase") == "flying")
    };
}
