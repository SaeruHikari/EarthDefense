using Earthward.Domain;
using Godot;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("fr-FR");
var cases=new Dictionary<string,DataMap>();
foreach(string name in new[]{"deep-technology.json","airframes.json","perks.json","economy.json","expedition.json"})
{
    CatalogData.Configure(Path.GetFullPath("data/domain"));
    cases[name]=CatalogData.Load(name).DeepClone();
}
cases["scalars"]=new(){["z"]=new List<object?>{null,true,false,"comma, quote\" slash/ line\n<&\u4e2d\u6587\u2028\ud83d\ude80"},["a"]=new DataMap{["f"]=(float).1,["d"]=.1,["min"]=double.Epsilon,["max"]=double.MaxValue,["negative_zero"]=-0d,["small_exp"]=1e-8,["big_exp"]=1e30,["long"]=long.MaxValue,["ulong"]=ulong.MaxValue,["decimal"]=.1234567890123456789012345678m,["empty"]=new DataMap()}};
var basis=new Basis(new Vector3(.1f,.2f,.3f),new Vector3(.4f,.5f,.6f),new Vector3(.7f,.8f,.9f));
cases["godot"]=new(){["v2"]=new Vector2(.1f,-.2f),["v3"]=new Vector3(.1f,3.5f,1000),["v4"]=new Vector4(1,2,3,4),["color"]=new Color(.1f,.2f,.3f,.4f),["q"]=new Quaternion(.1f,.2f,.3f,.4f),["b"]=basis,["t"]=new Transform3D(basis,new Vector3(.1f,.2f,.3f))};
string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
string fixture="tests/JsonManaged/reference-sha.json";
if(args.Contains("--capture")){var hashes=new DataMap();foreach(var(name,data)in cases){hashes[name]=Hash(data.ToJson());hashes[name+"/pretty"]=Hash(data.ToJson(true));}File.WriteAllText(fixture,hashes.ToJson(true));Console.WriteLine("FROZEN_JSON_HASHES "+hashes.Count);return;}
var reference=DataMap.Parse(File.ReadAllText(fixture));int checks=0,failed=0;
foreach(var(name,data)in cases)foreach(bool pretty in new[]{false,true})
{checks++;string key=name+(pretty?"/pretty":"");if(Hash(data.ToJson(pretty))!=reference.S(key)){failed++;Console.Error.WriteLine("JSON_SHA_FAIL "+key);}}
foreach(var data in cases.Values)foreach(bool pretty in new[]{false,true}){checks++;if(!data.ToJsonBytes(pretty).AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(data.ToJson(pretty)))){failed++;Console.Error.WriteLine("JSON_UTF8_MISMATCH");}}
Console.WriteLine($"JSON_COMPAT {checks-failed} PASS / {failed} FAIL");System.Environment.ExitCode=failed==0?0:1;
