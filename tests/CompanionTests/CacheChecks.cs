using BetterSupermarketTogether;
using Newtonsoft.Json.Linq;

internal static class CacheChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var context=new CompanionContext();
        var empty=Array.Empty<CompanionLine>();
        JObject Snapshot(string fact,int hour) => new JObject { ["memories"]=new JArray(fact),["hour"]=hour };
        bool Prefix(JArray before,JArray after) => before.Count<=after.Count && before.Select((x,i)=>JToken.DeepEquals(x,after[i])).All(x=>x);
        var first=context.Begin("Fixed persona",empty,Snapshot("unique-fact-A",8),"hello-one");
        check(!((string)first[0]["content"]).Contains("unique-fact-A") && ((string)first[1]["content"]).Contains("unique-fact-A"),"dynamic memory stays outside static system prefix");
        context.Answer("answer-one");
        var second=context.Begin("Fixed persona",empty,Snapshot("unique-fact-B",9),"hello-two");
        check(Prefix(first,second),"changed memory and clock preserve the complete previous request prefix");
        check((string)second.Last["content"]=="hello-two" && ((string)second[second.Count-2]["content"]).Contains("unique-fact-B"),"fresh context and current authorization remain at the tail");
        var request=second.DeepClone();
        var calls=new JArray(new JObject { ["id"]="call-A",["type"]="function",["function"]=new JObject { ["name"]="remember",["arguments"]="{\"fact\":\"sample\"}" } });
        second.Add(context.Assistant(new JObject { ["content"]=JValue.CreateNull(),["tool_calls"]=calls }));
        second.Add(context.Tool("call-A","Remembered sample"));
        second.Add(context.FinalOnly());
        check(Prefix((JArray)request,second),"tool followup appends to the original request without rebuilding messages");
        context.Answer("done");
        var third=context.Begin("Fixed persona",empty,Snapshot("sample",10),"hello-three");
        check(Prefix(second,third) && ValidTools(third) && third.Any(x=>(string)x["content"]==CompanionContext.FinalOnlyInstruction),"next turn preserves raw tool calls, results and final-only instruction");
        check(((string)third[third.Count-2]["content"]).Contains("earlier FINAL_RESPONSE_ONLY restrictions have ended"),"fresh user turn explicitly re-enables authorized tool requests after final-only reply");
        first[0]["content"]="mutated external copy";
        check((string)third[0]["content"]!= "mutated external copy","request snapshots do not alias retained prefix state");
        context.Assistant(new JObject { ["tool_calls"]=new JArray(
            new JObject { ["id"]="pending-A",["type"]="function",["function"]=new JObject { ["name"]="remember",["arguments"]="{\"fact\":\"A\"}" } },
            new JObject { ["id"]="pending-B",["type"]="function",["function"]=new JObject { ["name"]="remember",["arguments"]="{\"fact\":\"B\"}" } }) });
        context.Tool("pending-A","Executed A");context.Stop("Cancelled");
        var cancelled=context.Begin("Fixed persona",empty,Snapshot("sample",11),"after cancel");
        check(ValidTools(cancelled) && cancelled.Any(x=>(string)x["tool_call_id"]=="pending-B" && ((string)x["content"]).StartsWith("Not executed")),"cancellation closes unmatched tool messages without inventing execution");
        context.Answer("ok");
        bool bounded=true,valid=true;
        for(int i=0;i<30;i++) {
            var m=context.Begin("Fixed persona",empty,Snapshot("sample",i),new string('u',1000));
            bounded &= m.ToString(Newtonsoft.Json.Formatting.None).Length<40000;
            valid &= ValidTools(m);context.Answer(new string('a',3000));
        }
        check(context.Trims>0 && bounded && context.RetainedTurns<=20,"long sessions rotate bounded batches of complete turns");
        check(valid,"batch rotation never leaves orphan tool messages");
        var changed=context.Begin("New persona",empty,Snapshot("new",1),"new-persona");
        check(changed.Count==3 && ((string)changed[0]["content"]).StartsWith("New persona"),"persona changes reset the wire conversation");
        context.Reset();
        var seeded=context.Begin("Fixed persona",new[]{new CompanionLine { Role="user",Text="old user" },new CompanionLine { Role="system",Text="old action result" }},Snapshot("new",1),"new user");
        check(seeded.Count==5 && (string)seeded[2]["role"]=="assistant","legacy persisted display history seeds a new session safely");
        context.Reset();
        var fullLegacy=Enumerable.Range(0,40).Select(i=>new CompanionLine { Role=i%2==0?"user":"assistant",Text="legacy "+i }).ToArray();
        var fullSeed=context.Begin("Fixed persona",fullLegacy,Snapshot("new",1),"new user");
        check(fullSeed.Count==43,"normal-size legacy history keeps all forty persisted messages");

        var a=CompanionUsage.Parse(JObject.Parse("{prompt_tokens:1000,completion_tokens:20,prompt_cache_hit_tokens:800,prompt_cache_miss_tokens:200}"),25);
        check(a.CacheKnown && a.Hit==800 && a.Miss==200 && a.Completion==20,"DeepSeek cache counters parsed");
        var b=CompanionUsage.Parse(JObject.Parse("{prompt_tokens:200,completion_tokens:10,prompt_tokens_details:{cached_tokens:50}}"),30);
        check(b.CacheKnown && b.Hit==50 && b.Miss==150,"compatible cached_tokens field derives uncached input");
        check(!CompanionUsage.Parse(null,0).CacheKnown,"missing usage is unknown rather than zero percent");
        check(!CompanionUsage.Parse(JObject.Parse("{prompt_tokens:100,prompt_tokens_details:'bad'}"),0).CacheKnown,"malformed usage details do not break chat");
        check(!CompanionUsage.Parse(JObject.Parse("{prompt_tokens:100,prompt_cache_hit_tokens:-1,prompt_cache_miss_tokens:101}"),0).CacheKnown,"negative cache counters rejected");
        check(!CompanionUsage.Parse(JObject.Parse("{prompt_tokens:100,prompt_cache_hit_tokens:80,prompt_cache_miss_tokens:80}"),0).CacheKnown,"inconsistent cache totals rejected");
        check(!CompanionUsage.Parse(JObject.Parse("{prompt_cache_hit_tokens:"+new string('9',100)+",prompt_cache_miss_tokens:0}"),0).CacheKnown,"oversized usage integers do not crash telemetry");
        var stats=new CompanionUsageStats();stats.Record(a);stats.Record(b);stats.Record(CompanionUsage.Parse(null,40));
        check(stats.Summary(true).Contains("70.8%") && stats.Summary(true).Contains("unreported 1"),"room hit rate is token-weighted and excludes unreported samples");
        check(stats.Summary(false).Contains("未提供") && stats.Summary(false).Contains("未報快取 1"),"last unknown sample remains explicit in Chinese statistics");
        var zero=new CompanionUsageStats();zero.Record(CompanionUsage.Parse(JObject.Parse("{prompt_tokens:0,prompt_cache_hit_tokens:0,prompt_cache_miss_tokens:0}"),0));
        check(!zero.Summary(true).Contains("NaN") && zero.Summary(true).Contains("0 tokens"),"zero-input replies do not divide by zero");
        check(a.LogText.Contains("cache_hit=800") && a.LogText.Contains("elapsed_ms=25"),"usage log contains numeric counters and latency");
    }
    private static bool ValidTools(JArray messages)
    {
        var pending=new HashSet<string>();
        foreach(JObject m in messages) {
            string role=(string)m["role"];
            if(role=="tool") { if(!pending.Remove((string)m["tool_call_id"]))return false; }
            else {
                if(pending.Count!=0)return false;
                if(m["tool_calls"] is JArray calls)foreach(var call in calls)if(!pending.Add((string)call["id"]))return false;
            }
        }
        return pending.Count==0;
    }
}
