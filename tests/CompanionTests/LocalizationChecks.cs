using BetterSupermarketTogether;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

internal static class LocalizationChecks
{
    private static string ThisFile([CallerFilePath] string file="") => file;
    internal static void Run(Action<bool,string> check)
    {
        bool original=ModText.English;
        try
        {
            var literal=new Regex("\\\"((?:[^\\\"\\\\]|\\\\.)*)\\\"");
            var keyPattern=new Regex("\\[\\\"((?:[^\\\"\\\\]|\\\\.)*)\\\"\\]");
            string source=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ThisFile()),"..","..","src","BetterSupermarketTogether"));
            var extracted=keyPattern.Matches(File.ReadAllText(Path.Combine(source,"ModText.Catalog.cs"))).Select(x=>Regex.Unescape(x.Groups[1].Value)).ToArray();
            check(extracted.Length==extracted.Distinct().Count() && extracted.Length==ModText.Catalog.Count,"central catalog has no duplicate or lost translation entries");
            var missing=new List<string>();
            foreach(string file in new[]{"DeepSeekCompanion.cs","CompanionPlanning.cs","CompanionWorkWorld.cs","CompanionPanelUI.cs","CompanionUsage.cs"})
                foreach(string line in File.ReadLines(Path.Combine(source,file)))
                {
                    if(line.Contains("ModText.Description(") || line.TrimStart().StartsWith("//"))continue;
                    foreach(Match match in literal.Matches(line))
                    {
                        string value=Regex.Unescape(match.Groups[1].Value);
                        if(Regex.IsMatch(value,@"[\u3400-\u9fff]") && !ModText.Catalog.ContainsKey(value))missing.Add(file+": "+value);
                    }
                }
            check(missing.Count==0,"all fixed companion UI/status/error keys are in the central catalog: "+string.Join("; ",missing));

            var placeholders=new Regex(@"\{(\d+)(?:[^{}]*)\}");var invalid=new List<string>();
            foreach(var entry in ModText.Catalog)
            {
                var a=placeholders.Matches(entry.Key).Select(x=>int.Parse(x.Groups[1].Value)).Distinct().Order().ToArray();
                var b=placeholders.Matches(entry.Value).Select(x=>int.Parse(x.Groups[1].Value)).Distinct().Order().ToArray();
                if(!a.SequenceEqual(b))invalid.Add(entry.Key);
                var args=Enumerable.Repeat((object)1234,a.Length==0?0:a.Max()+1).ToArray();
                try { ModText.FormatFor(false,entry.Key,args);ModText.FormatFor(true,entry.Key,args); }
                catch(FormatException) { invalid.Add(entry.Key); }
            }
            check(invalid.Count==0,"all catalog translations preserve usable format placeholders: "+string.Join("; ",invalid));
            try { CompanionProtocol.Endpoint("invalid"); }
            catch(ArgumentException error) { check(ModText.Catalog.ContainsKey(error.Message) && !Regex.IsMatch(new ModText.Message(error.Message).Render(true),@"[\u3400-\u9fff]"),"API configuration validation uses the same translation catalog"); }

            const string raw="Storage Repairs restock 待命 {0} / 牛奶";
            var selected=new ModText.Message("已選取貨架 {0} · {1}","S4",raw);
            check(selected.Render(false)=="已選取貨架 S4 · "+raw && selected.Render(true)=="Selected shelf S4 · "+raw,"product names, identifiers and literal braces remain data during translation");
            var fact=new ModText.Message("已記住：{0}",raw);
            check(fact.Render(true)=="Remembered: "+raw && fact.Render(false)=="已記住："+raw,"player facts are not translated as status keywords");
            var target=new CompanionStep{Kind="restock",Product=17,Shelf="S4"};
            check(target.Label.Render(false)=="補貨：商品 #17，貨架 S4" && target.Label.Render(true)=="Restock: product #17, shelf S4","product and shelf plan labels render from structured arguments");
            var follow=new CompanionStep{Kind="follow",Seconds=30};
            check(follow.Label.Render(false)=="跟隨房主 30 秒" && follow.Label.Render(true)=="Follow host for 30 seconds","timed follow uses a shared translated template");
            check(new CompanionStep{Kind="follow"}.Label.Render(true)=="Follow host continuously","continuous follow has a distinct localized label");
            bool roleLabels=true;
            foreach(string role in CompanionProtocol.Roles)
                foreach(string completion in new[]{"continuous","one_cycle","duration"})
                {
                    var step=new CompanionStep{Kind="work",Role=role,Completion=completion,Seconds=60};
                    roleLabels &= !Regex.IsMatch(step.Label.Render(true),@"[\u3400-\u9fff]") && Regex.IsMatch(step.Label.Render(false),@"[\u3400-\u9fff]");
                }
            check(roleLabels,"all native job/completion labels have Chinese and English presentations");

            var world=new LocalWorld();var runner=new CompanionPlanRunner();
            runner.Replace(new List<CompanionStep>{target,new CompanionStep{Kind="work",Role="checkout",Completion="continuous"}},false);
            ModText.English=false;runner.Tick(0,world);world.Progress=CompanionProgress.Complete;runner.Tick(1,world);
            int revision=runner.EventVersion;
            check(runner.Phase==CompanionPlanPhase.Completed && runner.Status.ToString().StartsWith("已完成："),"plan completion has an explicit state and Chinese rendering");
            ModText.English=true;
            check(runner.Phase==CompanionPlanPhase.Completed && runner.Status.ToString().StartsWith("Completed:") && runner.EventVersion==revision,"switching language does not create a new completion event");
            world.Progress=CompanionProgress.Running;runner.Tick(2,world);
            check(world.Starts==2 && runner.Active.Role=="checkout" && runner.Phase==CompanionPlanPhase.Running,"task B starts correctly after language changes between completion and handoff");
            check(runner.Summary(false).Contains("收銀（持續）") && !runner.Summary(false).Contains("Current:") && runner.Summary(true).Contains("Checkout (continuous)"),"entire work summary uses one selected language without substring replacement");
            world=new LocalWorld{Error="指定目標沒有可用倉庫庫存。"};runner=new CompanionPlanRunner();runner.Replace(new List<CompanionStep>{target},false);runner.Tick(0,world);
            check(runner.Phase==CompanionPlanPhase.Failed && runner.Status.Render(true).Contains("No warehouse stock") && runner.Status.Render(false).Contains("沒有可用倉庫庫存"),"nested failure reasons translate without changing task state");
            int failedVersion=runner.EventVersion;runner.Replace(new List<CompanionStep>{target},false);runner.Tick(1,world);
            check(runner.EventVersion>failedVersion,"identical failures in later plans have separate event revisions");

            var memory=new CompanionMemory();memory.Add("user",raw);memory.AddReply(raw,false);memory.AddReply(raw,true);
            var failed=new ModText.Message("未能完成：{0}",new ModText.Message("貨架不存在或已移除：{0}","S4"));
            memory.AddNotice(failed);
            string wire=memory.History.Last().Text;
            check(memory.History.Last().Display(false)=="未能完成：貨架不存在或已移除：S4" && memory.History.Last().Display(true)=="Failed: Unknown or removed shelf: S4","new execution records switch language using saved keys and arguments");
            check(memory.History[0].Display(true)==raw && memory.History[1].Display(false)==raw,"player and model dialogue remain verbatim");
            check(memory.History[2].Display(false)=="［自主評估］"+raw && memory.History[2].Display(true)=="[Autonomous decision] "+raw && memory.History[2].Text==raw,"automatic-reply marker localizes independently of generated dialogue");
            var before=new CompanionContext().Begin("persona",memory.History,new JObject(),"next");
            ModText.English=false;_ = memory.History.Last().Display(false);ModText.English=true;
            var after=new CompanionContext().Begin("persona",memory.History,new JObject(),"next");
            check(memory.History.Last().Text==wire && JToken.DeepEquals(before,after),"display-language changes do not rewrite canonical API history or reusable prefixes");
            var tool=new CompanionToolResult(failed);ModText.English=false;
            check(tool.Wire==wire && tool.Notice.ToString().Contains("未能完成："),"tool wire result and localized display notice are separate");

            string directory=Path.Combine(Path.GetTempPath(),"bst-localization-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try
            {
                string path=Path.Combine(directory,"memory.json");memory.Save(path);var loaded=CompanionMemory.Load(path);
                check(loaded.History.Last().Display(true)==failed.Render(true) && loaded.History.Last().Display(false)==failed.Render(false) && loaded.History[2].Automatic,"localized notices and automatic markers survive UTF-8 memory reload");
                var preview=new ModText.Message("計畫已排入，尚未完成：{0}",new ModText.Message("{0}（另有 {1} 步）",target.Label,5));
                loaded.AddNotice(preview);loaded.Save(path);var again=CompanionMemory.Load(path);
                check(again.History.Last().Display(false)==preview.Render(false) && again.History.Last().Display(true)==preview.Render(true),"nested plan preview fits the existing bounded memory format");
                loaded=new CompanionMemory();loaded.Add("system","Failed: legacy untranslated text");loaded.Save(path);again=CompanionMemory.Load(path);
                check(again.History.Single().Display(false)=="Failed: legacy untranslated text" && again.History.Single().Display(true)=="Failed: legacy untranslated text","legacy raw execution records retain their original text");
                for(int i=0;i<45;i++)again.AddNotice(failed);
                check(again.History.Count==40,"structured execution records retain the forty-line history limit");
            }
            finally { Directory.Delete(directory,true); }
            foreach(var bad in new JToken[]{
                new JObject{["key"]="unknown",["args"]=new JArray()},
                new JObject{["key"]="已記住：{0}",["args"]=new JArray()},
                new JObject{["key"]="已記住：{0}",["args"]=new JArray(new string('x',2001))},
                new JObject{["key"]="待命",["args"]=new JArray(),["extra"]=true},
                new JObject{["key"]="待命",["args"]=new JArray(Enumerable.Repeat("x",17))}})
            {
                check(!ModText.Message.TryNotice(bad,out _) && new CompanionLine{Role="system",Text="raw fallback",Notice=(JObject)bad}.Display(false)=="raw fallback","invalid saved notice falls back without breaking the transcript");
            }
            JToken deep=new ModText.Message("待命").ToNotice();for(int i=0;i<10;i++)deep=new JObject{["key"]="已完成：{0}",["args"]=new JArray(deep)};
            check(!ModText.Message.TryNotice(deep,out _),"saved notice nesting is bounded");
        }
        finally { ModText.English=original; }
    }

    private sealed class LocalWorld : ICompanionWorkWorld
    {
        public bool Safe=>true;
        internal int Starts;
        internal ModText.Message Error;
        internal CompanionProgress Progress=CompanionProgress.Running;
        public ModText.Message Start(CompanionStep step){Starts++;return Error;}
        public CompanionProgress Poll(CompanionStep step,double elapsed,out ModText.Message detail){detail=null;return Progress;}
        public void PrepareHandoff(){}
        public void Release(){}
        public void Rest(){}
    }
}
