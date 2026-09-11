using System;
using System.IO;
using System.Linq;
using Godot;

namespace Earthward.Tools;

/// <summary>Offline cloud volume packer. It does not instantiate Main or access player profiles.
/// It writes the two-channel layout the cloud shader samples: R keeps the shaping field and G folds
/// the generator's erosion and detail channels into the single gain the shader applies to them.</summary>
public partial class CloudVolumeBake : Node
{
    // Weights the generator applies to its separate erosion and detail channels. Packing folds both
    // into one 8-bit channel, and the shader multiplies the packed channel back by their sum, so the
    // field survives packing exactly up to one 8-bit step.
    private const float ErosionWeight = 0.035f;
    private const float DetailWeight = 0.012f;
    private const float PackedErosionGain = ErosionWeight + DetailWeight;

    public override void _Ready()
    {
        int result = 1;
        try
        {
            if (DisplayServer.GetName() == "headless") throw new InvalidOperationException("A real renderer is required to read back Texture3D data. Run tools/bake_cloud_volume_texture.ps1.");
            string Argument(string name, string fallback) => OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--" + name + "=", StringComparison.Ordinal))?.Split('=', 2)[1] ?? fallback;
            string output = Argument("output", "res://assets/earth/clouds/volume/cloud_noise_3d.res");
            string source = Argument("source", string.Empty);
            // Repack an existing volume (migration) or pack a raw Z,Y,X RGBA8 cube.
            if (source.Length > 0) Repack(source, output);
            else Pack(Argument("raw", "res://artifacts/cloud_noise_3d.rgba8"), output, int.Parse(Argument("size", "96"), System.Globalization.CultureInfo.InvariantCulture));
            result = 0;
        }
        catch (Exception error) { GD.PushError("CLOUD_VOLUME_BAKE_FAIL: " + error); }
        GetTree().Quit(result);
    }

    /// <summary>Folds RGBA8 shape / erosion / detail / variation into the RG8 layout the shader reads.</summary>
    private static byte[] PackSlice(byte[] rgba, int pixels)
    {
        if (rgba.Length != pixels * 4) throw new InvalidDataException("Cloud volume source slice is not RGBA8.");
        var packed = new byte[pixels * 2];
        for (int pixel = 0; pixel < pixels; pixel++)
        {
            int i = pixel * 4;
            packed[pixel * 2] = rgba[i];
            float erosion = (rgba[i + 1] / 255f * ErosionWeight + rgba[i + 2] / 255f * DetailWeight) / PackedErosionGain;
            packed[pixel * 2 + 1] = (byte)Math.Clamp((int)MathF.Round(erosion * 255f), 0, 255);
        }
        return packed;
    }

    private static void Pack(string input, string output, int size)
    {
        if (size < 1 || size > 256) throw new InvalidDataException("Volume size must be between 1 and 256.");
        byte[] raw = System.IO.File.ReadAllBytes(ProjectSettings.GlobalizePath(input));
        int sliceBytes = checked(size * size * 4);
        if (raw.Length != checked(sliceBytes * size)) throw new InvalidDataException("Cloud volume byte count does not match SIZE cubed RGBA8.");
        var packed = new Godot.Collections.Array<byte[]>();
        for (int z = 0; z < size; z++) packed.Add(PackSlice(raw.AsSpan(z * sliceBytes, sliceBytes).ToArray(), size * size));
        Write(output, size, packed, $"raw cube {input}");
    }

    private static void Repack(string source, string output)
    {
        using var input = ResourceLoader.Load<Texture3D>(source, "Texture3D", ResourceLoader.CacheMode.Ignore);
        if (input == null) throw new FileNotFoundException("Cloud volume source is missing.", source);
        int size = input.GetWidth();
        if (input.GetHeight() != size || input.GetDepth() != size) throw new InvalidDataException("Cloud volume source must be a cube.");
        var slices = input.GetData();
        if (slices.Count != size) throw new InvalidDataException("Cloud volume source slice count does not match its depth.");
        var packed = new Godot.Collections.Array<byte[]>();
        for (int z = 0; z < size; z++)
        {
            using Image slice = slices[z];
            if (slice.GetFormat() != Image.Format.Rgba8) slice.Convert(Image.Format.Rgba8);
            packed.Add(PackSlice(slice.GetData(), size * size));
        }
        Write(output, size, packed, source);
    }

    private static void Write(string output, int size, Godot.Collections.Array<byte[]> packed, string origin)
    {
        var slices = new Godot.Collections.Array<Image>();
        foreach (byte[] bytes in packed) slices.Add(Image.CreateFromData(size, size, false, Image.Format.Rg8, bytes));
        // Mipmaps are deliberately not requested: Godot does not serialize them for ImageTexture3D
        // (a mipmapped save reloads as 1x1x1 with no slices), and the volume march samples level 0.
        using var texture = new ImageTexture3D();
        Error created = texture.Create(Image.Format.Rg8, size, size, size, false, slices);
        if (created != Error.Ok) throw new InvalidOperationException("Texture3D creation failed: " + created);
        texture.ResourceName = "Periodic volumetric cloud density: shape, folded erosion";
        string path = ProjectSettings.GlobalizePath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Error saved = ResourceSaver.Save(texture, output, ResourceSaver.SaverFlags.Compress);
        if (saved != Error.Ok) throw new IOException("Texture3D save failed: " + saved);
        using var restored = ResourceLoader.Load<Texture3D>(output, "Texture3D", ResourceLoader.CacheMode.Ignore);
        if (restored == null || restored.GetWidth() != size || restored.GetHeight() != size || restored.GetDepth() != size) throw new InvalidDataException("Saved volume dimensions do not match.");
        var data = restored.GetData();
        if (data.Count != size) throw new InvalidDataException("Saved volume slice count does not match: " + data.Count);
        for (int z = 0; z < size; z++)
        {
            if (data[z].GetFormat() != Image.Format.Rg8) throw new InvalidDataException("Saved volume format is not RG8.");
            if (!data[z].GetData().AsSpan().SequenceEqual(packed[z])) throw new InvalidDataException("Saved volume bytes differ at slice " + z);
        }
        foreach (var image in slices) image.Dispose();
        foreach (var image in data) image.Dispose();
        GD.Print($"CLOUD_VOLUME_BAKE_PASS: {size}x{size}x{size} RG8 from {origin}, {new FileInfo(path).Length} bytes; every slice verified: {output}");
    }
}
