using Godot;
using System;
using System.Collections.Generic;
namespace Earthward.Presentation;

public partial class SoundBank : Node
{
    public bool Muted
    {
        get; set;
    }

    public Dictionary<string, AudioStreamWav> Sounds { get; } = new();

    public List<AudioStreamPlayer> Players { get; } = new();

    public override void _Ready()
    {
        for (int i = 0; i < 6; i++)
        {
            var p = new AudioStreamPlayer { VolumeDb = -19 };
            AddChild(p);
            Players.Add(p);
        }
        Sounds["build"] = Tone(440, 880, .18, false);
        Sounds["research"] = Tone(660, 1320, .38, false);
        Sounds["start"] = Tone(220, 440, .42, false);
        Sounds["clear"] = Tone(520, 1040, .5, false);
        Sounds["error"] = Tone(150, 110, .16, false);
    }

    public void PlaySound(string id)
    {
        if (Muted || !Sounds.TryGetValue(id, out var sound))
            return;
        foreach (var player in Players)
            if (!player.Playing)
            {
                player.Stream = sound;
                player.Play();
                return;
            }
    }

    public override void _ExitTree()
    {
        // These streams are generated and owned by this bank. Stop playback before
        // releasing them so scene shutdown does not retain native audio handles.
        foreach (var player in Players)
        {
            if (!IsInstanceValid(player))
                continue;
            player.Stop();
            player.Stream = null;
        }
        Players.Clear();
        foreach (var stream in Sounds.Values)
            stream.Dispose();
        Sounds.Clear();
    }

    private static AudioStreamWav Tone(double start, double end, double duration, bool noise)
    {
        int count = (int)(duration * 22050);
        byte[] samples = new byte[count * 2];
        double phase = 0;
        using var rng = new RandomNumberGenerator { Seed = 742 };
        for (int i = 0; i < count; i++)
        {
            double t = (double)i / count;
            phase += Math.Tau * (start + (end - start) * t) / 22050;
            double envelope = Math.Min(t * 35, 1) * Math.Pow(1 - t, 2);
            double sample = (Math.Sin(phase) * .6 + Math.Sin(phase * 1.5) * .15) * envelope;
            if (noise)
                sample += rng.RandfRange(-.28f, .28f) * envelope;
            short value = (short)(Math.Clamp(sample, -1, 1) * 22000);
            samples[i * 2] = (byte)(value & 255);
            samples[i * 2 + 1] = (byte)((value >> 8) & 255);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = 22050, Data = samples };
    }
}
