using Godot;
using System.Text.RegularExpressions;

namespace Earthward.Application;

public partial class Main
{
    // Official release templates forbid positional scene overrides. Validation is an explicit game
    // user argument, and is available only in a publish that actually includes the test assembly types.
    private static bool _nativeValidationRouted;
    private bool TryRunNativeValidationScene()
    {
        if (_nativeValidationRouted) return false;
        const string prefix = "--native-scene=";
        string? request = OS.GetCmdlineUserArgs().FirstOrDefault(argument => argument.StartsWith(prefix, StringComparison.Ordinal));
        if (request == null) return false;
        _nativeValidationRouted = true;
        string scene = request[prefix.Length..];
        SetProcess(false);
        SetProcessInput(false);
        bool includesTests = typeof(Main).Assembly.GetType("Earthward.Tests.ManagedPlayerChecks", false) != null;
        bool valid = includesTests && Regex.IsMatch(scene, @"^res://tests/managed_[a-z0-9_]+\.tscn$", RegexOptions.CultureInvariant) && ResourceLoader.Exists(scene);
        Callable.From(() =>
        {
            if (!valid)
            {
                GD.PrintErr("Native validation requires IncludeNativeTests=true and an existing managed test scene.");
                GetTree().Quit(2);
                return;
            }
            Error result = GetTree().ChangeSceneToFile(scene);
            if (result != Error.Ok)
            {
                GD.PrintErr("Failed to enter native validation scene: " + result);
                GetTree().Quit(2);
            }
        }).CallDeferred();
        return true;
    }
}