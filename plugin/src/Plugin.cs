// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace KinOfSheredyn;
[BepInPlugin("org.el2.kinofsheredyn", "Kin of Sheredyn", "0.1.0")]
public sealed class Plugin : BaseUnityPlugin
{
    static Plugin Self = null!;
    Harmony harmony = null!;
    RuleSet rules = new RuleSet();
    Dictionary<string,string> text = new Dictionary<string,string>();
    readonly Dictionary<string,object> compiled = new Dictionary<string,object>();
    sealed class ImageBinding { public string Mapper=""; public object OriginalGuid=null!; }
    readonly Dictionary<object,ImageBinding> images = new Dictionary<object,ImageBinding>();
    readonly List<(Array images,int index,object original)> restoredImages = new List<(Array,int,object)>();
    readonly HashSet<string> failedAssets = new HashSet<string>();
    bool textureHookReady;
    readonly Dictionary<string,Texture2D> textures = new Dictionary<string,Texture2D>();
    readonly HashSet<object> managers = new HashSet<object>();
    readonly HashSet<string> warnings = new HashSet<string>();
    bool localization, data, texture;
    string folder = "";
    LeaderPlaybackBridge? audio;
    static Type T(string name) => AccessTools.TypeByName(name) ?? throw new MissingMemberException(name);
    static object? Get(object o, string name) => AccessTools.Field(o.GetType(), name)?.GetValue(o) ?? AccessTools.Property(o.GetType(), name)?.GetValue(o, null);
    static string RecordName(object o) => (Get(o, "Name") ?? Get(o, "name"))?.ToString() ?? "";
    void Warn(string key, Exception e) { if(warnings.Add(key)) Logger.LogWarning(key + ": " + e.Message); }
    void Awake()
    {
        Self=this; folder=Path.GetDirectoryName(Info.Location)!;
        StartupNotices();
        localization=Config.Bind("Features","Localization",true,"English replacement strings; restart required").Value;
        data=Config.Bind("Features","DataFields",true,"Named pronoun and voice fields; restart required").Value;
        texture=Config.Bind("Features","Textures",true,"Rule-selected PNG portraits; restart required").Value;
        var replaceLeader=Config.Bind("Audio","ReplaceLeaderVoice",true,"Replace preloaded leader dialogue");
        var replaceGrunts=Config.Bind("Audio","ReplaceUnitGrunts",false,"Reserved; grunt hooks are disabled in this release");
        var logAudio=Config.Bind("Audio","LogAudioReplacements",false,"Log successful leader replacements");
        if(replaceGrunts.Value)Logger.LogWarning("ReplaceUnitGrunts requested; grunt hooks are not implemented and remain disabled");
        try { rules=RuleSet.Parse(File.ReadAllText(Path.Combine(folder,"rules","rules.json"))); }
        catch(Exception e) { Warn("Rules load; data/textures disabled",e); data=texture=false; }
        try { text=TextOverrides.LoadJson(File.ReadAllText(Path.Combine(folder,"rules","text.json"))); }
        catch(Exception e) { Warn("Text load; localization disabled",e);localization=false; }
        if (!CheckBuild()) {localization=data=texture=false;Logger.LogWarning("Unsupported assembly hashes: all overrides disabled");}
        harmony=new Harmony("org.el2.kinofsheredyn");
        foreach(var target in new[]{("Amplitude.Mercury.UI.CharacterUIMapper","Pronouns"),("Amplitude.Mercury.UI.HeroUnitUIMapper","Pronouns"),("Amplitude.Mercury.Data.World.PresentationPawnDefinition","UnitVoiceGender")})
            try { var field=AccessTools.Field(T(target.Item1),target.Item2) ?? throw new MissingFieldException(target.Item1,target.Item2);Logger.LogInfo("FOUND field " + field.DeclaringType!.FullName+"."+field.Name+" : "+field.FieldType); }
            catch(Exception e) {Warn("MISSING field "+target,e);}
        // Exact signatures avoid silently patching a new overload after a game update.
        Hook("Amplitude.Framework.Localization.LocalizationManager","PrivateResolveLine",new[]{"Amplitude.Framework.Localization.LocalizedStringElement&","System.Int32"},nameof(Localize),true,localization);
        Hook("Amplitude.Framework.DatatableElement","Initialize",Array.Empty<string>(),nameof(RecordLoaded),false,data);
        Hook("Amplitude.Mercury.Data.World.PresentationPawnDefinition","Initialize",Array.Empty<string>(),nameof(RecordLoaded),false,data);
        Hook("Amplitude.UI.UIMapper","Initialize",Array.Empty<string>(),nameof(MapperLoaded),false,data||texture);
        Hook("Amplitude.Framework.Databases","Commit",Array.Empty<string>(),nameof(Sweep),false,data||texture);
        Hook("Amplitude.UI.UIMapper","GetImageIndex",new[]{"Amplitude.StaticString"},nameof(MapperRead),true,texture);
        Hook("Amplitude.UI.Renderers.UIRenderingManager","GetDocumentInfo",new[]{"Amplitude.Framework.Guid","System.Int32","UnityEngine.Object","System.Int32&","UnityEngine.Vector2&","UnityEngine.Rect&","Amplitude.UI.TextureCustomData+VirtualTextureEntry&","System.Boolean&"},nameof(TextureLookup),true,texture);
        // Builder dependencies are also logged before the first lookup.
        try { var builder=T("Amplitude.Framework.Localization.LocalizationProcessor.LocalizationBuilder"); Logger.LogInfo("FOUND " + AccessTools.Method(builder,"ExtractLinesFromLanguageNode")); }
        catch(Exception e) { Warn("MISSING localization builder",e); localization=false; }
        Sweep();
        try {audio=new LeaderPlaybackBridge(folder,replaceLeader,logAudio,s=>Logger.LogInfo(s));audio.Install(harmony);}
        catch(Exception e) {audio?.Dispose();audio=null;Warn("Audio hooks disabled",e);}
    }
    void Update() => audio?.Update();
    void OnEnable() {if(audio!=null)audio.Active=true;}
    void OnDisable() {if(audio!=null){audio.Active=false;audio.StopAll();}}
    void StartupNotices()
    {
        try
        {
            using(var stream=typeof(Plugin).Assembly.GetManifestResourceStream("KinOfSheredyn.DISCLAIMER.txt"))
            using(var reader=new StreamReader(stream ?? throw new FileNotFoundException("Embedded save notice")))
                Logger.LogWarning(reader.ReadToEnd());
        }
        catch(Exception e) { Logger.LogError("Save-compatibility notice unavailable: " + e.Message); }
        // Diagnostics are independent: a failed check must not suppress the remaining checks or hooks.
        try
        {
            var version=typeof(BaseUnityPlugin).Assembly.GetName().Version;
            if(version==null || version.Major!=5 || version.Minor!=4 || version.Build!=23)
                Logger.LogError("Unsupported BepInEx version " + version + "; required 5.4.23.x (tested 5.4.23.5). Install the Windows x64 loader beside Endless Legend 2.exe.");
        }
        catch(Exception e) { Logger.LogError("Cannot check BepInEx version: " + e.Message); }
        try
        {
            // Hazardous is a preloader; it may be visible as an assembly rather than a Unity plugin.
            bool Identity(string value) => value.IndexOf("Hazardous",StringComparison.OrdinalIgnoreCase)>=0 ||
                value.StartsWith("HZ.EL2.PluginLoader",StringComparison.OrdinalIgnoreCase);
            bool loader=AppDomain.CurrentDomain.GetAssemblies().Any(a=>Identity(a.GetName().Name ?? "")) ||
                BepInEx.Bootstrap.Chainloader.PluginInfos.Any(p=>Identity(p.Key) || Identity(p.Value.Metadata.Name) ||
                    Identity(Path.GetFileNameWithoutExtension(p.Value.Location)));
            if(!loader) Logger.LogError("Hazardous BepInEx Mod Loader not detected. Subscribe to Workshop item 3806104364 and install its manual bootstrap; continuing with available features.");
        }
        catch(Exception e) { Logger.LogError("Cannot check Hazardous loader: " + e.Message); }
    }
    bool CheckBuild()
    {
        bool ok=true;
        foreach(var pin in new Dictionary<string,string> {
            {"Amplitude.Framework","95228A49906F6975CCA31C8D599B0506786ACBFA615ED53C42E8C0FB5E70FCB3"},
            {"Amplitude.UI","70765DF15CE112EF1D2B3CD13926AE7D023BB69638E168CBCE0F5B5B85F93DD9"},
            {"Amplitude.Mercury.Data","58377063B71C800C1C26C7F64BE90CA373B6EA55D0D0C4D4B54159556EE7EC07"}
        }) try
        {
            var assembly=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetName().Name==pin.Key) ?? Assembly.Load(pin.Key);
            using(var sha=SHA256.Create()) using(var stream=File.OpenRead(assembly.Location))
            {
                string actual=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
                bool match=actual==pin.Value;ok &= match;
                Logger.LogInfo((match?"SUPPORTED ":"MISMATCH ")+pin.Key+" SHA256 "+actual);
            }
        }
        catch(Exception e) {Warn("Build guard "+pin.Key,e);ok=false;}
        return ok;
    }
    void Hook(string type,string method,string[] args,string callback,bool prefix,bool enabled)
    {
        try
        {
            var types=args.Select(n => { bool byref=n.EndsWith("&",StringComparison.Ordinal);var t=T(byref?n.Substring(0,n.Length-1):n);return byref?t.MakeByRefType():t; }).ToArray();
            var target=AccessTools.DeclaredMethod(T(type),method,types) ?? throw new MissingMethodException(type,method);
            Logger.LogInfo("FOUND " + target.DeclaringType!.FullName + "." + target + (enabled?" [enabled]":" [disabled]"));
            if(enabled) {var hook=new HarmonyMethod(typeof(Plugin),callback);harmony.Patch(target,prefix:prefix?hook:null,postfix:prefix?null:hook);if(callback==nameof(TextureLookup))textureHookReady=true;}
        }
        catch(Exception e) {Warn("MISSING/FAILED " + type+"."+method,e);}
    }
    static void Localize(object[] __args)
    {
        if(!Self.localization) return;
        try
        {
            string key=Get(__args[0],"LineId")?.ToString() ?? "";
            if(!TextOverrides.TryGet(Self.text,key,out var replacement))return;
            if(!Self.compiled.TryGetValue(key,out var line))
            {
                // Use the stock compiler so parameter insertions and rich text retain stock semantics.
                var builder=Activator.CreateInstance(T("Amplitude.Framework.Localization.LocalizationProcessor.LocalizationBuilder"))!;
                var context=Get(builder,"parsingContext")!;
                AccessTools.Field(context.GetType(),"TagToCodeMap").SetValue(context,new Dictionary<string,int>());
                var row=Activator.CreateInstance(T("Amplitude.Framework.Localization.NonProcessedLocalizationString"))!;
                foreach(var pair in new[]{("Id",key),("Body",replacement),("Tags",""),("ExcelIdentifier",key)}) AccessTools.Field(row.GetType(),pair.Item1).SetValue(row,pair.Item2);
                var list=(IList)Activator.CreateInstance(T("Amplitude.Framework.Localization.NonProcessedLocalizationString+List"))!;list.Add(row);
                var errors=new Dictionary<string,Exception>();
                AccessTools.Method(builder.GetType(),"ExtractLinesFromLanguageNode").Invoke(builder,new object?[]{list,errors,null});
                if(errors.Count!=0)throw new FormatException(string.Join("; ",errors.Values.Select(e=>e.Message)));
                var lines=(IList)Get(builder,"lineList")!;
                if(lines.Count!=1)throw new FormatException("Compiler produced no line");
                line=lines[0]!;Self.compiled.Add(key,line);
            }
            __args[0]=line;__args[1]=0;
        }
        catch(Exception e) {Self.Warn("Localization replacement",e);}
    }
    static void RecordLoaded(object __instance) => Self.ApplyFields(__instance);
    void ApplyFields(object record)
    {
        if(!data)return;
        foreach(var rule in rules.Fields.Where(f=>f.Record==RecordName(record)))
            try
            {
                var field=AccessTools.Field(record.GetType(),rule.Field);
                // DatatableElement.Initialize runs on unrelated record types sharing a name too.
                if(field==null)continue;
                var value=Enum.Parse(field.FieldType,rule.Value,false);
                if(!value.Equals(field.GetValue(record))) {field.SetValue(record,value);Logger.LogInfo("Override " + rule.Record+"."+rule.Field+"="+rule.Value);}
            }
            catch(Exception e) {Warn("Field " + rule.Record+"."+rule.Field,e);}
    }
    static void MapperLoaded(object __instance) { Self.ApplyFields(__instance);Self.MapImages(__instance); }
    static void MapperRead(object __instance) => Self.MapImages(__instance);
    void MapImages(object mapper)
    {
        if(!texture || !textureHookReady)return;
        try
        {
            string name=RecordName(mapper);
            if(!rules.Contexts.ContainsKey(name))return;
            if(Get(mapper,"Images") is Array entries)
                for(int i=0;i<entries.Length;i++)
                {
                    var entry=entries.GetValue(i)!;
                    string slot=Get(entry,"Key")?.ToString() ?? "";
                    var uiTexture=Get(entry,"Value")!;
                    var guid=Get(uiTexture,"Guid")!;
                    if(images.ContainsKey(guid))continue;
                    string key=name+"_"+slot;
                    if(rules.Select(key,rules.Contexts[name])==null)continue;
                    // Isolate mapper slots even when multiple stock slots share an atlas GUID.
                    var synthetic=AccessTools.Method(guid.GetType(),"NewGuid",Type.EmptyTypes).Invoke(null,null);
                    var replacement=Activator.CreateInstance(uiTexture.GetType(),new object?[]{synthetic,Get(uiTexture,"Flags"),Get(uiTexture,"ColorFormat"),null})!;
                    var virtualKey=AccessTools.Field(uiTexture.GetType(),"virtualTextureKeyHash");
                    virtualKey.SetValue(replacement,virtualKey.GetValue(uiTexture));
                    restoredImages.Add((entries,i,System.Runtime.CompilerServices.RuntimeHelpers.GetObjectValue(entry)));
                    images.Add(synthetic,new ImageBinding{Mapper=key,OriginalGuid=guid});
                    AccessTools.Field(entry.GetType(),"Value").SetValue(entry,replacement);
                    entries.SetValue(entry,i);
                }
        }
        catch(Exception e) {Warn("Mapper images",e);}
    }
    static void TextureLookup(object __instance,object[] __args)
    {
        if(!Self.texture)return;
        try
        {
            if(!Self.images.TryGetValue(__args[0],out var binding))return;
            __args[0]=binding.OriginalGuid; // every failure continues with the original atlas entry
            string mapper=binding.Mapper;
            var rule=Self.rules.Textures.FirstOrDefault(r=>r.Mapper==mapper && Self.rules.Contexts.Any(c=>r.When.Matches(c.Value) && mapper.StartsWith(c.Key+"_",StringComparison.Ordinal)));
            if(rule==null)return;
            string file=RuleSet.AssetPath(Self.folder,rule.File);
            if(Self.failedAssets.Contains(file))return;
            if(!Self.textures.TryGetValue(file,out var tex))
            {
                byte[] png;
                try {png=File.ReadAllBytes(file);} catch {Self.failedAssets.Add(file);throw;}
                tex=new Texture2D(2,2,TextureFormat.RGBA32,false);
                try { if(!ImageConversion.LoadImage(tex,png,true))throw new FormatException("Invalid PNG"); }
                catch {Destroy(tex);Self.failedAssets.Add(file);throw;}
                tex.name="Kin replacement: "+mapper;Self.textures.Add(file,tex);
            }
            // RegisterTexture deduplicates by Texture and survives manager atlas restarts.
            var guid=AccessTools.Method(__instance.GetType(),"RegisterTexture",new[]{typeof(Texture)}).Invoke(__instance,new object[]{tex});
            Self.managers.Add(__instance);
            __args[0]=guid;__args[1]=0; // standalone full rectangle; no stock virtual subrectangle
        }
        catch(Exception e) {Self.Warn("Texture lookup; retaining original",e);}
    }
    static void Sweep()
    {
        try
        {
            var map=(IDictionary)AccessTools.Field(T("Amplitude.Framework.Databases"),"DatabasesMap").GetValue(null);
            foreach(var db in map.Values) if(db is IEnumerable rows)foreach(var row in rows) {Self.ApplyFields(row!);if(AccessTools.Field(row!.GetType(),"Images")!=null)Self.MapImages(row);}
        }
        catch(Exception e) {Self.Warn("Database sweep",e);}
    }
    void OnDestroy()
    {
        foreach(var restore in restoredImages)restore.images.SetValue(restore.original,restore.index);
        restoredImages.Clear();
        audio?.Dispose();
        harmony?.UnpatchSelf();
        foreach(var manager in managers) foreach(var tex in textures.Values)
            try {AccessTools.Method(manager.GetType(),"UnregisterTexture",new[]{typeof(Texture)}).Invoke(manager,new object[]{tex});}
            catch(Exception e) {Warn("Unregister texture",e);}
        foreach(var tex in textures.Values)Destroy(tex);
        textures.Clear();managers.Clear();images.Clear();compiled.Clear();
    }
}
