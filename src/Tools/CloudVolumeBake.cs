using System;
using System.IO;
using System.Linq;
using Godot;

namespace Earthward.Tools;

/// <summary>Offline RGBA8-to-Texture3D packer. It does not instantiate Main or access player profiles.</summary>
public partial class CloudVolumeBake : Node
{
    public override void _Ready()
    {
        int result = 1;
        try
        {
            if (DisplayServer.GetName() == "headless") throw new InvalidOperationException("A real renderer is required to read back Texture3D data. Run tools/bake_cloud_volume_texture.ps1.");
            string Argument(string name, string fallback) => OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--" + name + "=", StringComparison.Ordinal))?.Split('=', 2)[1] ?? fallback;
            int size = int.Parse(Argument("size", "96"), System.Globalization.CultureInfo.InvariantCulture);
            if (size < 1 || size > 256) throw new InvalidDataException("Volume size must be between 1 and 256.");
            string input = Argument("raw", "res://artifacts/cloud_noise_3d.rgba8");
            string output = Argument("output", "res://assets/earth/clouds/volume/cloud_noise_3d.res");
            byte[] raw = System.IO.File.ReadAllBytes(ProjectSettings.GlobalizePath(input));
            int sliceBytes = checked(size * size * 4);
            if (raw.Length != checked(sliceBytes * size)) throw new InvalidDataException("Cloud volume byte count does not match SIZE cubed RGBA8.");
            var slices = new Godot.Collections.Array<Image>();
            for (int z = 0; z < size; z++)
            {
                var bytes = new byte[sliceBytes];
                Buffer.BlockCopy(raw, z * sliceBytes, bytes, 0, sliceBytes);
                slices.Add(Image.CreateFromData(size, size, false, Image.Format.Rgba8, bytes));
            }
            using var texture = new ImageTexture3D();
            Error created = texture.Create(Image.Format.Rgba8, size, size, size, false, slices);
            if (created != Error.Ok) throw new InvalidOperationException("Texture3D creation failed: " + created);
            texture.ResourceName = "Periodic volumetric cloud density: shape, erosion, detail, variation";
            string path = ProjectSettings.GlobalizePath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            Error saved = ResourceSaver.Save(texture, output, ResourceSaver.SaverFlags.Compress);
            if (saved != Error.Ok) throw new IOException("Texture3D save failed: " + saved);
            using var restored = ResourceLoader.Load<Texture3D>(output, "Texture3D", ResourceLoader.CacheMode.Ignore);
            if (restored == null || restored.GetWidth() != size || restored.GetHeight() != size || restored.GetDepth() != size) throw new InvalidDataException("Saved volume dimensions do not match.");
            var data = restored.GetData();
            if (data.Count != size) throw new InvalidDataException("Saved volume slice count does not match.");
            for (int z = 0; z < size; z++)
                if (!data[z].GetData().AsSpan().SequenceEqual(raw.AsSpan(z * sliceBytes, sliceBytes))) throw new InvalidDataException("Saved volume bytes differ at slice " + z);
            foreach (var image in slices) image.Dispose();
            foreach (var image in data) image.Dispose();
            GD.Print($"CLOUD_VOLUME_BAKE_PASS: {size}x{size}x{size}, RGBA8 linear, no mipmaps; every slice verified: {output}");
            result = 0;
        }
        catch (Exception error) { GD.PushError("CLOUD_VOLUME_BAKE_FAIL: " + error); }
        GetTree().Quit(result);
    }
}
