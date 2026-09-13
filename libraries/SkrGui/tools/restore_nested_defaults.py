import pathlib
root=pathlib.Path(__file__).resolve().parents[1]
p=root/'libraries/SkrGui.Core/Backend/VisualBackend.cs'
s=p.read_text().replace('public SRGBColor ClearColor;\n}', 'public SRGBColor ClearColor = new();\n    public REPLACE_CONSTRUCTOR() { }\n}',1).replace('REPLACE_CONSTRUCTOR','VisualRenderDesc')
s=s.replace('public SRGBColor ClearColor;\n}', 'public SRGBColor ClearColor = new();\n    public VisualRenderTargetDesc() { }\n}',1);p.write_text(s)
p=root/'libraries/SkrGui.Core/Backend/VisualOwner.cs';s=p.read_text().replace('BoxConstraints _lastLayoutConstraints;','BoxConstraints _lastLayoutConstraints = new();');p.write_text(s)
p=root/'libraries/SkrGui.Godot/Gallery/GalleryShared.cs';s=p.read_text()
s=s.replace('public SRGBColor Background,Surface,SurfaceSubtle,Border,GridLine,Ink,Muted,Blue,Green,Red,Amber,Violet,Cyan;','public SRGBColor Background=new(),Surface=new(),SurfaceSubtle=new(),Border=new(),GridLine=new(),Ink=new(),Muted=new(),Blue=new(),Green=new(),Red=new(),Amber=new(),Violet=new(),Cyan=new();\n    public GalleryPalette(){}')
s=s.replace('public uint GlyphPadding;}','public uint GlyphPadding=1;public GalleryTextAtlasConfig(){}}');p.write_text(s)
p=root/'libraries/SkrGui.Godot/Gallery/GallerySvg.cs';s=p.read_text().replace('public SRGBColor Fill;','public SRGBColor Fill=new();');p.write_text(s)
p=root/'libraries/SkrGui.Godot/Gallery/GalleryResources.cs';s=p.read_text().replace('public ETextAtlasFormat Format=ETextAtlasFormat.RGBA8;\n}', 'public ETextAtlasFormat Format=ETextAtlasFormat.RGBA8;\n    public GalleryTextureData(){}\n    public GalleryTextureData(GalleryTextureData source){Pixels=(byte[])source.Pixels.Clone();Width=source.Width;Height=source.Height;Stride=source.Stride;Generation=source.Generation;Format=source.Format;}\n}')
s=s.replace('DestroyGpu();Data=value;Size=', 'DestroyGpu();Data=new(value);Size=');p.write_text(s)
