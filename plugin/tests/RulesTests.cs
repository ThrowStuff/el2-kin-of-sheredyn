// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using KinOfSheredyn;
using Xunit;

public sealed class RulesTests
{
    [Fact] public void RulesSelectFirstMatchingMapperAndAllCriteria()
    {
        var r=RuleSet.Parse("""{"version":1,"textures":[{"mapper":"Leader_Portrait","file":"assets/a.png","when":{"faction":"Kin","gender":"Female","character":"Leader"}},{"mapper":"Leader_Portrait","file":"assets/fallback.png"}]}""");
        Assert.Equal("assets/a.png",r.Select("Leader_Portrait",new Criteria{Faction="Kin",Gender="Female",Character="Leader"})!.File);
        Assert.Equal("assets/fallback.png",r.Select("Leader_Portrait",new Criteria{Faction="Other",Gender="Female",Character="Leader"})!.File);
        Assert.Null(r.Select("Other_Portrait",new Criteria()));
    }
    [Theory] [InlineData("Faction")] [InlineData("Character")] [InlineData("Gender")]
    public void EachCriterionIsRequiredWhenSpecified(string field)
    {
        var wanted=new Criteria();typeof(Criteria).GetProperty(field)!.SetValue(wanted,"A");
        Assert.False(wanted.Matches(new Criteria()));
        var actual=new Criteria();typeof(Criteria).GetProperty(field)!.SetValue(actual,"A");Assert.True(wanted.Matches(actual));
        typeof(Criteria).GetProperty(field)!.SetValue(actual,"a");Assert.False(wanted.Matches(actual));
        Assert.True(new Criteria().Matches(actual));
    }
    [Theory] [InlineData("null")] [InlineData("{\"version\":2}")] [InlineData("{\"textures\":[{}]}")] [InlineData("{\"fields\":[{\"record\":\"x\",\"field\":\"Other\",\"value\":\"Male\"}]}")]
    public void RejectInvalidRules(string json) => Assert.Throws<FormatException>(()=>RuleSet.Parse(json));
    [Fact] public void JsonLookupNormalizesOnlyLeadingPercentAndIsCaseSensitive()
    {
        var map=TextOverrides.LoadJson("{\"%LeaderTitle\":\"Bogdan\",\"Empty\":\"\"}");
        Assert.True(TextOverrides.TryGet(map,"%LeaderTitle",out var value));Assert.Equal("Bogdan",value);
        Assert.True(TextOverrides.TryGet(map,"LeaderTitle",out _));Assert.False(TextOverrides.TryGet(map,"leadertitle",out _));
        Assert.False(TextOverrides.TryGet(map,"Missing",out _));Assert.Equal("",map["Empty"]);
        Assert.Throws<FormatException>(()=>TextOverrides.LoadJson("{\"x\":\"1\",\"%x\":\"2\"}"));
    }
    [Fact] public void CsvHandlesQuotesCommasCrLfAndMultilineReplacement()
    {
        var map=TextOverrides.LoadCsv("key,original,replacement,reason\r\n%Title,old,\"New, \"\"quoted\"\"\r\nline\",change\r\n%Other,old,Simple,x\r\n");
        Assert.Equal(2,map.Count);Assert.Equal("New, \"quoted\"\r\nline",map["Title"]);Assert.Equal("Simple",map["Other"]);
    }
    [Theory] [InlineData("key,original\na,b")] [InlineData("key,replacement\nx,\"open")] [InlineData("key,replacement\nx,a\nx,b")] [InlineData("key,replacement\nx,\"a\"oops")]
    public void CsvRejectsMalformedOrDuplicateRows(string csv)=>Assert.Throws<FormatException>(()=>TextOverrides.LoadCsv(csv));
    [Fact] public void AssetPathsStayInsideModFolder()
    {
        var root=Path.Combine(Path.GetTempPath(),"kin");
        Assert.Equal(Path.Combine(root,"assets","portrait.png"),RuleSet.AssetPath(root,"assets/portrait.png"));
        Assert.Throws<FormatException>(()=>RuleSet.AssetPath(root,"../outside.png"));
        Assert.Throws<FormatException>(()=>RuleSet.AssetPath(root,"/absolute.png"));
        Assert.Throws<FormatException>(()=>RuleSet.AssetPath(root,"../kin-other/x.png"));
    }
    [Fact] public void ShippedRulesAndTextHaveExpectedOverrides()
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../../"));
        var rules=RuleSet.Parse(File.ReadAllText(Path.Combine(root,"plugin/rules/rules.json")));
        var map=TextOverrides.LoadJson(File.ReadAllText(Path.Combine(root,"plugin/rules/text.json")));
        Assert.Equal(13,rules.Fields.Count);
        Assert.Contains(rules.Fields,f=>f.Record=="Hero_KinOfSheredyn_Defender_2" && f.Field=="Pronouns" && f.Value=="HeHim");
        Assert.Contains(rules.Fields,f=>f.Record=="PresentationPawn_KinOfSheredyn_Defender_Hero_2" && f.Value=="Male");
        Assert.Equal("General Bogdan Shaparenko",map["FactionLeader_KinOfSheredynTitle"]);
        Assert.Equal("Przemko",map["DialogCharacter_KinOfSheredyn_PryjzaTitle"]);
        Assert.Equal(292,map.Count);
        Assert.DoesNotContain(map.Values,v=>v.Contains("{parameter:"));
        Assert.Contains("{0}",map["FactionQuest_KinOfSheredyn_Chapter02_Step01_Start_Sentence01"]);
        Assert.Equal("Female",rules.Contexts["Leader_KinOfSheredyn"].Gender);
    }
    private static string RepositoryRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../../"));
    private static string NormalizeParameters(string text) => Regex.Replace(text, @"\{parameter:(\d+)\}", "{$1}");

    // GameData tests need an extracted stock localization (not distributable):
    // public builds run `dotnet test --filter Category!=GameData`.
    [Theory]
    [Trait("Category","GameData")]
    [InlineData(@"\{[^{}]*\}")]
    [InlineData(@"<[^<>]*>")]
    [InlineData(@"\[[^\[\]]*\]")]
    [InlineData("\n")]
    public void EveryReplacementPreservesStockTokensInOrder(string pattern)
    {
        var stock=JArray.Parse(File.ReadAllText(Path.Combine(RepositoryRoot,"reports/t05/localization.json")))
            .ToDictionary(r=>((string)r["key"]!).TrimStart('%'),r=>NormalizeParameters((string)r["text"]!));
        var replacements=TextOverrides.LoadJson(File.ReadAllText(Path.Combine(RepositoryRoot,"plugin/rules/text.json")));
        foreach(var pair in replacements)
        {
            Assert.True(stock.ContainsKey(pair.Key), $"Unknown stock key: {pair.Key}");
            var expected=Regex.Matches(stock[pair.Key],pattern).Select(m=>m.Value).ToArray();
            var actual=Regex.Matches(pair.Value,pattern).Select(m=>m.Value).ToArray();
            Assert.True(expected.SequenceEqual(actual), $"Stock token mismatch for {pair.Key}: {pattern}");
        }
    }

    [Fact]
    [Trait("Category","GameData")]
    public void VideoSubtitlesStayStock()
    {
        var replacements=TextOverrides.LoadJson(File.ReadAllText(Path.Combine(RepositoryRoot,"plugin/rules/text.json")));
        foreach(var row in JArray.Parse(File.ReadAllText(Path.Combine(RepositoryRoot,"reports/t05/localization.json"))))
        {
            var key=((string)row["key"]!).TrimStart('%');
            if(key.StartsWith("Video_",StringComparison.Ordinal) && replacements.TryGetValue(key,out var value))
                Assert.Equal(NormalizeParameters((string)row["text"]!),value);
        }
    }
}
