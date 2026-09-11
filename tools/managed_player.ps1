# Standard Godot .NET release-template player beside project.godot, reading current source resources.
# The official binary is cached under .runtime and copied beside the source project; the editor installation is untouched.
$ErrorActionPreference = 'Stop'
$script:ManagedPlayerVersion = '4.6.1'
$script:ManagedPlayerArchive = 'https://github.com/godotengine/godot-builds/releases/download/4.6.1-stable/Godot_v4.6.1-stable_mono_export_templates.tpz'
$script:ManagedPlayerArchiveSize = 1169366602L
$script:ManagedPlayerArchiveSha256 = '5792661ed089550357e0e10954af695846be0a32ed7494fa40a0009e3b5a3ea9'

function Initialize-EarthwardTemplateReader {
    if ('EarthwardTemplateReader' -as [type]) { return }
    Add-Type -TypeDefinition @"
using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
public sealed class EarthwardTemplateEntry {
    public string Name;
    public uint Crc32;
    public long Bytes;
    public long CompressedBytes;
}
public static class EarthwardTemplateReader {
    static ushort U16(byte[] b, int o) { return BitConverter.ToUInt16(b,o); }
    static uint U32(byte[] b, int o) { return BitConverter.ToUInt32(b,o); }
    static byte[] Range(string url, long first, long last) {
        HttpWebRequest r=(HttpWebRequest)WebRequest.Create(url);
        r.UserAgent="Earthward-development-player/1.27";
        r.Timeout=60000; r.ReadWriteTimeout=60000; r.AllowAutoRedirect=true;
        r.AddRange(first,last);
        using(HttpWebResponse response=(HttpWebResponse)r.GetResponse()) {
            string expected="bytes "+first+"-"+last+"/";
            if(response.StatusCode!=HttpStatusCode.PartialContent || !(response.Headers["Content-Range"]??"").StartsWith(expected,StringComparison.Ordinal))
                throw new IOException("Official template server did not honor the requested byte range; refusing an accidental 1.17GB download.");
            int count=checked((int)(last-first+1)); byte[] bytes=new byte[count]; int read=0;
            using(Stream input=response.GetResponseStream()) {
                while(read<count) { int n=input.Read(bytes,read,count-read); if(n<=0) throw new EndOfStreamException("Incomplete template range"); read+=n; }
            }
            return bytes;
        }
    }
    static uint[] CrcTable() {
        uint[] table=new uint[256];
        for(uint n=0;n<256;n++) { uint c=n; for(int k=0;k<8;k++)c=(c&1)!=0?0xedb88320U^(c>>1):c>>1; table[n]=c; }
        return table;
    }
    public static EarthwardTemplateEntry Extract(string url,long archiveSize,string destination) {
        byte[] tail=Range(url,Math.Max(0,archiveSize-65557),archiveSize-1); int end=-1;
        for(int i=tail.Length-22;i>=0;i--) if(U32(tail,i)==0x06054b50U && i+22+U16(tail,i+20)==tail.Length){end=i;break;}
        if(end<0 || U16(tail,end+4)!=0 || U16(tail,end+6)!=0)throw new IOException("Unsupported template ZIP directory");
        long size=U32(tail,end+12), offset=U32(tail,end+16);
        byte[] directory=Range(url,offset,offset+size-1);
        for(int p=0;p+46<=directory.Length;) {
            if(U32(directory,p)!=0x02014b50U)throw new IOException("Invalid template ZIP central entry");
            int names=U16(directory,p+28),extras=U16(directory,p+30),comments=U16(directory,p+32);
            string name=Encoding.UTF8.GetString(directory,p+46,names);
            if(name=="templates/windows_release_x86_64.exe") {
                ushort flags=U16(directory,p+8),method=U16(directory,p+10);
                uint crc=U32(directory,p+16);long compressed=U32(directory,p+20),uncompressed=U32(directory,p+24),headerOffset=U32(directory,p+42);
                if((flags&1)!=0 || (method!=0 && method!=8) || uncompressed<10000000 || uncompressed>300000000)throw new IOException("Unexpected Windows .NET template entry");
                byte[] header=Range(url,headerOffset,headerOffset+29);
                if(U32(header,0)!=0x04034b50U)throw new IOException("Invalid template ZIP local header");
                long start=headerOffset+30+U16(header,26)+U16(header,28);
                byte[] payload=Range(url,start,start+compressed-1);uint value=0xffffffffU;long total=0;uint[] table=CrcTable();
                using(MemoryStream packed=new MemoryStream(payload,false))
                using(Stream input=method==8?(Stream)new DeflateStream(packed,CompressionMode.Decompress):packed)
                using(FileStream output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                    byte[] buffer=new byte[81920];int n;
                    while((n=input.Read(buffer,0,buffer.Length))>0) {
                        total+=n;if(total>uncompressed)throw new IOException("Template exceeds declared length");
                        for(int i=0;i<n;i++)value=table[(value^buffer[i])&255]^(value>>8);
                        output.Write(buffer,0,n);
                    }
                }
                if(total!=uncompressed || (value^0xffffffffU)!=crc)throw new IOException("Official Windows .NET template CRC/length verification failed");
                return new EarthwardTemplateEntry {Name=name,Crc32=crc,Bytes=total,CompressedBytes=compressed};
            }
            p+=46+names+extras+comments;
        }
        throw new FileNotFoundException("Windows x86_64 release entry is missing from the official .NET template archive");
    }
}
"@ -ReferencedAssemblies 'System.dll','System.IO.Compression.dll'
}

function Get-EarthwardManagedPlayer {
    param([Parameter(Mandatory=$true)][string]$ProjectRoot)
    $root = [IO.Path]::GetFullPath($ProjectRoot)
    $runner = Join-Path $root '.runtime\managed-player'
    $player = Join-Path $runner 'EarthwardPlayer.exe'
    $manifestPath = Join-Path $runner 'SOURCE.json'
    [IO.Directory]::CreateDirectory($runner) | Out-Null
    $valid = $false
    if ((Test-Path -LiteralPath $player -PathType Leaf) -and (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        try {
            $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $valid = $manifest.source -eq $script:ManagedPlayerArchive -and $manifest.archive_sha256 -eq $script:ManagedPlayerArchiveSha256 -and $manifest.sha256 -eq (Get-FileHash -LiteralPath $player -Algorithm SHA256).Hash.ToLowerInvariant()
        } catch { $valid = $false }
    }
    if (-not $valid) {
        Initialize-EarthwardTemplateReader
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        $temporary = $player + '.download'
        if (Test-Path -LiteralPath $temporary -PathType Leaf) { Remove-Item -LiteralPath $temporary }
        try {
            Write-Host 'Fetching only the Windows x64 .NET release player from the official Godot template archive...'
            $entry = [EarthwardTemplateReader]::Extract($script:ManagedPlayerArchive,$script:ManagedPlayerArchiveSize,$temporary)
            $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($temporary).FileVersion
            if ($version -notlike ($script:ManagedPlayerVersion + '*')) { throw ('Unexpected Godot player version: ' + $version) }
            $hash = (Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash.ToLowerInvariant()
            $metadata = [ordered]@{version=$script:ManagedPlayerVersion;source=$script:ManagedPlayerArchive;archive_bytes=$script:ManagedPlayerArchiveSize;archive_sha256=$script:ManagedPlayerArchiveSha256;archive_hash_scope='Official GitHub asset digest; partial download validated by central-entry CRC32 and file version';entry=$entry.Name;entry_crc32=$entry.Crc32.ToString('x8');bytes=$entry.Bytes;downloaded_entry_bytes=$entry.CompressedBytes;sha256=$hash;retrieved_utc=[DateTime]::UtcNow.ToString('o')}
            Move-Item -LiteralPath $temporary -Destination $player -Force
            [IO.File]::WriteAllText($manifestPath,($metadata | ConvertTo-Json),[Text.UTF8Encoding]::new($false))
        } finally {
            if (Test-Path -LiteralPath $temporary -PathType Leaf) { Remove-Item -LiteralPath $temporary }
        }
    }
    $sourcePlayer = Join-Path $root 'EarthwardPlayer.exe'
    if (-not (Test-Path -LiteralPath $sourcePlayer -PathType Leaf) -or (Get-FileHash -LiteralPath $sourcePlayer -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $player -Algorithm SHA256).Hash) {
        Copy-Item -LiteralPath $player -Destination $sourcePlayer -Force
    }
    return $sourcePlayer
}

function Publish-EarthwardManagedPlayer {
    param([Parameter(Mandatory=$true)][string]$ProjectRoot,[switch]$IncludeNativeTests)
    $root = [IO.Path]::GetFullPath($ProjectRoot)
    $player = Get-EarthwardManagedPlayer $root
    $destination = Join-Path (Split-Path -Parent $player) 'data_Earthward_windows_x86_64'
    $metadataRoot = Join-Path $root '.runtime\managed-player'
    $publishLog = Join-Path $metadataRoot 'publish.log'
    $projectPublish = Join-Path $root '.godot\mono\publish\x86_64'
    if (Test-Path -LiteralPath $projectPublish) { throw ('An embedded export publish directory would override this source player: ' + $projectPublish + '. Move that generated export cache out before launching the source player.') }
    $lock = New-Object System.Threading.Mutex($false,'Local\EarthwardManagedBuild')
    $acquired = $false
    try {
        $acquired = $lock.WaitOne(60000)
        if (-not $acquired) { throw 'Another managed build is still running.' }
        # The complete self-contained directory is the official non-editor host contract, not an API replacement.
        $arguments = @('publish',(Join-Path $root 'Earthward.csproj'),'-c','ExportRelease','-r','win-x64','--self-contained','true','-o',$destination,'--nologo','-p:ValidationSlice=',('-p:IncludeNativeTests=' + $IncludeNativeTests.IsPresent.ToString().ToLowerInvariant()))
        & dotnet @arguments 2>&1 | Tee-Object -FilePath $publishLog | Out-Host
        if ($LASTEXITCODE -ne 0) { throw ('Current source publish failed. Inspect ' + $publishLog) }
        foreach ($name in @('Earthward.dll','Earthward.deps.json','Earthward.runtimeconfig.json','GodotSharp.dll','hostfxr.dll','hostpolicy.dll','coreclr.dll')) {
            if (-not (Test-Path -LiteralPath (Join-Path $destination $name) -PathType Leaf)) { throw ('Incomplete standard .NET publish output: ' + $name) }
        }
        $metadata = [ordered]@{configuration='ExportRelease';runtime='win-x64';self_contained=$true;include_native_tests=$IncludeNativeTests.IsPresent;project=$root;player=$player;publish=$destination;godotsharp_sha256=(Get-FileHash -LiteralPath (Join-Path $destination 'GodotSharp.dll') -Algorithm SHA256).Hash.ToLowerInvariant();earthward_sha256=(Get-FileHash -LiteralPath (Join-Path $destination 'Earthward.dll') -Algorithm SHA256).Hash.ToLowerInvariant();updated_utc=[DateTime]::UtcNow.ToString('o')}
        [IO.File]::WriteAllText((Join-Path $metadataRoot 'PUBLISH.json'),($metadata | ConvertTo-Json),[Text.UTF8Encoding]::new($false))
    } finally { if ($acquired) { $lock.ReleaseMutex() }; $lock.Dispose() }
    return $player
}
