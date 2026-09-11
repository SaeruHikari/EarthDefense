# Fleet 1.27 combat and rendering handoff

This note records the frozen combat implementation and its evidence. The release owner runs final native player frame measurements after all visual changes; managed tick timings are not game FPS.

## Frozen workload and independent replay

`src/Tests/FleetStressFixture.cs` uses 75 actual factories, 5,000 allied aircraft across all nine physical airframes, two actual perks per frame, and 500 or 1,000 enemies across all eight roles. Enemies retain the original 4,000 HP. Production replacements, real weapons, armor, projectile impacts, losses and damage notifications remain active. Population maintenance occurs outside the timed combat step. Domain source, balance inputs and compiled profiles are frozen explicitly below `tests/CombatManaged/Fixtures`; the stable compiled stats SHA256 is `177e7538d7a686621b6c2ccee97f8790ae4962021a50572ec739ea52baa86644`.

Default `cluster` is the original severe concentrated encounter. Optional `--layout=world` distributes the same 75 factories over a Fibonacci sphere. Each of the 1,000 enemies is placed within 0.8 world lateral units of a real factory, with 13 or 14 enemies per factory. Enemy maintenance uses the current real factory normal and surface frame. This avoids falsely improving load by placing hostiles in empty territory. World-layout geometry/population/engagement-envelope checks: 3,238 passed. These are correctness assertions, not a performance run.

Shared native entry: `tests/managed_fleet_scale.tscn`, parameters `--layout=world --bench-mode=combat --enemies=1000 --fixture-root=D:/MyGame/EarthDefense`. CPU entry: `CombatManaged.dll --stress --layout=world --mode=combat --enemies=1000`. Omit layout to preserve the original cluster setup and output names. Outputs include current/peak aircraft with targets, observed firing lives, observed engaged factories, damage events, losses and profile hash.

The independent baseline program compiles the original unoptimized combat and frozen Domain source; it does not call current combat algorithms. Twelve complete combat snapshot samples across 180 ticks match the optimized implementation, including actor poses, HP, weapons, factories and RNG. Canonicalization only removes rebuilt `_actor_order`, the new cosmetic laser `style`, and clamps the old invalid negative UI-only telegraph value. Actual timers and all beam coordinates/colors/lifetimes stay strict. Rebuild the oracle with `LegacyStressBaseline.csproj`, not the implementation under test.

## Combat changes

- Removed eager world-coordinate fallbacks; cached factory/actor transforms avoid thousands of native accesses per frame.
- Reusable managed point trees provide nearest, weighted and sphere queries with original stable tie ordering. Target assignment, defender selection and guided missiles no longer scan all targets repeatedly.
- Per-assignment armor/configuration caches preserve per-berth overrides, factory home directions, shield changes and planet rotation. Candidate filtering and firepower budgets remain unchanged.
- Healthy-flight work is batched into managed-only chunks. Godot object access, events, shared-index writes, damage, repairs and death actions stay on the main thread. Small encounters remain serial.
- Reduced repeated dictionary cloning, LINQ and transient collider storage. Per-factory fleet capacity is calculated in one allied pass instead of scanning all aircraft for every factory.
- Missile explosion bonus is applied once to explosion damage, including delayed/cluster portions; direct damage, laser damage and self-destruction are not accidentally amplified. C_N1 applies its compiled range multiplier to all targets once.

Selected focused verification: spatial index 18,001; serial/parallel full-state replay 26; all-target coverage 138; fleet occupancy 30; explosion bonus 17; full scale/combat suite previously 112,045. Tests are behavioral evidence, not substitute frame-rate measurements.

## Recorded timings and limits

Managed original Release 5,000 allies + 1,000 hostiles, 40 warmup / 80 measured 60 Hz ticks: combat p50 59.2278 ms, p95 100.0025 ms. Original patrol p50 5.4065 ms, p95 8.9792 ms. Raw files: `artifacts/fleet5000-baseline-combat-1000.json`, `artifacts/fleet5000-baseline-patrol-0.json`.

A sustained 0.06-second simulation step (120 warmup / 300 samples) after assignment caching recorded p50 22.8662 ms / p95 37.9477 ms in `artifacts/fleet5000-assignment-cached006-combat-1000.json`, with the same 222,382 damage events, 3,921 friendly losses, 3,905 replacements and 821 enemy kills as its paired baseline. Later neighborhood profiling reduced that component, but the associated overall capture slowed in unrelated stages; it is not claimed as a reliable total-speed improvement.

Native investigation established that an optimized Earthward assembly alone was insufficient: the editor loaded JIT-unoptimized GodotSharp API assemblies. Parent native captures comparing actual Release API/player are authoritative. The earlier Release API capture `artifacts/fleet127-native-releaseapi-combat-1000.json` recorded combat p50 10.3358 ms / p95 19.116 ms and wall frame p50 18.2485 ms / p95 31.6226 ms. This does **not** establish stable 60 FPS. Final standard-player world/cluster results supersede it, particularly after the new shield and laser effects.

## GDScript cleanup

The dependency audit removed 186 unused `.gd`, their 186 `.gd.uid` companions, and seven obsolete test/migration wrappers. Current executable/scene references to deleted scripts and script UIDs are zero. Twenty-one `.gdshader` files and valid baked model/texture resources remain. Hashes and dependency proof: `artifacts/gdscript-cleanup127-audit.json`. User saves and archived source backups were excluded.

The still-useful cloud-volume texture packer was replaced by `src/Tools/CloudVolumeBake.cs`, `tools/bake_cloud_volume_texture.tscn` and `.ps1`; the Python noise generator remains. The C# packer verifies every saved texture slice byte-for-byte. Use an isolated output for its native test. Root handles final import/NoLegacyScripts checks.

## New visual acceptance scenes

- `tests/managed_atmosphere_direction.tscn`: damage-scaled envelope is unchanged; actual geographic impact is brighter and follows Earth rotation. Shader retains 12 integration samples. No-position calls preserve global breathing. Captures: `atmosphere-direction-idle/global/hit/rotated.png`.
- `tests/managed_unit_shields.tscn`: actual energy ratios 1/.5/.1/0, native brightness decrease, zero-shield removal, eight enemy roles, three bosses, moving/fixed motherships, fitted model bounds and world/surface rotation. Optional `--shield-stress` checks 5,000 shielded hulls with bounded batches. Captures: `unit-shields-ratios/baseline/model-family/fixed-mothership.png`.
- `tests/managed_laser_glow.tscn`: a real laser damage call emits an explicit cosmetic style; one thin HDR ribbon plus two endpoint flares use only two additional batches. Generic support/jammer/tracer beams remain unchanged. Fade, endpoints, camera facing and unchanged exposure are checked. Captures: `laser-glow-baseline/old/new/fade.png`.

Native Release results confirmed by the release owner: laser 19/19, unit shields 19/19, unchanged generic effects 715/715, and original asset contracts 273/273. Shield image contributions at ratios 1/.5/.1/0 were 0.169338 / 0.066589 / 0.017353 / 0.000000, with real rendered pixel comparisons. Laser core/muzzle contributions were 0.614811 / 1.476390; this is local effect contribution, not whole-scene exposure. The generic effect renderer retained a measured 2.63x native-sync improvement in its separate reference fixture.

The first atmosphere direction scene passed 22/22. A subsequent shader-only local frontal-opacity adjustment makes the actual impact more obvious without changing the far-side glow or sample count; its final native rerun is pending the release owner's coordinated performance capture. The two new laser shaders and unit-shield shader use the same lit-pipeline emission path as the pre-existing HDR effects: `ALBEDO=0`, ambient/specular disabled, `EMISSION` output. An initial `unshaded` variant rendered black on this backend and was corrected before the successful native results above.

## Final standard-player measurements

Pending release-owner insertion: same frozen stats hash, actual 5,000 friendly / 1,000 hostile population, both `cluster` and `world`, warmup and sample counts, true wall-frame p50/p95, combat/sync/GPU timing, live firing and damage participation. These final captures include the newly authorized energy shields and laser effects. No final frame-rate claim is inferred from earlier editor/API-only captures.
