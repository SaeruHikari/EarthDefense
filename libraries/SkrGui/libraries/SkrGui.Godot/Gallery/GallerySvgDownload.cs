using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using static SkrGui.Gallery.GalleryNative;
namespace SkrGui.Gallery;
public static partial class GallerySvg
{
 private sealed class DownloadEntry {public Utf8StringView Url;public EGallerySvgDownloadState State;public GallerySvgDocument Document=new();public string Error="";public ulong Generation;}
 private sealed class DownloadStore {public readonly object Mutex=new();public List<DownloadEntry> Entries=[];public ulong Generation;}
 private static readonly DownloadStore Store=new();
 private static readonly Lazy<bool> Registered=new(()=>{nng_tcp_register();nng_tls_register();return true;},LazyThreadSafetyMode.ExecutionAndPublication);
 private static DownloadEntry? FindEntry(Utf8StringView url){foreach(var entry in Store.Entries)if(entry.Url==url)return entry;return null;}
 private static DownloadEntry EnsureEntry(Utf8StringView url){var entry=FindEntry(url);if(entry!=null)return entry;entry=new(){Url=new(url.Bytes.ToArray())};Store.Entries.Add(entry);return entry;}
 private static bool NngError(ref string error,string prefix,int rv){error=prefix+": "+GalleryNative.String(nng_strerror(rv));return false;}
 private static bool DownloadSystemHttpsFallback(Utf8StringView url,ref Utf8StringView svg,ref string error)
 {
  if(OperatingSystem.IsWindows()){error="nng does not support HTTPS in this SDK, and no Windows system HTTPS fallback is wired for the temporary gallery.";return false;}
  Process? process;
  try
  {
   var start=new ProcessStartInfo("python3"){UseShellExecute=false,RedirectStandardOutput=true,CreateNoWindow=true};
   start.ArgumentList.Add("-c");start.ArgumentList.Add("from urllib.request import Request, urlopen; import sys; req=Request(sys.argv[1], headers={'User-Agent':'SkrGuiGalleryCommon/0.1'}); sys.stdout.buffer.write(urlopen(req, timeout=15).read())");start.ArgumentList.Add(url.ToString());
   process=Process.Start(start);
  }catch(Exception e) when(e is System.ComponentModel.Win32Exception or InvalidOperationException){error="nng does not support HTTPS in this SDK, and python urllib fallback could not start.";return false;}
  if(process==null){error="nng does not support HTTPS in this SDK, and python urllib fallback could not start.";return false;}
  using(process)
  {
   svg=default;using var stream=new MemoryStream();
   try{process.StandardOutput.BaseStream.CopyTo(stream);process.WaitForExit();}
   catch(IOException){svg=default;error="python urllib fallback read failed.";return false;}
   if(process.ExitCode!=0){svg=default;error="nng does not support HTTPS in this SDK, and python urllib fallback failed.";return false;}
   svg=new Utf8StringView(stream.ToArray());if(svg.IsEmpty()){error="python urllib fallback returned an empty SVG body.";return false;}return true;
  }
 }
 private static bool DownloadWithNng(Utf8StringView urlText,ref Utf8StringView svg,ref string error)
 {
  _=Registered.Value;nint url=0,client=0,request=0,response=0,aio=0,tls=0;
  byte[] terminatedUrl=new byte[urlText.Bytes.Length+1];urlText.Bytes.CopyTo(terminatedUrl);
  int rv=nng_url_parse(out url,terminatedUrl);if(rv!=0)return NngError(ref error,"nng_url_parse failed",rv);
  try
  {
   var parsed=Marshal.PtrToStructure<GalleryNative.Url>(url);string scheme=GalleryNative.String(parsed.Scheme);
   rv=nng_http_client_alloc(out client,url);
   if(rv!=0){if(scheme=="https"&&rv==9)return DownloadSystemHttpsFallback(urlText,ref svg,ref error);return NngError(ref error,"nng_http_client_alloc failed",rv);}
   if(scheme=="https")
   {
    rv=nng_tls_config_alloc(out tls,0);
    if(rv==0){nng_tls_config_server_name(tls,parsed.Hostname);nng_tls_config_auth_mode(tls,0);rv=nng_http_client_set_tls(client,tls);}
    if(rv!=0){if(rv==9)return DownloadSystemHttpsFallback(urlText,ref svg,ref error);return NngError(ref error,"nng TLS setup failed",rv);}
   }
   rv=nng_http_req_alloc(out request,url);
   if(rv==0)rv=nng_http_req_set_header(request,"User-Agent","SkrGuiGalleryCommon/0.1");
   if(rv==0)rv=nng_http_req_set_header(request,"Accept","image/svg+xml,text/xml,*/*");
   if(rv==0)rv=nng_http_req_set_header(request,"Connection","close");
   if(rv!=0)return NngError(ref error,"nng_http_req setup failed",rv);
   rv=nng_http_res_alloc(out response);if(rv!=0)return NngError(ref error,"nng_http_res_alloc failed",rv);
   rv=nng_aio_alloc(out aio,0,0);if(rv!=0)return NngError(ref error,"nng_aio_alloc failed",rv);
   nng_aio_set_timeout(aio,15000);nng_http_client_transact(client,request,response,aio);nng_aio_wait(aio);rv=nng_aio_result(aio);
   if(rv!=0)return NngError(ref error,"nng_http_client_transact failed",rv);
   ushort status=nng_http_res_get_status(response);
   if(status!=200){error=$"HTTP status {status}";string reason=GalleryNative.String(nng_http_res_get_reason(response));if(reason.Length!=0)error+=" "+reason;return false;}
   nng_http_res_get_data(response,out nint body,out nuint size);
   if(body==0||size==0){error="HTTP response body is empty.";return false;}
   byte[] bytes=new byte[(int)size];Marshal.Copy(body,bytes,0,bytes.Length);svg=new Utf8StringView(bytes);return true;
  }
  finally
  {
   if(aio!=0)nng_aio_free(aio);if(response!=0)nng_http_res_free(response);if(request!=0)nng_http_req_free(request);if(tls!=0)nng_tls_config_free(tls);if(client!=0)nng_http_client_free(client);if(url!=0)nng_url_free(url);
  }
 }
 private static void StartDownloadIfNeeded(Utf8StringView url)
 {
  Utf8StringView downloadUrl=default;bool start=false;
  lock(Store.Mutex){var entry=EnsureEntry(url);if(entry.State==EGallerySvgDownloadState.Idle){entry.State=EGallerySvgDownloadState.Downloading;entry.Error="";entry.Generation++;Store.Generation++;start=true;downloadUrl=new(entry.Url.Bytes.ToArray());}}
  if(!start)return;
  new Thread(()=>
  {
   Utf8StringView svg=default;string error="";GallerySvgDocument document=new();bool ok=DownloadWithNng(downloadUrl,ref svg,ref error);if(ok)ok=Parse(svg,ref document,ref error);
   lock(Store.Mutex){var entry=EnsureEntry(downloadUrl);entry.State=ok?EGallerySvgDownloadState.Ready:EGallerySvgDownloadState.Failed;entry.Document=ok?document:new();entry.Error=ok?"":error;entry.Generation++;Store.Generation++;}
   if(ok)Trace.TraceInformation($"Downloaded SVG: {downloadUrl} ({svg.Size()} bytes)");else Trace.TraceError($"Failed to download SVG: {downloadUrl}: {error}");
  }){IsBackground=true}.Start();
 }
 public static GallerySvgSnapshot RemoteSnapshot(string? url)=>RemoteSnapshot(url==null?default(Utf8StringView):(Utf8StringView)url);
 public static GallerySvgSnapshot RemoteSnapshot(Utf8StringView url)
 {
  url=GalleryShared.CString(url);
  if(url.IsEmpty())return new(){State=EGallerySvgDownloadState.Failed,Error="SVG download URL is empty.",Generation=0};
  StartDownloadIfNeeded(url);
  lock(Store.Mutex){var entry=FindEntry(url);return entry==null?new():new(){State=entry.State,Document=new(entry.Document),Error=entry.Error,Generation=entry.Generation};}
 }
 public static bool DownloadGenerationChanged(ref ulong generation)
 {lock(Store.Mutex){if(generation==Store.Generation)return false;generation=Store.Generation;return true;}}
 public static string NativeDependencyVersion()=>GalleryNative.String(nng_version());
}
