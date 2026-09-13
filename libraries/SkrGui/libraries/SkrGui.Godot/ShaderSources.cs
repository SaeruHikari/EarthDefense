namespace SkrGui.Godot;
public static class ShaderSources
{
    private static string Read(string file)
    {
        using var stream=typeof(ShaderSources).Assembly.GetManifestResourceStream("SkrGui.Godot.Shaders."+file)??throw new InvalidOperationException("Missing source-mapped shader "+file);
        using var reader=new StreamReader(stream);return reader.ReadToEnd();
    }
    private static string Definitions(string source,bool useTexture,int effect)=>source.Replace("#version 450",$"#version 450\n#define USE_TEXTURE {(useTexture?1:0)}\n#define TEXT_EFFECT {effect}");
    public static string GalleryVertex(bool useTexture,int effect)=>Definitions(Read("gallery.vert.glsl"),useTexture,effect);
    public static string GalleryFragment(bool useTexture,int effect)=>Definitions(Read("gallery.frag.glsl"),useTexture,effect);
    public static string BackdropVertex()=>Read("backdrop.vert.glsl");
    public static string BackdropFragment()=>Read("backdrop.frag.glsl");
    public static string BlurCompute()=>Read("blur.comp.glsl");
    public static string ReadbackCompute()=>Read("readback.comp.glsl");
}
