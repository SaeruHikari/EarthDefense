using System.Numerics;
using Sugoi.Data;

namespace Sugoi.DefenseSimulation;

internal enum FleetSide : byte { Human, Alien }

[Component("38f134db-044d-48db-bcb1-68f6cb9e3301")]
internal partial struct Position { public Vector3 Value; }

[Component("38f134db-044d-48db-bcb1-68f6cb9e3302")]
internal partial struct Velocity { public Vector3 Value; }

[Component("38f134db-044d-48db-bcb1-68f6cb9e3303")]
internal partial struct Hull { public float Value; }

[Component("38f134db-044d-48db-bcb1-68f6cb9e3304")]
internal partial struct Weapon
{
    public float Damage;
    public float RangeSquared;
    public int FireEverySteps;
}

[Component("38f134db-044d-48db-bcb1-68f6cb9e3305")]
internal partial struct Aircraft
{
    public int Serial;
    public FleetSide Side;
    public Entity Wingman; // Transient during staging; Source Generator provides the Apply remapper.
    public Entity Factory; // Assigned only after factory validation during birth publication.
}

[Component("38f134db-044d-48db-bcb1-68f6cb9e3306")]
internal partial struct Factory
{
    public int Capacity;
    public int ActiveAircraft;
    public int RebuildSteps;
}

[Component("38f134db-044d-48db-bcb1-68f6cb9e3307", Kind = ComponentKind.Tag)]
internal partial struct Human { }

[Component("38f134db-044d-48db-bcb1-68f6cb9e3308", Kind = ComponentKind.Tag)]
internal partial struct Alien { }

[Component("38f134db-044d-48db-bcb1-68f6cb9e3309")]
internal partial struct BirthTicket { public int Value; }
