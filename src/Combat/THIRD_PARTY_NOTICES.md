# Algorithm provenance

`CombatRandom.cs` is a managed implementation of the PCG32 XSH-RR state transition and Godot 4.6 float sampling convention. It is verified against independently exported Godot `RandomNumberGenerator` values.

- Godot reference: https://github.com/godotengine/godot/blob/4.6-stable/core/math/random_pcg.h (Godot Engine contributors, MIT license).
- PCG reference: https://github.com/godotengine/godot/blob/4.6-stable/thirdparty/misc/pcg.cpp (M. E. O'Neill, PCG Random Number Generation, Apache 2.0 license).
- Godot license: https://github.com/godotengine/godot/blob/4.6-stable/LICENSE.txt
- PCG license: https://www.pcg-random.org/download.html

GodotSharp is consumed as the engine's existing .NET dependency; its math structs are not duplicated as native simulation objects.
