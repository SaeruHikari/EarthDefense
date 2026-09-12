using Earthward.Domain;
using Earthward.Combat;
using Earthward.Application;
using Godot;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

CatalogData.Configure(Path.GetFullPath("data/domain"));
int passed=0,failed=0;
void Check(bool ok,string label){if(ok)passed++;else{failed++;Console.Error.WriteLine("CHECKPOINT_FAIL "+label);}}
var timings=new DataMap();
T Measure<T>(string label,Func<T> action){var watch=Stopwatch.StartNew();T result=action();timings[label]=watch.Elapsed.TotalMilliseconds;return result;}
long DecodeVisits(object? encoded)
{
    if(encoded is List<object?> list)return 1+list.Sum(DecodeVisits);
    if(encoded is DataMap map)
    {
        if(map.S("@") is "map")return 1+map.List("v").Cast<List<object?>>().Sum(p=>DecodeVisits(p[0])+DecodeVisits(p[1]));
        if(map.S("@") is "basis")return 1+map.List("v").Sum(DecodeVisits);
    }
    return 1;
}
string folder=Path.GetFullPath(Path.Combine(".runtime-tests","large-checkpoint-"+Guid.NewGuid().ToString("N")));Directory.CreateDirectory(folder);
var game=new DefenseState();game.UnlockAllTechnologyCheat();game.SetCombatSetting("interceptor_factory_capacity",100);game.SetCombatSetting("factory_capacity_multiplier",100);
var surface=new Surface();var battle=new Battlefield(game,surface);var campaign=new DefenseCampaignDirector(game,battle);
var first=battle.SpawnFactoryDrone(battle.Factories[3],0);first["state"]="patrol";first["launch_age"]=CombatScale.LaunchDuration;first["space_position"]=Vector3.Back*(float)(CombatScale.EarthRadius+CombatScale.DroneAltitude);first["flight_age"]=8d;
for(int i=1;i<5000;i++){var drone=first.DeepClone();drone["uid"]=(long)i+1;drone["patrol_slot"]=i;drone["_actor_order"]=i;battle.Drones.Add(drone);}
typeof(Battlefield).GetField("_nextUid",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(battle,100000L);
battle.StartWave();
var stats=battle.DroneWeaponStats(first);var source=(DataMap)typeof(Battlefield).GetMethod("WeaponSource",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(battle,new object?[]{first,stats})!;
for(int i=0;i<3000;i++)battle.MakeShot(first.Vector3("space_position"),first.Vector3("space_position")+Vector3.Right*4,10+(i%7),i%3==0,0,stats,new DataMap(source));
var snapshot=Measure("capture_encode_ms",battle.SerializeCombatSnapshot);long visits=DecodeVisits(snapshot.Value("payload"));
Check(visits>600000&&visits<CombatSnapshotCodec.MaximumDecodedNodes,"representative legal5000+3000 exceeds original budget");
Check(Measure("validate_ms",()=>Battlefield.ValidateCombatSnapshot(snapshot)),"complete live snapshot validates");
Check(CombatSnapshotCodec.TryDecode(snapshot.Value("payload"),out var decoded)&&decoded is DataMap,"large decode succeeds");
var state=(DataMap)decoded!;Check(state.List("_drones").Count==5000&&state.List("_shots").Count==3000,"all entities included");
Check(state.List("_drones").Cast<DataMap>().All(d=>!d.ContainsKey("_actor_order")),"derived actor index omitted from new snapshots");
Check(CombatSnapshotCodec.IsNumericKey(state.Map("_factories"),"3"),"numeric legacy factory-key identity retained");
var checkpoint=new DataMap{["version"]=3L,["game"]=game.Serialize(),["started"]=true,["expedition_battle"]=campaign.SerializeWaveStart(),["slots"]=new List<object?>{"mine","solar","","interceptor"},["site_directions"]=new List<object?>(),["destroyed_fronts"]=new List<object?>()};
var record=new DataMap{["version"]=1L,["run_id"]=game.RunId,["wave"]=game.Wave,["origin"]="wave_start",["checkpoint"]=checkpoint};
byte[] bytes=Measure("json_utf8_once_ms",()=>CheckpointFile.Serialize(checkpoint));byte[] wrapped=Measure("wrap_without_reserialize_ms",()=>CheckpointFile.WrapWaveStart(record,bytes));
var parsed=Measure("read_parse_ms",()=>DataMap.Parse(Encoding.UTF8.GetString(bytes)));var wrappedMap=DataMap.Parse(Encoding.UTF8.GetString(wrapped));
Check(DataMap.Equivalent(parsed,wrappedMap.Map("checkpoint"))&&wrappedMap.Count==5,"wave file embeds exact same checkpoint");
var restoredGame=new DefenseState();Check(restoredGame.Restore(parsed.Map("game")),"domain run survives complete JSON boundary");
var restoredBattle=new Battlefield(restoredGame,surface);Check(Measure("battle_restore_ms",()=>restoredBattle.RestoreCombatSnapshot(parsed.Map("expedition_battle").Map("earth"))),"5000+3000 complete battle restores");
Check(restoredBattle.Drones.Count==5000&&restoredBattle.Shots.Count==3000&&restoredBattle.Random.State==battle.Random.State,"roster projectile count and RNG exact");
Check(DataMap.Equivalent(snapshot,restoredBattle.SerializeCombatSnapshot()),"authoritative snapshot roundtrip exact without derived index drift");
string path=Path.Combine(folder,"checkpoint.json");
Check(Measure("write_verify_replace_ms",()=>CheckpointFile.WriteVerified(path,bytes)),"large verified atomic write");
Check(File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes),"flushed bytes equal validated input");
var replacement=Encoding.UTF8.GetBytes("{\"replacement\":true}");Check(CheckpointFile.WriteVerified(path,replacement),"atomic replacement succeeds");
Check(File.ReadAllBytes(path+".previous").AsSpan().SequenceEqual(bytes),"previous valid snapshot retained");
string digest=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
using(var locked=new FileStream(path,FileMode.Open,System.IO.FileAccess.Read,FileShare.Read))
{
    bool rejected=false;try{CheckpointFile.WriteVerified(path,bytes);}catch(IOException){rejected=true;}Check(rejected,"locked primary refuses replace");
}
Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))==digest&&!File.Exists(path+".tmp"),"failed replace preserves primary and cleans temporary");
Directory.CreateDirectory(path+".tmp");bool denied=false;try{CheckpointFile.WriteVerified(path,bytes);}catch(Exception e)when(e is IOException or UnauthorizedAccessException){denied=true;}Check(denied&&Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))==digest,"failed temp creation never touches valid primary");Directory.Delete(path+".tmp");
object deep=true;for(int i=0;i<CombatSnapshotCodec.MaximumDepth+2;i++)deep=new List<object?>{deep};Check(!CombatSnapshotCodec.TryDecode(deep,out _),"depth safety limit retained");
Check(!CombatSnapshotCodec.TryDecode(new string('x',CombatSnapshotCodec.MaximumStringLength+1),out _),"unbounded string rejected");
Check(!CombatSnapshotCodec.TryDecode(double.PositiveInfinity,out _),"nonfinite rejected");
Check(!CombatSnapshotCodec.TryDecode(new DataMap{["@"]="int",["v"]=new List<object?>{"01"}},out _),"noncanonical integer rejected");
Check(!CombatSnapshotCodec.TryDecode(new DataMap{["@"]="map",["v"]=new List<object?>{new List<object?>{"x",1L},new List<object?>{"x",2L}}},out _),"duplicate map keys rejected");
// Queue writes only owned immutable snapshots; final order and failure behavior do not depend on scheduling speed.
string queuePath=Path.Combine(folder,"queued.json"),wavePath=Path.Combine(folder,"queued-wave.json");var queue=new CheckpointWriteQueue();
for(int i=1;i<=12;i++)queue.EnqueueOwned("queue-run",1,queuePath,new(){["run_id"]="queue-run",["wave"]=1L,["sequence"]=(long)i});
var nextOpening=new DataMap{["run_id"]="queue-run",["wave"]=2L,["sequence"]=13L};var nextRecord=new DataMap{["version"]=1L,["run_id"]="queue-run",["wave"]=2L,["origin"]="wave_start",["checkpoint"]=nextOpening};
queue.EnqueueOwned("queue-run",2,queuePath,nextOpening,wavePath,nextRecord);
queue.EnqueueOwned("queue-run",2,queuePath,new(){["run_id"]="queue-run",["wave"]=2L,["sequence"]=14L});queue.Flush();
Check(DataMap.Parse(File.ReadAllText(queuePath)).L("sequence")==14,"queued latest ordinary state wins serially");Check(DataMap.Equivalent(DataMap.Parse(File.ReadAllText(wavePath)).Map("checkpoint"),nextOpening),"queued ordinary never overwrites exact wave-start sidecar");
Check(queue.TakeResults().All(r=>r.MainSaved&&(!r.IsWaveStart||r.WaveSaved))&&!queue.IsBusy,"flush waits for all pending writes");
string queueDigest=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(queuePath)));
using(var locked=new FileStream(queuePath,FileMode.Open,System.IO.FileAccess.Read,FileShare.Read))
{queue.EnqueueOwned("queue-run",2,queuePath,new(){["sequence"]=15L});queue.Flush();Check(queue.TakeResults().Any(r=>!r.MainSaved&&r.Error.Length>0),"background failures returned for main-thread notice");}
Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(queuePath)))==queueDigest,"failed background write preserves valid record");
queue.Flush();queue.EnqueueOwned("new-run",1,queuePath,new(){["run_id"]="new-run",["wave"]=1L,["sequence"]=16L});queue.Flush();
Check(DataMap.Parse(File.ReadAllText(queuePath)).S("run_id")=="new-run"&&queue.TakeResults().All(r=>r.RunId=="new-run"),"flushed run switch cannot be overwritten by old jobs");
// Retry retains the separate permanent profile and its idempotent reward claims.
string profile=Path.Combine(folder,"perks.json");var meta=new FactoryPerks();Check(meta.LoadProfile(profile)&&meta.ResetRunSites(game.RunId),"isolated permanent profile");
var reward=meta.ClaimAlienChip(game.RunId,"earth:wave:1:enemy:3");string metaHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(profile)));
Check(reward.B("claimed")&&!meta.ClaimAlienChip(game.RunId,"earth:wave:1:enemy:3").B("claimed")&&metaHash==Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(profile))),"same aircraft death cannot replay permanent chip claim");
var report=new DataMap{["passes"]=passed,["failures"]=failed,["drones"]=5000,["projectiles"]=3000,["decoded_visits"]=visits,["checkpoint_bytes"]=bytes.Length,["timings_ms"]=timings,["scope"]="one functional save/load; no simulation ticks or performance stress loop"};File.WriteAllText("artifacts/large-checkpoint-report.json",report.ToJson(true));Console.WriteLine(report.ToJson());System.Environment.ExitCode=failed==0?0:1;

sealed class Surface:ICombatSurface
{
    private readonly List<DataMap> _sites=new(){new(){["site_id"]=3L,["kind"]="interceptor",["normal"]=Vector3.Back,["launch_position"]=Vector3.Back*(float)(CombatScale.EarthRadius+.108),["launch_direction"]=Vector3.Right}};
    public IReadOnlyList<DataMap> GetFactorySites()=>_sites;
    public IReadOnlyList<Vector3> GetOccupiedSurfaceNormals()=>new[]{Vector3.Back};
    public Vector3 SurfaceToSpace(Vector3 normal,double altitude)=>normal.Normalized()*(float)(CombatScale.EarthRadius+altitude);
    public Vector3 SpaceToSurface(Vector3 position)=>position.Normalized();
}
