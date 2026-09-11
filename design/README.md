# Historical design records

These documents record the game's earlier design reviews and implementation plans.
References to GDScript filenames and line numbers describe the versions reviewed at
that time. The active game is now implemented in `src/` with C#; unused GDScript
runtime code, tests and one-time migration exporters were removed in 1.27.

The original pre-migration source remains in protected development backups. Current
runtime behavior, CSV balance data, launch commands and validation entrypoints are
described by the project README and `tools/test_managed.ps1`.

Current implementation records:

- [1.27 implementation and validation status](incremental-combat/implementation-1.27.md): current C# source player, 64 CSV tables, 299 independent technology nodes, adjacent dependency layout, patrol rebalance, and verified versus pending checks.
- [README preserved before the 1.27 update](incremental-combat/README-before-127.md): historical behavior and launch notes, not the current runtime contract.