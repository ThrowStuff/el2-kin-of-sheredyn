// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KinOfSheredyn;

// Only the verified LeaderActionDialog consumer may receive the no-Wwise-playback sentinel.
internal sealed class LeaderPlaybackBridge : IDisposable
{
    static LeaderPlaybackBridge? self;
    [ThreadStatic] static object? scope;
    readonly int mainThread = Thread.CurrentThread.ManagedThreadId;
    readonly ConfigEntry<bool> enabled, trace;
    readonly Action<string> log;
    readonly Dictionary<string,(AudioEventSpec spec,AudioClip[] clips)> cache = new Dictionary<string,(AudioEventSpec,AudioClip[])>();
    readonly List<Slot> slots = new List<Slot>();
    readonly PrivateClipRandom random = new PrivateClipRandom();
    readonly GameObject owner;
    object? options;
    AudioListener? listener;
    bool disposed;
    public bool Active = true;
    sealed class Slot { public AudioSource Source=null!; public object? Action; public float Gain; public float Fade=-1; }
    static Type T(string name) => AccessTools.TypeByName(name) ?? throw new TypeLoadException(name);
    static object? Get(object value,string name) => AccessTools.Property(value.GetType(),name)?.GetValue(value,null) ?? AccessTools.Field(value.GetType(),name)?.GetValue(value);
    public LeaderPlaybackBridge(string folder, ConfigEntry<bool> enabled, ConfigEntry<bool> trace, Action<string> log)
    {
        this.enabled=enabled;this.trace=trace;this.log=log;
        owner=new GameObject("Kin leader audio");UnityEngine.Object.DontDestroyOnLoad(owner);
        try
        {
            var manifest=AudioManifest.Parse(File.ReadAllText(Path.Combine(folder,"audio","events.json")));
            foreach(var pair in manifest.Events)
            {
                var loaded=new List<AudioClip>();
                try
                {
                    foreach(string file in pair.Value.Clips)
                    {
                        var wav=WavData.Decode(File.ReadAllBytes(Path.Combine(folder,"audio",file)));
                        var clip=AudioClip.Create(pair.Key,wav.Samples.Length/wav.Channels,wav.Channels,wav.SampleRate,false);
                        loaded.Add(clip);
                        if(!clip.SetData(wav.Samples,0))throw new FormatException("AudioClip.SetData failed");
                    }
                    cache.Add(pair.Key,(pair.Value,loaded.ToArray()));
                }
                catch(Exception e) {foreach(var clip in loaded)UnityEngine.Object.Destroy(clip);log("Stock audio for "+pair.Key+": "+e.Message);}
            }
        }
        catch(Exception e) {log("Audio manifest unavailable; retaining stock audio: "+e.Message);}
        SceneManager.sceneUnloaded+=SceneUnloaded;
    }
    public void Install(Harmony harmony)
    {
        var dialog=T("Amplitude.Mercury.Presentation.LeaderActionDialog");
        var manager=T("Amplitude.Wwise.Audio.AudioManager");
        var handle=T("Amplitude.Wwise.Audio.AudioEventHandle");var guid=T("Amplitude.Wwise.Audio.AudioEntityGUID");
        MethodInfo Exact(Type t,string n,params Type[] args) => AccessTools.DeclaredMethod(t,n,args) ?? throw new MissingMethodException(t.FullName,n);
        var start=Exact(dialog,"OnStart");var update=Exact(dialog,"OnUpdate",typeof(float));
        var stop=Exact(dialog,"OnStop",T("Amplitude.Mercury.Presentation.Action+StopReason"));
        var post=Exact(manager,"PostEvent",handle,guid);
        var callback=Exact(manager,"PostEvent",handle,guid,T("Amplitude.Wwise.Interop.AkCallbackType"),T("Amplitude.Wwise.Interop.AkCallbackManager+EventCallback"),typeof(object));
        // Resolve every target before installing any patch.
        self=this;
        foreach(var method in new[]{start,update})harmony.Patch(method,prefix:new HarmonyMethod(typeof(LeaderPlaybackBridge),nameof(Enter)),finalizer:new HarmonyMethod(typeof(LeaderPlaybackBridge),nameof(Leave)));
        harmony.Patch(stop,prefix:new HarmonyMethod(typeof(LeaderPlaybackBridge),nameof(Stop)));
        harmony.Patch(post,prefix:new HarmonyMethod(typeof(LeaderPlaybackBridge),nameof(Post)));
        harmony.Patch(callback,prefix:new HarmonyMethod(typeof(LeaderPlaybackBridge),nameof(CallbackStock)));
        log("Leader audio hooks installed; "+cache.Count+" events preloaded; callbacks and grunts remain stock");
    }
    static void Enter(object __instance,out object? __state) {__state=scope;scope=__instance;}
    static Exception? Leave(Exception? __exception,object? __state) {scope=__state;return __exception;}
    static void CallbackStock() { } // Explicitly audited overload: never suppress callbacks.
    static void Stop(object __instance,object[] __args)
    {
        if(self==null || Thread.CurrentThread.ManagedThreadId!=self.mainThread)return;
        foreach(var slot in self.slots)if(ReferenceEquals(slot.Action,__instance))
        {if(__args[0].ToString()=="Interrupt")slot.Fade=0.1f;else self.Release(slot);}
    }
    static bool Post(object __instance,object[] __args,ref uint __result)
    {
        var bridge=self;
        if(bridge==null || bridge.disposed || !bridge.Active || !bridge.enabled.Value || scope==null || Thread.CurrentThread.ManagedThreadId!=bridge.mainThread)return true;
        Slot? started=null;
        try
        {
            var handle=__args[0];
            if(handle==null || !ReferenceEquals(Get(scope,"dialogAudioHandle"),handle))return true;
            if(!(Get(__instance,"Initialized") is bool ready) || !ready)return true;
            if(!Equals(Get(__instance,"audioManagerGUID"),__args[1]))return true;
            if(!(AccessTools.Method(__instance.GetType(),"CheckTags").Invoke(__instance,new[]{__args[1]}) is bool eligible) || !eligible)return true;
            string? name=AudioEventIdentity.Resolve((uint)Get(handle,"ID")!,Get(handle,"EditorName") as string ?? "");
            if(name==null || !bridge.cache.TryGetValue(name,out var entry))return true;
            bridge.options=__instance;
            if(!bridge.PrepareListener())return true;
            // A repeated post for the same action must not start another source.
            if(bridge.slots.Any(s=>ReferenceEquals(s.Action,scope))) {__result=uint.MaxValue;return false;}
            started=bridge.slots.FirstOrDefault(s=>s.Action==null);
            if(started==null)
            {
                if(bridge.slots.Count>=8)return true;
                started=new Slot{Source=bridge.owner.AddComponent<AudioSource>()};bridge.slots.Add(started);
                started.Source.playOnAwake=false;started.Source.spatialBlend=0;started.Source.dopplerLevel=0;
            }
            started.Action=scope;started.Gain=entry.spec.Volume;started.Fade=-1;
            started.Source.clip=entry.clips[bridge.random.Next(entry.clips.Length)];
            bridge.ApplyVolume(started);started.Source.Play();
            if(!started.Source.isPlaying) {bridge.Release(started);return true;}
            __result=uint.MaxValue;
            if(bridge.trace.Value)try {bridge.log("Replaced leader audio "+name);}catch { }
            return false;
        }
        catch {if(started!=null)bridge.Release(started);return true;}
    }
    bool PrepareListener()
    {
        var active=UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Where(l=>l.isActiveAndEnabled).ToArray();
        if(active.Length>1)return false;
        var target=options==null?null:Get(options,"DefaultListener") as Component;
        if(active.Length==1 && active[0]!=listener)return true;
        if(target==null || !target.gameObject.activeInHierarchy)return false;
        if(listener==null)listener=owner.AddComponent<AudioListener>();
        listener.enabled=true;listener.transform.SetPositionAndRotation(target.transform.position,target.transform.rotation);
        return true;
    }
    void ApplyVolume(Slot slot)
    {
        if(options==null)throw new InvalidOperationException("No audio options");
        float fade=slot.Fade<0?1:Mathf.Clamp01(slot.Fade/0.1f);
        slot.Source.volume=AudioMath.Volume((bool)Get(options,"VoiceEnabled")!,(bool)Get(options,"MasterEnabled")!,Convert.ToSingle(Get(options,"VoiceVolume")),slot.Gain)*fade;
    }
    void Release(Slot slot) {slot.Source.Stop();slot.Source.clip=null;slot.Action=null;slot.Fade=-1;}
    public void Update()
    {
        if(disposed)return;
        try
        {
            // Yield our listener whenever the game supplies one; never create duplicate listeners.
            if(listener!=null && UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Any(l=>l!=listener && l.isActiveAndEnabled))listener.enabled=false;
            foreach(var slot in slots)if(slot.Action!=null)
            {
                if(!enabled.Value || !slot.Source.isPlaying) {Release(slot);continue;}
                if(!PrepareListener()) {Release(slot);continue;}
                if(slot.Fade>=0) {slot.Fade-=Time.unscaledDeltaTime;if(slot.Fade<=0){Release(slot);continue;}}
                ApplyVolume(slot);
            }
        }
        catch {StopAll();}
    }
    void SceneUnloaded(Scene scene) => StopAll();
    public void StopAll() {foreach(var slot in slots)Release(slot);if(listener!=null)listener.enabled=false;}
    public void Dispose()
    {
        disposed=true;StopAll();SceneManager.sceneUnloaded-=SceneUnloaded;
        foreach(var entry in cache.Values)foreach(var clip in entry.clips)UnityEngine.Object.Destroy(clip);
        cache.Clear();UnityEngine.Object.Destroy(owner);if(self==this)self=null;
    }
}
