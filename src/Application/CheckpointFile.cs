using System;
using System.IO;
using System.Text;
using Earthward.Domain;
namespace Earthward.Application;

/// <summary>Persistence for already structurally validated immutable checkpoint bytes. No scene/state access.</summary>
public static class CheckpointFile
{
    public const int MaximumBytes = 256 * 1024 * 1024;
    public static byte[] Serialize(DataMap checkpoint)
    {
        byte[] bytes=checkpoint.ToJsonBytes();
        if(bytes.Length>MaximumBytes)throw new InvalidDataException("Checkpoint exceeds the256MiB safety limit.");
        return bytes;
    }
    public static byte[] WrapWaveStart(DataMap record,byte[] checkpoint)
    {
        var header=new DataMap();foreach(var(key,value)in record)if(key!="checkpoint")header[key]=value;
        string json=header.ToJson();byte[] prefix=Encoding.UTF8.GetBytes(json[..^1]+",\"checkpoint\":");
        long total=(long)prefix.Length+checkpoint.Length+1;if(total>MaximumBytes)throw new InvalidDataException("Wave checkpoint exceeds the256MiB safety limit.");
        var bytes=new byte[(int)total];Buffer.BlockCopy(prefix,0,bytes,0,prefix.Length);Buffer.BlockCopy(checkpoint,0,bytes,prefix.Length,checkpoint.Length);bytes[^1]=(byte)'}';return bytes;
    }
    public static bool WriteVerified(string path,byte[] validatedContents)
    {
        if(validatedContents.Length==0||validatedContents.Length>MaximumBytes)throw new InvalidDataException("Invalid checkpoint byte length.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary=path+".tmp",backup=path+".previous";
        try
        {
            using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
            {stream.Write(validatedContents);stream.Flush(true);}
            // Check the actual flushed file, not a second object decode: structural validation already passed.
            using(var verify=new FileStream(temporary,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(verify.Length!=validatedContents.Length)return false;
                var buffer=new byte[64*1024];int offset=0,count;
                while((count=verify.Read(buffer,0,buffer.Length))>0)
                {if(!buffer.AsSpan(0,count).SequenceEqual(validatedContents.AsSpan(offset,count)))return false;offset+=count;}
                if(offset!=validatedContents.Length)return false;
            }
            if(File.Exists(path))File.Replace(temporary,path,backup,true);else File.Move(temporary,path);
            return true;
        }
        finally{try{if(File.Exists(temporary))File.Delete(temporary);}catch(IOException){}catch(UnauthorizedAccessException){}}
    }
}
