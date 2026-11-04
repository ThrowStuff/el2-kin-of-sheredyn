// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KinOfSheredyn;
public static class LeaderAudioIds
{
    public static readonly IReadOnlyDictionary<string,uint> ByName = new Dictionary<string,uint>(StringComparer.Ordinal)
    {
        ["Play_Leader_KinOfSheredyn_Attitude_Aggressive01"] = 1500791534u,
        ["Play_Leader_KinOfSheredyn_Attitude_Bound01"] = 2307995108u,
        ["Play_Leader_KinOfSheredyn_Attitude_Friendly01"] = 3735873375u,
        ["Play_Leader_KinOfSheredyn_Attitude_Indifferent01"] = 3594911732u,
        ["Play_Leader_KinOfSheredyn_Attitude_Mean01"] = 2552409217u,
        ["Play_Leader_KinOfSheredyn_Attitude_Needy01"] = 4104724617u,
        ["Play_Leader_KinOfSheredyn_Attitude_Withdrawn01"] = 3564695068u,
        ["Play_Leader_KinOfSheredyn_Greeting_Aggressive01"] = 255043291u,
        ["Play_Leader_KinOfSheredyn_Greeting_Aggressive02"] = 255043288u,
        ["Play_Leader_KinOfSheredyn_Greeting_Aggressive03"] = 255043289u,
        ["Play_Leader_KinOfSheredyn_Greeting_Bound01"] = 2492566019u,
        ["Play_Leader_KinOfSheredyn_Greeting_Bound02"] = 2492566016u,
        ["Play_Leader_KinOfSheredyn_Greeting_Bound03"] = 2492566017u,
        ["Play_Leader_KinOfSheredyn_Greeting_FirstMeet01"] = 3433477064u,
        ["Play_Leader_KinOfSheredyn_Greeting_Friendly01"] = 749843746u,
        ["Play_Leader_KinOfSheredyn_Greeting_Friendly02"] = 749843745u,
        ["Play_Leader_KinOfSheredyn_Greeting_Friendly03"] = 749843744u,
        ["Play_Leader_KinOfSheredyn_Greeting_Indifferent01"] = 2184682171u,
        ["Play_Leader_KinOfSheredyn_Greeting_Indifferent02"] = 2184682168u,
        ["Play_Leader_KinOfSheredyn_Greeting_Indifferent03"] = 2184682169u,
        ["Play_Leader_KinOfSheredyn_Greeting_Mean01"] = 3947804376u,
        ["Play_Leader_KinOfSheredyn_Greeting_Mean02"] = 3947804379u,
        ["Play_Leader_KinOfSheredyn_Greeting_Mean03"] = 3947804378u,
        ["Play_Leader_KinOfSheredyn_Greeting_Needy01"] = 1533632206u,
        ["Play_Leader_KinOfSheredyn_Greeting_Needy02"] = 1533632205u,
        ["Play_Leader_KinOfSheredyn_Greeting_Needy03"] = 1533632204u,
        ["Play_Leader_KinOfSheredyn_Greeting_Recess01"] = 1417330878u,
        ["Play_Leader_KinOfSheredyn_Greeting_Recess02"] = 1417330877u,
        ["Play_Leader_KinOfSheredyn_Greeting_Recess03"] = 1417330876u,
        ["Play_Leader_KinOfSheredyn_Greeting_Withdrawn01"] = 2904536155u,
        ["Play_Leader_KinOfSheredyn_Greeting_Withdrawn02"] = 2904536152u,
        ["Play_Leader_KinOfSheredyn_Greeting_Withdrawn03"] = 2904536153u,
        ["Play_Leader_KinOfSheredyn_Treaty_Accept01"] = 852305947u,
        ["Play_Leader_KinOfSheredyn_Treaty_Accept_Player01"] = 280885447u,
        ["Play_Leader_KinOfSheredyn_Treaty_Acknowledge01"] = 2105732443u,
        ["Play_Leader_KinOfSheredyn_Treaty_Acknowledge_Player01"] = 2617502983u,
        ["Play_Leader_KinOfSheredyn_Treaty_Compliment01"] = 586650651u,
        ["Play_Leader_KinOfSheredyn_Treaty_Counter01"] = 94551419u,
        ["Play_Leader_KinOfSheredyn_Treaty_Counter_Player01"] = 1424929255u,
        ["Play_Leader_KinOfSheredyn_Treaty_Declare01"] = 1706673353u,
        ["Play_Leader_KinOfSheredyn_Treaty_Declare_Player01"] = 3035860129u,
        ["Play_Leader_KinOfSheredyn_Treaty_Discovery_Declare01"] = 133180526u,
        ["Play_Leader_KinOfSheredyn_Treaty_Discovery_Propose01"] = 4145127518u,
        ["Play_Leader_KinOfSheredyn_Treaty_Economy_Declare01"] = 196559728u,
        ["Play_Leader_KinOfSheredyn_Treaty_Economy_Propose01"] = 1296651248u,
        ["Play_Leader_KinOfSheredyn_Treaty_Insult01"] = 4242599810u,
        ["Play_Leader_KinOfSheredyn_Treaty_Military_Declare_Negative01"] = 639012815u,
        ["Play_Leader_KinOfSheredyn_Treaty_Military_Declare_Negative02"] = 639012812u,
        ["Play_Leader_KinOfSheredyn_Treaty_Military_Declare_Positive01"] = 3111437967u,
        ["Play_Leader_KinOfSheredyn_Treaty_Military_Propose01"] = 1945617277u,
        ["Play_Leader_KinOfSheredyn_Treaty_Refuse01"] = 1148129571u,
        ["Play_Leader_KinOfSheredyn_Treaty_Refuse_Player01"] = 3807077231u,
        ["Play_Leader_KinOfSheredyn_Treaty_Revoke01"] = 2015560213u,
        ["Play_Leader_KinOfSheredyn_Treaty_Revoke_Player01"] = 2295069901u,
        ["Play_Leader_KinOfSheredyn_Treaty_Society_Declare01"] = 126322566u,
        ["Play_Leader_KinOfSheredyn_Treaty_Society_Propose01"] = 3324006758u,
        ["Play_Leader_KinOfSheredyn_Treaty_Thinking01"] = 1060626331u,
    };
}
public static class AudioEventIdentity
{
    public static string? Resolve(uint id, string? name)
    {
        if (id != 0) return LeaderAudioIds.ByName.FirstOrDefault(x => x.Value == id).Key;
        if (name == null) return null;
        return LeaderAudioIds.ByName.ContainsKey(name) ? name : null;
    }
}
public sealed class AudioEventSpec
{
    public uint Id { get; set; }
    public string Spatial { get; set; } = "2d";
    public float Volume { get; set; } = 1;
    public List<string> Clips { get; set; } = new List<string>();
}
public sealed class AudioManifest
{
    public int SchemaVersion { get; set; }
    public Dictionary<string,AudioEventSpec> Events { get; set; } = new Dictionary<string,AudioEventSpec>();
    public static AudioManifest Parse(string json)
    {
        AudioManifest manifest;
        try
        {
            using var reader = new JsonTextReader(new StringReader(json));
            var token = JToken.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            manifest = token.ToObject<AudioManifest>() ?? throw new FormatException("Empty manifest");
        }
        catch (JsonException e) { throw new FormatException("Invalid audio manifest", e); }
        if (manifest.SchemaVersion != 1 || manifest.Events == null || manifest.Events.Count == 0) throw new FormatException("Invalid schema or empty events");
        var ids = new HashSet<uint>();
        foreach (var pair in manifest.Events)
        {
            var e = pair.Value;
            if (!LeaderAudioIds.ByName.TryGetValue(pair.Key, out var expected) || e == null || e.Id != expected || !ids.Add(e.Id)) throw new FormatException("Unknown or duplicate audio event/ID");
            if (e.Spatial != "2d" || float.IsNaN(e.Volume) || float.IsInfinity(e.Volume) || e.Volume < 0 || e.Volume > 1) throw new FormatException("Invalid spatial mode or gain");
            if (e.Clips == null || e.Clips.Count == 0) throw new FormatException("Empty clip list");
            foreach (var path in e.Clips) ValidateClipPath(path);
        }
        return manifest;
    }
    public static void ValidateClipPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains("\\") || path.Contains(":") || path.StartsWith("/", StringComparison.Ordinal) || !path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || path.Split('/').Any(p => p == ".." || p == "." || p.Length == 0)) throw new FormatException("Invalid relative WAV path");
    }
}
public sealed class WavData
{
    public float[] Samples { get; private set; } = Array.Empty<float>();
    public int Channels { get; private set; }
    public int SampleRate { get; private set; }
    public double Duration => (double)Samples.Length / Channels / SampleRate;
    public static WavData Decode(byte[] bytes, bool requireMono = false)
    {
        if (bytes == null || bytes.Length < 12) throw new FormatException("Truncated RIFF header");
        using var stream = new MemoryStream(bytes, false);
        using var reader = new BinaryReader(stream, Encoding.ASCII);
        string Tag() => Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (Tag() != "RIFF") throw new FormatException("Not RIFF");
        uint riffSize = reader.ReadUInt32();
        if (riffSize < 4 || (long)riffSize + 8 != bytes.Length || Tag() != "WAVE") throw new FormatException("Invalid RIFF extent/type");
        int channels = 0, sampleRate = 0, blockAlign = 0;
        byte[]? data = null;
        bool formatFound = false;
        while (stream.Position < stream.Length)
        {
            if (stream.Length - stream.Position < 8) throw new FormatException("Truncated chunk header");
            string tag = Tag(); uint size = reader.ReadUInt32();
            long end = stream.Position + size;
            if (end > stream.Length) throw new FormatException("Truncated chunk");
            if (tag == "fmt ")
            {
                if (formatFound || size < 16) throw new FormatException("Invalid fmt chunk");
                formatFound = true;
                ushort format = reader.ReadUInt16(); channels = reader.ReadUInt16(); sampleRate = reader.ReadInt32();
                uint byteRate = reader.ReadUInt32(); blockAlign = reader.ReadUInt16(); ushort bits = reader.ReadUInt16();
                if (format != 1 || bits != 16 || (channels != 1 && channels != 2) || sampleRate < 8000 || sampleRate > 192000 || blockAlign != channels * 2 || byteRate != (long)sampleRate * blockAlign || (requireMono && channels != 1)) throw new FormatException("Expected PCM16 mono/stereo (mono required for positional clips)");
            }
            else if (tag == "data")
            {
                if (data != null || size > int.MaxValue) throw new FormatException("Duplicate/oversized data");
                data = reader.ReadBytes((int)size);
            }
            stream.Position = end;
            if ((size & 1) != 0) { if (stream.Position == stream.Length) throw new FormatException("Missing chunk pad"); stream.Position++; }
        }
        if (!formatFound || data == null || data.Length == 0 || data.Length % blockAlign != 0) throw new FormatException("Missing or incomplete PCM data");
        var samples = new float[data.Length / 2];
        for (int i = 0; i < samples.Length; i++) samples[i] = unchecked((short)(data[i*2] | data[i*2+1] << 8)) / 32768f;
        return new WavData { Samples = samples, Channels = channels, SampleRate = sampleRate };
    }
}
public static class AudioMath
{
    public static float Volume(bool voiceEnabled, bool masterEnabled, float voiceVolume, float gain)
    {
        if (!voiceEnabled || !masterEnabled || float.IsNaN(voiceVolume) || float.IsNaN(gain)) return 0;
        return Clamp(Clamp(voiceVolume) * gain);
    }
    static float Clamp(float value) => Math.Max(0, Math.Min(1, value));
}
public sealed class PrivateClipRandom
{
    readonly Random random;
    public PrivateClipRandom() { random = new Random(); }
    public PrivateClipRandom(int seed) { random = new Random(seed); }
    public int Next(int count) { if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count)); lock (random) return random.Next(count); }
}
