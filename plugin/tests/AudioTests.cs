// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.IO;
using System.Linq;
using System.Text;
using KinOfSheredyn;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

public sealed class AudioTests
{
    static string Manifest() => JsonConvert.SerializeObject(new { schemaVersion=1, events=LeaderAudioIds.ByName.Take(1).ToDictionary(p=>p.Key,p=>new {id=p.Value,spatial="2d",volume=1,clips=new[]{"leader/voice.wav"}}) });
    [Fact] public void ManifestAcceptsAuthoritativeIdAndSafeClip()
    {
        var manifest=AudioManifest.Parse(Manifest()); Assert.Single(manifest.Events); Assert.Equal(57,LeaderAudioIds.ByName.Count);
    }
    [Theory] [InlineData("id")] [InlineData("name")] [InlineData("clips")] [InlineData("events")] [InlineData("gain")] [InlineData("spatial")]
    public void ManifestRejectsUnknownIdsAndEmptyLists(string mutation)
    {
        var json=JObject.Parse(Manifest()); var events=(JObject)json["events"]!; var entry=(JObject)events.Properties().First().Value;
        if(mutation=="id") entry["id"]=123;
        if(mutation=="name") { var value=events.Properties().First().Value; events.RemoveAll(); events["Unknown"]=value; }
        if(mutation=="clips") entry["clips"]=new JArray();
        if(mutation=="events") events.RemoveAll();
        if(mutation=="gain") entry["volume"]=-1;
        if(mutation=="spatial") entry["spatial"]="3d";
        Assert.Throws<FormatException>(()=>AudioManifest.Parse(json.ToString()));
    }
    [Fact] public void ManifestRejectsDuplicatePropertiesAndIds()
    {
        var json=Manifest(); var name=LeaderAudioIds.ByName.First().Key;
        var entry=((JObject)JObject.Parse(json)["events"]!)[name]!.ToString();
        Assert.Throws<FormatException>(()=>AudioManifest.Parse("{\"schemaVersion\":1,\"events\":{\""+name+"\":"+entry+",\""+name+"\":"+entry+"}}"));
        var source=JObject.Parse(json); var events=(JObject)source["events"]!;
        events[LeaderAudioIds.ByName.Skip(1).First().Key]=JToken.Parse(entry);
        Assert.Throws<FormatException>(()=>AudioManifest.Parse(source.ToString()));
    }
    [Theory] [InlineData("../evil.wav")] [InlineData("leader/../evil.wav")] [InlineData("/tmp/a.wav")] [InlineData("C:\\a.wav")] [InlineData("leader\\a.wav")] [InlineData("leader//a.wav")] [InlineData("leader/a.mp3")]
    public void ManifestRejectsTraversalAndUnsafePaths(string path) => Assert.Throws<FormatException>(()=>AudioManifest.ValidateClipPath(path));
    static byte[] Wav(ushort channels=1,ushort format=1)
    {
        using var stream=new MemoryStream(); using(var writer=new BinaryWriter(stream,Encoding.ASCII,true))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36+channels*4); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write(format);writer.Write(channels);writer.Write(48000);writer.Write(48000*channels*2);writer.Write((ushort)(channels*2));writer.Write((ushort)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(channels*4);
            for(int i=0;i<channels*2;i++) writer.Write((short)(i%2==0?short.MinValue:short.MaxValue));
        }
        return stream.ToArray();
    }
    [Fact] public void Pcm16SamplesAndDurationAreDecoded()
    {
        var wav=WavData.Decode(Wav()); Assert.Equal(48000,wav.SampleRate);Assert.Equal(1,wav.Channels);Assert.Equal(-1,wav.Samples[0]); Assert.InRange(wav.Samples[1],0.9999f,1f);Assert.Equal(2d/48000,wav.Duration);
    }
    [Fact] public void StereoIsPreservedForLeaderAndRejectedForGrunts()
    {
        Assert.Equal(2,WavData.Decode(Wav(2)).Channels); Assert.Throws<FormatException>(()=>WavData.Decode(Wav(2),true));
    }
    [Fact] public void TruncationAndNonPcmAreRejected()
    {
        var bytes=Wav();for(int length=0;length<bytes.Length;length++) Assert.Throws<FormatException>(()=>WavData.Decode(bytes.Take(length).ToArray()));
        Assert.Throws<FormatException>(()=>WavData.Decode(Wav(1,3)));
    }
    [Fact] public void NonzeroIdWinsAndNamesAreExact()
    {
        var pair=LeaderAudioIds.ByName.First(); Assert.Equal(pair.Key,AudioEventIdentity.Resolve(pair.Value,"wrong")); Assert.Null(AudioEventIdentity.Resolve(123,pair.Key));Assert.Equal(pair.Key,AudioEventIdentity.Resolve(0,pair.Key));Assert.Null(AudioEventIdentity.Resolve(0,pair.Key.ToLowerInvariant()));Assert.Null(AudioEventIdentity.Resolve(0,null));
        var signed=LeaderAudioIds.ByName.First(p=>p.Value>int.MaxValue); Assert.Equal(signed.Key,AudioEventIdentity.Resolve(unchecked((uint)(int)signed.Value),null));
    }
    [Fact] public void VolumeUsesVoiceGainAndMuteWithoutSquaringMaster()
    {
        Assert.Equal(0.2f,AudioMath.Volume(true,true,0.5f,0.4f));Assert.Equal(0,AudioMath.Volume(false,true,1,1));Assert.Equal(0,AudioMath.Volume(true,false,1,1));Assert.Equal(1,AudioMath.Volume(true,true,2,1));Assert.Equal(0,AudioMath.Volume(true,true,-1,1));
    }
    [Fact] public void SelectionHasItsOwnRandomSequence()
    {
        var a=new PrivateClipRandom(37);var b=new PrivateClipRandom(37);var unrelated=new Random(2);
        for(int i=0;i<100;i++){ unrelated.Next();Assert.Equal(a.Next(11),b.Next(11)); }
        Assert.Throws<ArgumentOutOfRangeException>(()=>a.Next(0));
    }
}
