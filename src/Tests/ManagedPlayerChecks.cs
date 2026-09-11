using Godot;
using System.Diagnostics;
using Earthward.Application;
using Earthward.Domain;

namespace Earthward.Tests;

/// <summary>Runs only in an isolated native-validation profile, through the official template player.</summary>
public partial class ManagedPlayerChecks : Node
{
    public override void _Ready()
    {
        int failures = 0;
        void Check(bool value, string label) { if (!value) { failures++; GD.PrintErr("MANAGED_PLAYER_FAIL: " + label); } }
        Check(ProjectSettings.GlobalizePath("user://").Contains("runtime-tests", StringComparison.OrdinalIgnoreCase), "isolated profile");
        bool godotUnoptimized = typeof(Vector3).Assembly.GetCustomAttributes(typeof(DebuggableAttribute), false).OfType<DebuggableAttribute>().Any(a => a.IsJITOptimizerDisabled);
        bool gameUnoptimized = typeof(Main).Assembly.GetCustomAttributes(typeof(DebuggableAttribute), false).OfType<DebuggableAttribute>().Any(a => a.IsJITOptimizerDisabled);
        Check(!OS.HasFeature("editor"), "running a standalone template, not the editor binary");
        Check(!godotUnoptimized && !gameUnoptimized, "both managed assemblies permit JIT optimization");
        Check(typeof(Vector3).Assembly.Location.Contains("data_Earthward_windows_x86_64"), "GodotSharp comes from the ordinary published data directory");
        Check(Godot.FileAccess.FileExists("res://project.godot") && Godot.FileAccess.FileExists("res://data/domain/patrol_bases.csv"), "current source project and CSV are accessible");
        var scene = GD.Load<PackedScene>("res://main.tscn");
        var main = scene?.Instantiate();
        Check(main is Main, "source main scene resolves its compiled native C# script");
        main?.Free();
        var result = new DataMap { ["godotsharp_jit_disabled"] = godotUnoptimized, ["earthward_jit_disabled"] = gameUnoptimized, ["godotsharp_path"] = typeof(Vector3).Assembly.Location, ["earthward_path"] = typeof(Main).Assembly.Location, ["project"] = ProjectSettings.GlobalizePath("res://"), ["template"] = !OS.HasFeature("editor"), ["failures"] = failures };
        GD.Print("MANAGED_PLAYER_PROBE: " + result.ToJson());
        GD.Print($"MANAGED_PLAYER_CHECKS: 6 checks / {failures} failures");
        GetTree().Quit(failures == 0 ? 0 : 1);
    }
}