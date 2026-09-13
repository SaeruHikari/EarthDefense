using System.Runtime.InteropServices;
namespace SkrGui.Gallery;
// Source: gallery_common/src/gallery_png.cpp. Same uncompressed DEFLATE blocks and PNG byte layout.
public static class GalleryPng
{
 private static uint Crc32Bytes(ReadOnlySpan<byte> data)
 {uint crc=0xffffffff;foreach(byte value in data){crc^=value;for(uint bit=0;bit<8;bit++){uint mask=unchecked(0u-(crc&1));crc=(crc>>1)^(0xedb88320u&mask);}}return ~crc;}
 private static uint Adler32Bytes(ReadOnlySpan<byte> data)
 {uint a=1,b=0;foreach(byte value in data){a=(a+value)%65521;b=(b+a)%65521;}return(b<<16)|a;}
 private static void AppendBe32(List<byte> output,uint value)
 {output.Add((byte)(value>>24));output.Add((byte)(value>>16));output.Add((byte)(value>>8));output.Add((byte)value);}
 private static void AppendChunk(List<byte> output,string type,ReadOnlySpan<byte> data)
 {AppendBe32(output,(uint)data.Length);int offset=output.Count;for(int i=0;i<4;i++)output.Add((byte)type[i]);foreach(byte b in data)output.Add(b);uint crc=Crc32Bytes(CollectionsMarshal.AsSpan(output).Slice(offset,4+data.Length));AppendBe32(output,crc);}
 public static bool WritePngRgba8(string path,uint width,uint height,ReadOnlySpan<byte> rgba)
 {
  if((ulong)rgba.Length!=(ulong)width*height*4)return false;
  byte[] raw=new byte[(int)((ulong)height*((ulong)width*4+1))];
  for(uint y=0;y<height;y++){int dst=(int)((ulong)y*((ulong)width*4+1));raw[dst]=0;rgba.Slice((int)((ulong)y*width*4),(int)(width*4)).CopyTo(raw.AsSpan(dst+1));}
  List<byte> zlib=[0x78,0x01];int offset=0;
  while(offset<raw.Length){int remaining=raw.Length-offset;ushort blockSize=(ushort)Math.Min(remaining,65535);bool final=offset+blockSize==raw.Length;zlib.Add(final?(byte)1:(byte)0);zlib.Add((byte)blockSize);zlib.Add((byte)(blockSize>>8));ushort nlen=unchecked((ushort)~blockSize);zlib.Add((byte)nlen);zlib.Add((byte)(nlen>>8));for(int i=0;i<blockSize;i++)zlib.Add(raw[offset+i]);offset+=blockSize;}
  AppendBe32(zlib,Adler32Bytes(raw));
  List<byte> png=[137,80,78,71,13,10,26,10];List<byte> ihdr=[];AppendBe32(ihdr,width);AppendBe32(ihdr,height);ihdr.AddRange([8,6,0,0,0]);
  AppendChunk(png,"IHDR",CollectionsMarshal.AsSpan(ihdr));AppendChunk(png,"IDAT",CollectionsMarshal.AsSpan(zlib));AppendChunk(png,"IEND",[]);
  try{string? parent=Path.GetDirectoryName(path);if(!string.IsNullOrEmpty(parent))Directory.CreateDirectory(parent);File.WriteAllBytes(path,png.ToArray());return true;}catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}catch(ArgumentException){return false;}catch(NotSupportedException){return false;}
 }
 public static bool WriteAtlasPng(TextServices services,string path)
 {
  uint width=0,height=0;
  for(uint i=0;i<services.AtlasCount();i++){var atlas=services.Atlas(i);if(atlas.AtlasIndex==uint.MaxValue||atlas.Pixels.IsEmpty)continue;width=Math.Max(width,atlas.Width);height+=atlas.Height;}
  if(width==0||height==0)return false;
  byte[] rgba=new byte[(int)((ulong)width*height*4)];uint destinationY=0;
  for(uint i=0;i<services.AtlasCount();i++)
  {var atlas=services.Atlas(i);if(atlas.AtlasIndex==uint.MaxValue||atlas.Pixels.IsEmpty)continue;
   for(uint y=0;y<atlas.Height;y++){var source=atlas.Pixels.Span.Slice((int)(y*atlas.Stride));int dest=(int)((ulong)(destinationY+y)*width*4);
    for(uint x=0;x<atlas.Width;x++){byte alpha=atlas.Format==ETextAtlasFormat.RGBA8?source[(int)(x*4+3)]:source[(int)x];rgba[dest+(int)x*4]=alpha;rgba[dest+(int)x*4+1]=alpha;rgba[dest+(int)x*4+2]=alpha;rgba[dest+(int)x*4+3]=255;}}
   destinationY+=atlas.Height;
  }
  return WritePngRgba8(path,width,height,rgba);
 }
}
