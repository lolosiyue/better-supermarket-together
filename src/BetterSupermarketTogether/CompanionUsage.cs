using System;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace BetterSupermarketTogether;

internal sealed class CompanionUsage
{
    internal long? Prompt, Completion, Hit, Miss;
    internal double Milliseconds;
    internal bool CacheKnown => Hit.HasValue && Miss.HasValue;
    internal static CompanionUsage Parse(JToken usage,double milliseconds)
    {
        var result=new CompanionUsage { Milliseconds=milliseconds };
        if (usage is not JObject obj) return result;
        result.Prompt=Counter(obj["prompt_tokens"]); result.Completion=Counter(obj["completion_tokens"]);
        var details=obj["prompt_tokens_details"] as JObject;
        long? hit=Counter(obj["prompt_cache_hit_tokens"]) ?? Counter(details?["cached_tokens"]);
        long? miss=Counter(obj["prompt_cache_miss_tokens"]);
        if (!miss.HasValue && hit.HasValue && result.Prompt.HasValue && result.Prompt>=hit) miss=result.Prompt-hit;
        if (hit.HasValue && miss.HasValue && (!result.Prompt.HasValue || hit+miss==result.Prompt)) { result.Hit=hit;result.Miss=miss; }
        return result;
    }
    private static long? Counter(JToken token)
    {
        if(token?.Type!=JTokenType.Integer) return null;
        try { long n=(long)token; return n>=0 && n<=100000000 ? n : (long?)null; }
        catch(OverflowException) { return null; }
    }
    internal string LogText => "Companion API usage: input="+N(Prompt)+" output="+N(Completion)+" cache_hit="+N(Hit)+" cache_miss="+N(Miss)+" elapsed_ms="+Milliseconds.ToString("0",CultureInfo.InvariantCulture);
    internal static string N(long? n) => n?.ToString(CultureInfo.InvariantCulture) ?? "n/a";
}

internal sealed class CompanionUsageStats
{
    private readonly object _lock=new object();
    private CompanionUsage _last;
    private long _hit,_miss;
    private int _responses,_cacheKnown;
    internal void Record(CompanionUsage usage)
    {
        lock(_lock)
        {
            _last=usage;_responses++;
            if(usage.CacheKnown) { _cacheKnown++;_hit+=usage.Hit.Value;_miss+=usage.Miss.Value; }
        }
    }
    internal string Summary(bool english)
    {
        lock(_lock)
        {
            if(_last==null) return ModText.Translate("Cache：尚未收到 API 用量",english);
            string latest=_last.CacheKnown?Rate(_last.Hit.Value,_last.Miss.Value,english):ModText.Translate("未提供",english);
            string total=_cacheKnown>0?Rate(_hit,_miss,english):ModText.Translate("未提供",english);
            string line1=ModText.FormatFor(english,"Cache 本次：{0}{1}",latest,_last.CacheKnown?" · H"+_last.Hit+"/M"+_last.Miss:"");
            string line2=ModText.FormatFor(english,"本房間：{0} · 命中 {1} / 未命中 {2}",total,_hit,_miss);
            string line3=ModText.FormatFor(english,"輸入 {0} / 輸出 {1} · {2} ms",_last.Prompt.HasValue?CompanionUsage.N(_last.Prompt):ModText.Translate("未提供",english),
                _last.Completion.HasValue?CompanionUsage.N(_last.Completion):ModText.Translate("未提供",english),_last.Milliseconds.ToString("0",CultureInfo.InvariantCulture));
            string line4=ModText.FormatFor(english,"回覆 {0} · 未報快取 {1}",_responses,_responses-_cacheKnown);
            return line1+"\n"+line2+"\n"+line3+"\n"+line4;
        }
    }
    private static string Rate(long hit,long miss,bool english) => hit+miss==0?ModText.Translate("—（0 tokens）",english):(100.0*hit/(hit+miss)).ToString("0.0",CultureInfo.InvariantCulture)+"%";
}
