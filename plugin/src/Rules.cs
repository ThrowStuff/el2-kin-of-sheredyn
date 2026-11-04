// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace KinOfSheredyn;
public sealed class Criteria
{
    public string? Faction { get; set; }
    public string? Character { get; set; }
    public string? Gender { get; set; }
    public bool Matches(Criteria actual) => Match(Faction, actual.Faction) && Match(Character, actual.Character) && Match(Gender, actual.Gender);
    static bool Match(string? wanted, string? actual) => wanted == null || string.Equals(wanted, actual, StringComparison.Ordinal);
}
public sealed class TextureRule
{
    public string Mapper { get; set; } = "";
    public string File { get; set; } = "";
    public Criteria When { get; set; } = new Criteria();
}
public sealed class FieldRule
{
    public string Record { get; set; } = "";
    public string Field { get; set; } = "";
    public string Value { get; set; } = "";
}
public sealed class RuleSet
{
    public int Version { get; set; } = 1;
    public Dictionary<string,Criteria> Contexts { get; set; } = new Dictionary<string,Criteria>();
    public List<TextureRule> Textures { get; set; } = new List<TextureRule>();
    public List<FieldRule> Fields { get; set; } = new List<FieldRule>();
    public static RuleSet Parse(string json)
    {
        var r = JsonConvert.DeserializeObject<RuleSet>(json) ?? throw new FormatException("Empty rules");
        if (r.Version != 1 || r.Textures == null || r.Fields == null || r.Contexts == null || r.Contexts.Any(c => string.IsNullOrWhiteSpace(c.Key) || c.Value == null)) throw new FormatException("Invalid rules version/lists");
        if (r.Textures.Any(t => t == null || t.When == null || string.IsNullOrWhiteSpace(t.Mapper) || string.IsNullOrWhiteSpace(t.File))) throw new FormatException("Invalid texture rule");
        if (r.Fields.Any(f => f == null || string.IsNullOrWhiteSpace(f.Record) || (f.Field != "Pronouns" && f.Field != "UnitVoiceGender") || f.Value != (f.Field == "Pronouns" ? "HeHim" : "Male"))) throw new FormatException("Invalid field override");
        return r;
    }
    public TextureRule? Select(string mapper, Criteria actual) => Textures.FirstOrDefault(t => t.Mapper == mapper && t.When.Matches(actual));
    public static string AssetPath(string root, string relative)
    {
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(prefix, relative));
        if (Path.IsPathRooted(relative) || !full.StartsWith(prefix, StringComparison.Ordinal)) throw new FormatException("Asset escapes mod folder");
        return full;
    }
}
public static class TextOverrides
{
    public static string Normalize(string key) => key.StartsWith("%", StringComparison.Ordinal) ? key.Substring(1) : key;
    public static Dictionary<string,string> LoadJson(string json)
    {
        var source = JsonConvert.DeserializeObject<Dictionary<string,string>>(json) ?? throw new FormatException("Empty text map");
        var result = new Dictionary<string,string>(StringComparer.Ordinal);
        foreach (var kv in source) Add(result, kv.Key, kv.Value);
        return result;
    }
    static void Add(Dictionary<string,string> map, string key, string value)
    {
        key = Normalize(key);
        if (key.Length == 0 || value == null || map.ContainsKey(key)) throw new FormatException("Empty or duplicate localization key");
        map.Add(key, value);
    }
    public static bool TryGet(Dictionary<string,string> map, string key, out string value) => map.TryGetValue(Normalize(key), out value!);
    public static Dictionary<string,string> LoadCsv(string csv)
    {
        var rows = Csv(csv).ToList();
        if (rows.Count == 0) throw new FormatException("Missing CSV header");
        int key = rows[0].IndexOf("key"), replacement = rows[0].IndexOf("replacement");
        if (key < 0 || replacement < 0) throw new FormatException("Missing key/replacement column");
        var result = new Dictionary<string,string>(StringComparer.Ordinal);
        foreach (var row in rows.Skip(1))
        {
            if (row.Count <= Math.Max(key, replacement)) throw new FormatException("Short CSV row");
            Add(result, row[key], row[replacement]);
        }
        return result;
    }
    static IEnumerable<List<string>> Csv(string input)
    {
        var row = new List<string>(); var cell = new StringBuilder(); bool quoted = false, closed = false;
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (quoted) { if (c == '"') { if (i+1 < input.Length && input[i+1] == '"') {cell.Append('"'); i++;} else {quoted=false; closed=true;} } else cell.Append(c); }
            else if (c == ',' || c == '\r' || c == '\n')
            {
                row.Add(cell.ToString()); cell.Clear(); closed=false;
                if (c != ',') { if (c == '\r' && i+1 < input.Length && input[i+1] == '\n') i++; yield return row; row=new List<string>(); }
            }
            else if (c == '"' && cell.Length == 0 && !closed) quoted=true;
            else { if (closed || c == '"') throw new FormatException("Invalid CSV quote"); cell.Append(c); }
        }
        if (quoted) throw new FormatException("Unclosed CSV quote");
        if (cell.Length > 0 || row.Count > 0 || closed) {row.Add(cell.ToString());yield return row;}
    }
}
