// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Mono.Cecil;
var root=Path.GetFullPath(args[0]);
var found=new System.Collections.Generic.HashSet<string>();
foreach(var name in new[]{"Amplitude.Framework","Amplitude.UI","Amplitude.Mercury.Data","Amplitude.Wwise","mscorlib","netstandard"})
{
 var path=Path.Combine(root,"ref/managed",name+".dll");
 using var assembly=AssemblyDefinition.ReadAssembly(path);
 Console.WriteLine($"ASSEMBLY {name} {assembly.Name.Version} SHA256 {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))}");
 foreach(var type in assembly.MainModule.Types)
 {
  if(new[]{"LocalizationManager","DatatableElement","Databases","UIMapper","UIRenderingManager","PresentationPawnDefinition","CharacterUIMapper","HeroUnitUIMapper","AudioManager","AkSoundEngine","LocalizationBuilder"}.Contains(type.Name))
  {
   foreach(var m in type.Methods.Where(m=>new[]{"Initialize","Commit","GetImageIndex","GetDocumentInfo","RegisterTexture","PrivateResolveLine","ExtractLinesFromLanguageNode","PostEvent","LoadBank"}.Contains(m.Name))) {Console.WriteLine("METHOD "+m.FullName);found.Add(m.FullName);}
   foreach(var f in type.Fields.Where(f=>new[]{"Pronouns","UnitVoiceGender","Images"}.Contains(f.Name))) Console.WriteLine("FIELD "+f.FullName);
  }
 }
}
foreach(var expected in new[]{
 "System.Void Amplitude.Framework.Databases::Commit()",
 "System.Void Amplitude.Framework.DatatableElement::Initialize()",
 "System.Void Amplitude.Framework.Localization.LocalizationManager::PrivateResolveLine(Amplitude.Framework.Localization.LocalizedStringElement&,System.Int32)",
 "System.Void Amplitude.UI.UIMapper::Initialize()",
 "System.Int32 Amplitude.UI.UIMapper::GetImageIndex(Amplitude.StaticString)",
 "System.Void Amplitude.UI.Renderers.UIRenderingManager::GetDocumentInfo(Amplitude.Framework.Guid,System.Int32,UnityEngine.Object,System.Int32&,UnityEngine.Vector2&,UnityEngine.Rect&,Amplitude.UI.TextureCustomData/VirtualTextureEntry&,System.Boolean&)",
 "Amplitude.Framework.Guid Amplitude.UI.Renderers.UIRenderingManager::RegisterTexture(UnityEngine.Texture)",
 "System.Void Amplitude.Mercury.Data.World.PresentationPawnDefinition::Initialize()",
 "System.Void Amplitude.Framework.Localization.LocalizationProcessor.LocalizationBuilder::ExtractLinesFromLanguageNode(Amplitude.Framework.Localization.NonProcessedLocalizationString/List,System.Collections.Generic.Dictionary`2<System.String,System.Exception>&,System.Action`2<System.String,System.Single>)"
}) if(!found.Contains(expected))throw new Exception("Missing expected target: "+expected);
Console.WriteLine("VERIFIED 7 hook signatures and 2 helper signatures");
using(var a=AssemblyDefinition.ReadAssembly(Path.Combine(root,"plugin/.tools/bepinex/BepInEx/core/0Harmony.dll"))) Console.WriteLine("HARMONY "+a.Name.FullName);
// Execute only the managed localization compiler, without starting Unity or the game.
if(args.Contains("--compile-text"))
{
 System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (_,name) => {
  string candidate=Path.Combine(root,"ref/managed",name.Name+".dll");
  return File.Exists(candidate)?System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate):null;
 };
 var framework=System.Reflection.Assembly.LoadFrom(Path.Combine(root,"ref/managed/Amplitude.Framework.dll"));
 Type Ty(string n)=>framework.GetType("Amplitude.Framework.Localization."+n,true)!;
 object? Get(object o,string n)=>o.GetType().GetField(n,System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(o);
 var map=System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string,string>>(File.ReadAllText(Path.Combine(root,"plugin/rules/text.json")))!;
 int count=0,parameters=0;
 foreach(var pair in map)
 {
  var builder=Activator.CreateInstance(Ty("LocalizationProcessor.LocalizationBuilder"))!;
  var context=Get(builder,"parsingContext")!;
  context.GetType().GetField("TagToCodeMap")!.SetValue(context,new System.Collections.Generic.Dictionary<string,int>());
  var row=Activator.CreateInstance(Ty("NonProcessedLocalizationString"))!;
  foreach(var p in new[]{("Id","%"+pair.Key),("Body",pair.Value),("Tags",""),("ExcelIdentifier",pair.Key)}) row.GetType().GetField(p.Item1)!.SetValue(row,p.Item2);
  var list=(System.Collections.IList)Activator.CreateInstance(Ty("NonProcessedLocalizationString+List"))!;list.Add(row);
  var errors=new System.Collections.Generic.Dictionary<string,Exception>();
  builder.GetType().GetMethod("ExtractLinesFromLanguageNode",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(builder,new object?[]{list,errors,null});
  if(errors.Count>0)throw new Exception(pair.Key+": "+string.Join("; ",errors.Values.Select(e=>e.Message)));
  var lines=(System.Collections.IList)Get(builder,"lineList")!;
  if(lines.Count!=1)throw new Exception("No line for "+pair.Key);
  foreach(var node in (Array)Get(lines[0]!,"CompactedNodes")!)if(Get(node!,"Id")!.ToString()=="InsertionFromParameter")parameters++;
  count++;
 }
 Console.WriteLine($"COMPILED {count} replacement lines; parameter insertion nodes {parameters}");
}

// Exercise our actual generic deserialization calls against the game's JSON assembly.
var jsonPath=Path.Combine(root,"ref/managed/Newtonsoft.Json.dll");
var loadedJson=typeof(Newtonsoft.Json.JsonConvert).Assembly;
if(!SHA256.HashData(File.ReadAllBytes(jsonPath)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(loadedJson.Location))))
 throw new Exception("Probe did not load the game's Newtonsoft.Json bytes");
var parsedRules=KinOfSheredyn.RuleSet.Parse(File.ReadAllText(Path.Combine(root,"plugin/rules/rules.json")));
var parsedText=KinOfSheredyn.TextOverrides.LoadJson(File.ReadAllText(Path.Combine(root,"plugin/rules/text.json")));
if(parsedRules.Fields.Count==0 || parsedText.Count!=103)throw new Exception("Game JSON deserialization failed");
Console.WriteLine($"VERIFIED game Newtonsoft.Json {loadedJson.GetName().Version}: {parsedRules.Fields.Count} field rules, {parsedText.Count} text entries");
// Verify packaging embeds exactly the canonical startup notice, without loading Unity.
if(args.Contains("--check-package"))
using(var pluginAssembly=AssemblyDefinition.ReadAssembly(Path.Combine(root,"plugin/dist/KinOfSheredyn/BepInEx/plugins/KinOfSheredyn/KinOfSheredyn.dll")))
{
 var notice=pluginAssembly.MainModule.Resources.OfType<EmbeddedResource>().Single(r=>r.Name=="KinOfSheredyn.DISCLAIMER.txt");
 if(!notice.GetResourceData().SequenceEqual(File.ReadAllBytes(Path.Combine(root,"dist/DISCLAIMER.txt"))))
  throw new Exception("Embedded save notice differs from canonical file");
 Console.WriteLine("VERIFIED embedded startup save notice matches dist/DISCLAIMER.txt byte for byte");
}
