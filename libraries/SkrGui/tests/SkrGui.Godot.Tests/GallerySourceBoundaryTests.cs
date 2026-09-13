using System.Reflection;
using SkrGui.Gallery;
namespace SkrGui.Tests;
internal static class GallerySourceBoundaryTests
{
 [GuiTest("godot-gallery/source-boundary/framework-descriptors-value-copy-defaults")]
 private static void DescriptorValueCopies()
 {
  var page=new GalleryPageFrameDesc{AppTitle="original",PageIndex=2};var pageCopy=page;pageCopy.PageIndex=9;pageCopy.AppTitle="copy";
  Check.Equal(2u,page.PageIndex);Check.That(page.AppTitle=="original");Check.That(new GalleryPageFrameDesc().PageTitle.IsEmpty());
  var info=new GalleryCaseFrameDesc{CaseTitle="original"};var copy=info;copy.CaseTitle="copy";copy.CaseSummaryMaxLines=2;
  Check.That(info.CaseTitle=="original");Check.Equal(5UL,info.CaseSummaryMaxLines);Check.Near(12.5,info.CaseSummaryPixelSize);Check.Near(186,info.CaseInfoWidth);
 }
 [GuiTest("godot-gallery/source-boundary/framework-null-terminated-raw-utf8")]
 private static void FrameworkCString()
 {
  byte[] bytes=[0xff,0xc3,0xa9,0,0x62];
  var text=GalleryFramework.MakeTextNode(new Utf8StringView(bytes),15,new(),EVisualTempTextAlign.Left,ETextOverflow.Visible,1);
  Check.SequenceEqual(new byte[]{0xff,0xc3,0xa9},text.Text().Bytes.ToArray());
  bytes[0]=0x61;Check.Equal((byte)0xff,text.Text().Bytes[0]);
 }
 [GuiTest("godot-gallery/source-boundary/text-aa-strcmp-null-termination")]
 private static void AaCString()
 {
  var mode=EGalleryTextAAMode.Auto;Check.That(GalleryShared.ParseTextAaMode("gray\0ignored",ref mode));Check.Equal(EGalleryTextAAMode.Gray,mode);
  Check.That(GalleryShared.ParseTextAaMode(new Utf8StringView(new byte[]{(byte)'s',(byte)'d',(byte)'f',0,0xff}),ref mode));Check.Equal(EGalleryTextAAMode.Sdf,mode);
  Check.False(GalleryShared.ParseTextAaMode(new Utf8StringView(new byte[]{0xff}),ref mode));Check.Equal(EGalleryTextAAMode.Sdf,mode);
  Check.False(GalleryShared.ParseTextAaMode((string?)null,ref mode));Check.Equal(EGalleryTextAAMode.Sdf,mode);
 }
 [GuiTest("godot-gallery/source-boundary/texture-value-argument-copied-before-destroy")]
 private static void TextureCopyOrder()
 {
  var data=new GalleryTextureData{Width=2,Height=1,Stride=8,Generation=3,Pixels=[1,2,3,4,5,6,7,8]};var texture=new GalleryBackendTexture{Data=data};
  var release=typeof(GalleryBackendTexture).GetField("ReleaseGpu",BindingFlags.Instance|BindingFlags.NonPublic)!;
  release.SetValue(texture,(Action)(()=>{data.Pixels[0]=99;data.Width=9;data.Generation=20;}));texture.ResetData(data);
  Check.Equal(2,texture.PixelSize().Width);Check.Equal(3UL,texture.Data.Generation);Check.Equal((byte)1,texture.Data.Pixels[0]);Check.Equal((byte)99,data.Pixels[0]);
 }
 [GuiTest("godot-gallery/source-boundary/atlas-path-original-lexical-filenames")]
 private static void AtlasPaths()
 {
  foreach(var pair in new(string,string)[]{("","gallery.atlas.png"),("frame.png","frame.atlas.png"),(".hidden",".hidden.atlas.png"),(".hidden.png",".hidden.atlas.png"),(".","..atlas.png"),("..","...atlas.png"),("a//b/frame.png","a//b/frame.atlas.png"),("a//b/","a//b/gallery.atlas.png"),("/","/gallery.atlas.png")})Check.Equal(pair.Item2,GalleryShared.DefaultAtlasPngPath(pair.Item1));
  if(!OperatingSystem.IsWindows())return;
  foreach(var pair in new(string,string)[]{(@"C:",@"C:gallery.atlas.png"),(@"C:frame.png",@"C:frame.atlas.png"),(@"C:\",@"C:\gallery.atlas.png"),(@"\\server\share",@"\\server\share\gallery.atlas.png"),(@"\\server\share\frame.png",@"\\server\share\frame.atlas.png"),(@"\\?\C:\frame.png",@"\\?\C:\frame.atlas.png"),(@"\\?\UNC\server\share",@"\\?\UNC\server\share\gallery.atlas.png"),(@"\\.\pipe",@"\\.\pipe\gallery.atlas.png"),(@"\Device\HarddiskVolume1",@"\Device\HarddiskVolume1\gallery.atlas.png")})Check.Equal(pair.Item2,GalleryShared.DefaultAtlasPngPath(pair.Item1));
 }
 [GuiTest("godot-gallery/source-boundary/png-invalid-path-returns-false")]
 private static void PngInvalidPath()=>Check.False(GalleryPng.WritePngRgba8("bad\0path.png",1,1,new byte[]{1,2,3,4}));
 [GuiTest("godot-gallery/source-boundary/nng-url-raw-byte-input")]
 private static void NativeUrlBytes()
 {
  var native=typeof(GallerySvg).Assembly.GetType("SkrGui.Gallery.GalleryNative")!;
  var parse=native.GetMethod("nng_url_parse",BindingFlags.Static|BindingFlags.NonPublic)!;
  var free=native.GetMethod("nng_url_free",BindingFlags.Static|BindingFlags.NonPublic)!;
  byte[] input=[..System.Text.Encoding.ASCII.GetBytes("http://127.0.0.1/"),0xff,0];object?[] args=[nint.Zero,input];
  Check.Equal(3,(int)parse.Invoke(null,args)!); // Original NNG_EINVAL for malformed UTF-8.
  input=[..System.Text.Encoding.ASCII.GetBytes("http://127.0.0.1/"),0xc3,0xa9,0];args=[nint.Zero,input];
  Check.Equal(0,(int)parse.Invoke(null,args)!);nint url=(nint)args[0]!;
  try
  {
   nint raw=System.Runtime.InteropServices.Marshal.ReadIntPtr(url);var actual=new byte[input.Length-1];System.Runtime.InteropServices.Marshal.Copy(raw,actual,0,actual.Length);
   Check.SequenceEqual(input.Take(input.Length-1),actual);
  }
  finally{free.Invoke(null,[url]);}
 }

}
