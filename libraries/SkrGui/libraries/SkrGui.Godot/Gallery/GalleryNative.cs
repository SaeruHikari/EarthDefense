using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace SkrGui.Gallery;
internal static unsafe class GalleryNative
{
 const string Library="skrgui_gallery_native";
 private static nint _library;
 [ModuleInitializer] internal static void Initialize()=>NativeLibrary.SetDllImportResolver(typeof(GalleryNative).Assembly,Resolve);
 private static nint Resolve(string name,Assembly assembly,DllImportSearchPath? searchPath)
 {
  if(name!=Library)return 0;if(_library!=0)return _library;
  string filename=OperatingSystem.IsWindows()?Library+".dll":OperatingSystem.IsMacOS()?"lib"+Library+".dylib":"lib"+Library+".so";
  foreach(string start in new[]{AppContext.BaseDirectory,Path.GetDirectoryName(assembly.Location)!,Environment.CurrentDirectory})
   for(DirectoryInfo? dir=new(start);dir!=null;dir=dir.Parent)
    foreach(string path in new[]{Path.Combine(dir.FullName,filename),Path.Combine(dir.FullName,"native/artifacts/win-x64",filename)})
     if(File.Exists(path)&&NativeLibrary.TryLoad(path,out _library))return _library;
  throw new DllNotFoundException("Run native/build_gallery_native.ps1 for the source NNG 1.11.0 SDK dependency.");
 }
 [StructLayout(LayoutKind.Sequential)]internal struct Url {internal nint Raw,Scheme,UserInfo,Host,Hostname,Port,Path,Query,Fragment,RequestUri;}
 internal static string String(nint ptr)=>Marshal.PtrToStringUTF8(ptr)??"";
 [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern float skrgui_strtof(byte* text,out byte* end,out int rangeError);
 [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern nint nng_version();
 [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int nng_tcp_register();
 [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int nng_tls_register();
 [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int nng_url_parse(out nint url,[In]byte[] text);
 [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern void nng_url_free(nint url);
 [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern nint nng_strerror(int error);
 [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int nng_http_client_alloc(out nint client,nint url);
 [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern void nng_http_client_free(nint client);
 [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int nng_http_client_set_tls(nint client,nint tls);
 [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int nng_tls_config_alloc(out nint config,int mode);
 [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern void nng_tls_config_free(nint config);
 [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int nng_tls_config_server_name(nint config,nint hostname);
 [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int nng_tls_config_auth_mode(nint config,int mode);
 [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int nng_http_req_alloc(out nint request,nint url);
 [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern void nng_http_req_free(nint request);
 [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int nng_http_req_set_header(nint request,[MarshalAs(UnmanagedType.LPUTF8Str)]string name,[MarshalAs(UnmanagedType.LPUTF8Str)]string value);
 [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int nng_http_res_alloc(out nint response);
 [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern void nng_http_res_free(nint response);
 [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern ushort nng_http_res_get_status(nint response);
 [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern nint nng_http_res_get_reason(nint response);
 [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern void nng_http_res_get_data(nint response,out nint data,out nuint size);
 [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int nng_aio_alloc(out nint aio,nint callback,nint argument);
 [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern void nng_aio_free(nint aio);
 [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern void nng_aio_set_timeout(nint aio,int timeout);
 [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern void nng_http_client_transact(nint client,nint request,nint response,nint aio);
 [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern void nng_aio_wait(nint aio);
 [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int nng_aio_result(nint aio);
}
