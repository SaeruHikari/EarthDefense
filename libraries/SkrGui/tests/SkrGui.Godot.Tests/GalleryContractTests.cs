using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using SkrGui.Gallery;
namespace SkrGui.Tests;
internal static class GalleryContractTests
{
 private static GallerySvgDocument Parse(string source)
 {var document=new GallerySvgDocument();string error="unchanged";Check.That(GallerySvg.Parse(source,ref document,ref error),error);Check.Equal("unchanged",error);return document;}
 private static string Svg(string path,string attributes="")=>$"<svg viewBox='0 0 100 80'><path d='{path}' {attributes}/></svg>";
 [GuiTest("godot-gallery/svg/source-native-library-version-and-float-parser")]
 private static void NativeParsing()
 {
  Check.Equal("1.11.0",GallerySvg.NativeDependencyVersion());
  var doc=Parse(Svg("M1e1,2E1 L30.5 40 L1 1 Z"));Check.Equal(new Offsetf(10,20),doc.Shapes[0].Path.Commands()[0].Move.Target);Check.Equal(Rectf.LTWH(0,0,100,80),doc.ViewBox);
  doc=Parse(Svg("M0x1p2 0x1p3 L20 20 L1 1 Z"));Check.Equal(new Offsetf(4,8),doc.Shapes[0].Path.Commands()[0].Move.Target);
  string error="";doc=new();Check.False(GallerySvg.Parse(Svg("M1e999 0L1 1L2 0Z"),ref doc,ref error));Check.Equal("Invalid SVG move command.",error);
 }
 [GuiTest("godot-gallery/svg/source-all-supported-path-commands")]
 private static void PathCommands()
 {
  var doc=Parse(Svg("M10 10 20 10 l10 10 h5 v5 C40 25 40 40 50 40 s10 10 20 0 Q80 30 90 40 t-10 20 z"));
  var c=doc.Shapes[0].Path.Commands();Check.SequenceEqual(new[]{EVGPathCommandType.MoveTo,EVGPathCommandType.LineTo,EVGPathCommandType.LineTo,EVGPathCommandType.LineTo,EVGPathCommandType.LineTo,EVGPathCommandType.CubicTo,EVGPathCommandType.CubicTo,EVGPathCommandType.QuadTo,EVGPathCommandType.QuadTo,EVGPathCommandType.Close},c.Select(x=>x.Type));
  Check.Equal(new Offsetf(60,40),c[6].Cubic.Control0);Check.Equal(new Offsetf(60,50),c[6].Cubic.Control1);Check.Equal(new Offsetf(100,50),c[8].Quad.Control);Check.Equal(new Offsetf(80,60),c[8].Quad.Target);
  // Bounds deliberately include controls, exactly as the temporary source parser does.
  Check.Equal(new Rectf(10,10,100,60),doc.ContentBounds);
 }
 [GuiTest("godot-gallery/svg/source-color-and-transform-inheritance")]
 private static void Transforms()
 {
  var doc=Parse("<svg viewBox='0 0 40 40'><g transform='translate(10,20) scale(2,3)' fill='#f80'><path d='M0 0L5 0L5 5Z' transform='translate(1,2)'/></g><path fill='none' d='M1 1L2 1L2 2Z'/></svg>");
  Check.Equal(new Offsetf(12,26),doc.Shapes[0].Path.Commands()[0].Move.Target);Check.Equal(new Offsetf(22,41),doc.Shapes[0].Path.Commands()[2].Line.Target);
  Check.Equal(GalleryShared.Rgba(255,136,0),doc.Shapes[0].Fill);Check.Equal(new SRGBColor(0,0,0,0),doc.Shapes[1].Fill);
  doc=Parse(Svg("M1 0L2 0L2 1Z","transform='rotate(90 1 1)' fill='unsupported'"));var p=doc.Shapes[0].Path.Commands()[0].Move.Target;Check.Near(2,p.X,.0001);Check.Near(1,p.Y,.0001);Check.Equal(GalleryShared.Rgba(0,0,0),doc.Shapes[0].Fill);
 }
 [GuiTest("godot-gallery/svg/source-rejection-messages-and-partial-document")]
 private static void Rejections()
 {
  foreach(var item in new[]{
   ("<svg></svg>","SVG viewBox is missing or invalid."),
   ("<svg viewBox='0 0 4 4'><rect x='0'/></svg>","SVG has no supported path data."),
   ("<svg viewBox='0 0 4 4'><path","SVG path tag is not closed."),
   (Svg("M0 0A1 1 0 0 1 2 2"),"SVG arc path command is not supported by the temporary parser."),
   (Svg("0 0L1 1"),"SVG path data starts without a command."),
   (Svg("M0 0L1 1L2 0Z","transform='skewX(2)'"),"SVG path transform is invalid.")
  }){GallerySvgDocument doc=new();string error="";Check.False(GallerySvg.Parse(item.Item1,ref doc,ref error));Check.Equal(item.Item2,error);}
  GallerySvgDocument partial=new();string message="";Check.False(GallerySvg.Parse("<svg viewBox='0 0 10 10'><path d='M0 0L2 0L2 2Z'/><path d='M0 0A1 1 0 0 1 2 2'/></svg>",ref partial,ref message));Check.Equal(1,partial.Shapes.Count);
 }
 [GuiTest("godot-gallery/svg/source-value-copy-and-empty-remote-state")]
 private static void ValueCopy()
 {
  var source=Parse(Svg("M0 0L4 0L4 4Z"));var copy=new GallerySvgDocument(source);copy.Shapes[0].Path.Clear();copy.Shapes[0].Fill=new(1,0,0,1);Check.False(source.Shapes[0].Path.IsEmpty());Check.Equal(GalleryShared.Rgba(0,0,0),source.Shapes[0].Fill);
  var remote=GallerySvg.RemoteSnapshot("");Check.Equal(EGallerySvgDownloadState.Failed,remote.State);Check.Equal(0UL,remote.Generation);Check.Equal("SVG download URL is empty.",remote.Error);
 }
 [GuiTest("godot-gallery/svg/source-local-http-state-machine")]
 private static async Task LocalHttp()
 {
  using var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;
  string body=Svg("M0 0L4 0L4 4Z");string url=$"http://127.0.0.1:{port}/test.svg";ulong generation=0;
  var server=Task.Run(async()=>{using var client=await listener.AcceptTcpClientAsync();using var stream=client.GetStream();var buffer=new byte[4096];string request="";while(!request.Contains("\r\n\r\n")){int n=await stream.ReadAsync(buffer);if(n==0)break;request+=Encoding.ASCII.GetString(buffer,0,n);}byte[] content=Encoding.UTF8.GetBytes(body);await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: image/svg+xml\r\nContent-Length: {content.Length}\r\nConnection: close\r\n\r\n"));await stream.WriteAsync(content);return request;});
  byte[] rawUrl=Encoding.UTF8.GetBytes(url+"\0ignored");var snapshot=GallerySvg.RemoteSnapshot(new Utf8StringView(rawUrl));rawUrl[0]=(byte)'x';Check.That(snapshot.State is EGallerySvgDownloadState.Downloading or EGallerySvgDownloadState.Ready);Check.That(GallerySvg.DownloadGenerationChanged(ref generation));
  using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
  while(snapshot.State==EGallerySvgDownloadState.Downloading){await Task.Delay(10,timeout.Token);snapshot=GallerySvg.RemoteSnapshot(url);}
  Check.Equal(EGallerySvgDownloadState.Ready,snapshot.State,snapshot.Error);Check.Equal(2UL,snapshot.Generation);Check.That(snapshot.Document.IsValid());var request=await server.WaitAsync(timeout.Token);Check.That(request.Contains("User-Agent: SkrGuiGalleryCommon/0.1"));Check.That(request.Contains("Accept: image/svg+xml,text/xml,*/*"));
  snapshot.Document.Shapes[0].Path.Clear();Check.False(GallerySvg.RemoteSnapshot(url).Document.Shapes[0].Path.IsEmpty());Check.Equal(2UL,GallerySvg.RemoteSnapshot(url).Generation);
 }
 [GuiTest("godot-gallery/png/source-uncompressed-png-byte-layout")]
 private static void PngBytes()
 {
  string path=Path.Combine(Path.GetTempPath(),"skrgui-png-contract-"+Guid.NewGuid()+".png");
  try
  {
   byte[] rgba=[12,34,56,78,90,123,234,255];Check.That(GalleryPng.WritePngRgba8(path,2,1,rgba));byte[] png=File.ReadAllBytes(path);Check.SequenceEqual(new byte[]{137,80,78,71,13,10,26,10},png.Take(8));
   int pos=8;byte[]? compressed=null;while(pos<png.Length){uint n=System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(pos));string type=Encoding.ASCII.GetString(png,pos+4,4);if(type=="IDAT")compressed=png.AsSpan(pos+8,(int)n).ToArray();pos+=(int)n+12;}
   Check.NotNull(compressed);Check.Equal((byte)0x78,compressed![0]);Check.Equal((byte)0x01,compressed[1]);using var z=new ZLibStream(new MemoryStream(compressed),CompressionMode.Decompress);using var raw=new MemoryStream();z.CopyTo(raw);Check.SequenceEqual(new byte[]{0}.Concat(rgba),raw.ToArray());
   Check.False(GalleryPng.WritePngRgba8(path,2,1,new byte[3]));
  }finally{if(File.Exists(path))File.Delete(path);}
 }
 [GuiTest("godot-gallery/framework/source-common-grid-and-text-box")]
 private static void Framework()
 {
  for(uint count=1;count<=4;count++){var grid=GalleryFramework.MakeCaseGrid(count);for(uint i=0;i<count;i++){var card=GalleryFramework.MakeRuleNode(new(1,1,1,1));GalleryFramework.PlaceCaseCard(grid,card,count,i);Check.That(card.Slot() is VisualGridSlot);}}
  var box=GalleryFramework.MakeTextBox("same source",16,new(1,1,1,1),maxLines:ulong.MaxValue);Check.That(box is VisualConstrained);
  var text=GalleryFramework.MakeTextNode("original",15,new(1,1,1,1),EVisualTempTextAlign.Left,ETextOverflow.Ellipsis,ulong.MaxValue);Check.Null(text.MaxLines());
 }
}
