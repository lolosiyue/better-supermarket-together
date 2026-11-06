using BetterSupermarketTogether;

internal static class ChatChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var aliases=CompanionChatPolicy.DefaultAliases.Concat(new[]{"小美",null}).ToArray();
        var players=new[]{"Bob","阿明"};
        bool Parse(string text,out string prompt)=>CompanionChatPolicy.TryParse(text,aliases,players,out prompt);
        check(Parse("@幫我去補貨",out var p) && p=="幫我去補貨","bare @ message goes to the companion");
        check(Parse("＠ 幫我去補貨",out p) && p=="幫我去補貨","full-width IME @ is accepted");
        check(Parse("@DS 改去收銀",out p) && p=="改去收銀","leading alias is stripped");
        check(Parse("@ds：你好",out p) && p=="你好","alias matching ignores case and full-width colon");
        check(Parse("@DeepSeek娘，記住我叫阿明",out p) && p=="記住我叫阿明","longest Chinese alias stripped with full-width comma");
        check(Parse("@小美 去倉庫",out p) && p=="去倉庫","bound employee name works as alias");
        check(Parse("@DSL 是什麼",out p) && p=="DSL 是什麼","alias must end at a word boundary");
        check(!Parse("@Bob 你好",out _),"mentioning another player is not sent to the companion");
        check(!Parse("@阿明 過來",out _),"mentioning a Chinese player name is ignored");
        check(!Parse("你好 @DS",out _) && !Parse("@",out _) && !Parse("@DS",out _) && !Parse(null,out _),"messages without a leading @ or prompt are ignored");
        check(CompanionChatPolicy.Plain("<color=red>x</color>")=="＜color=red＞x＜/color＞","model text cannot inject chat rich text");
        var chunks=CompanionChatPolicy.Chunks("第一行\r\n\n"+new string('字',450));
        check(chunks.Count==4 && chunks[0]=="第一行" && chunks.Skip(1).All(x=>x.Length<=CompanionChatPolicy.ChunkLength),"replies split by line and length");
        chunks=CompanionChatPolicy.Chunks(string.Join("\n",Enumerable.Range(1,20).Select(x=>"line "+x)));
        check(chunks.Count==CompanionChatPolicy.MaxChunks && chunks.Last().EndsWith("…") && chunks.All(x=>!x.Contains('\n')),"overlong replies are capped with an ellipsis");
        check(CompanionChatPolicy.Chunks("  \n ").Count==0,"blank replies produce no chat lines");
        check(CompanionChatPolicy.IsSkip("SKIP") && CompanionChatPolicy.IsSkip(" skip。") && CompanionChatPolicy.IsSkip("") && !CompanionChatPolicy.IsSkip("不要 skip 我啦"),"SKIP remarks are recognised without swallowing real lines");
        check(CompanionChatPolicy.TypingDelay("好",false)==.6f && CompanionChatPolicy.TypingDelay(new string('字',200),false)==3.5f && CompanionChatPolicy.TypingDelay("補好了",true)<CompanionChatPolicy.TypingDelay("補好了",false),"typing delay is bounded and shorter for the first line");
        check(CompanionContext.Prefix("persona").StartsWith("persona"+CompanionContext.Voice) && CompanionContext.Voice.Contains("NO_PLAYER_MESSAGE"),"voice guidance follows the persona in the stable prefix");

        var mood=new CompanionMood();
        mood.Observe(0,false,1);
        for(int t=1;t<=600;t++)mood.Observe(t,true,1);
        check(Math.Abs(mood.Energy-56)<.01 && mood.Snapshot(600)["working_without_break_minutes"]!.ToObject<int>()==9,"continuous work drains energy and tracks the streak");
        for(int t=601;t<=900;t++)mood.Observe(t,false,1);
        check(Math.Abs(mood.Energy-80)<.01 && mood.Snapshot(900)["working_without_break_minutes"]!.ToObject<int>()==0,"rest recovers energy and ends the streak");
        mood.Observe(901,false,2);
        check(mood.Energy>=85,"a new game day restores energy");
        check(!mood.Lonely(901) && mood.Lonely(CompanionMood.LonelyMinutes*60),"loneliness only after a long silence since observation began");
        double before=mood.Mood;mood.Chatted(2000);double once=mood.Mood;mood.Chatted(2010);
        check(once>before && mood.Mood==once && !mood.Lonely(2010),"talking cheers her up once per two minutes and ends loneliness");
        for(int i=0;i<7;i++)mood.Event(2100+i,"event "+i,1);
        var recent=(Newtonsoft.Json.Linq.JArray)mood.Snapshot(2110)["recent_events"]!;
        check(recent.Count==5 && (string?)recent[0]["event"]=="event 2","recent events keep the latest five");
        mood.Observe(2111,false,2);mood.Observe(2112+CompanionMood.EventMinutes*60,false,2);
        check(((Newtonsoft.Json.Linq.JArray)mood.Snapshot(2112+CompanionMood.EventMinutes*60)["recent_events"]!).Count==0,"old events fade from inner state");
        mood.Event(3000,"bad",-200,-200);
        check(mood.Mood==-100 && mood.Energy==0 && mood.EnergyWord=="exhausted","mood and energy stay bounded");
        mood.Reset();
        check(mood.Energy==80 && mood.Snapshot(0)["minutes_since_player_talked"]!.Type==Newtonsoft.Json.Linq.JTokenType.Null,"reset clears inner state");

        var gate=new CompanionRemarkGate();
        check(!gate.Ready(0,0),"zero remarks per hour disables remarks");
        check(gate.Ready(0,2),"first remark allowed");gate.Used(0);
        check(!gate.Ready(100,2) && gate.Ready(CompanionRemarkGate.MinimumGap,2),"minimum gap between remarks");gate.Used(200);
        check(!gate.Ready(1000,2) && gate.Ready(3600,2),"hourly remark budget slides");
    }
}
