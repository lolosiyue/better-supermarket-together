using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace BetterSupermarketTogether;

// A bounded, explicit vocabulary. Never infer controls from arbitrary prose or history.
internal sealed class CompanionBehaviorPreferences
{
    internal bool Help=true,Rice=true,Stretch=true,Look=true,Skirt=true,Nod=true,Relax=true,RestCorner=true,Gaze=true,Company=true,Greet=true,Stare=true,Yawn=true;
    internal string Frequency="一般";
    internal int[] Gestures => Frequency=="關"?new int[0]:Enumerable.Range(0,3).Where(i=>i==0?Stretch:i==1?Look:Skirt).ToArray();
    internal double IntervalScale => Frequency=="較少"?3:1;
    internal string Signature => Help+"|"+Rice+"|"+Stretch+"|"+Look+"|"+Skirt+"|"+Frequency+"|"+Nod+"|"+Relax+"|"+RestCorner+"|"+Gaze+"|"+Company+"|"+Greet+"|"+Stare+"|"+Yawn;
    // Social reactions are small gestures too, so the gesture master switch silences them.
    internal bool GreetOn => Greet && Frequency!="關";
    internal bool StareOn => Stare && Frequency!="關";
    internal bool YawnOn => Yawn && Frequency!="關";
    private static readonly string[] Keys={"主動幫忙","小動作頻率","伸懶腰","左右看","整理裙擺","偷吃白飯","路過點頭","忙完放鬆","固定休息位置","看著我","下班陪我","迎接我","被注視反應","打哈欠"};
    private static readonly Dictionary<string,string> Aliases=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
    {
        {"不要偷吃","偷吃白飯：關"},{"不要偷吃白飯","偷吃白飯：關"},{"可以偷吃白飯","偷吃白飯：開"},
        {"少做小動作","小動作頻率：較少"},{"不要做小動作","小動作頻率：關"},{"恢復一般小動作頻率","小動作頻率：一般"},
        {"不要主動幫忙","主動幫忙：關"},{"主動幫忙補貨","主動幫忙：開"},{"優先補貨","主動幫忙：開"},
        {"不要伸懶腰","伸懶腰：關"},{"不要左右看","左右看：關"},{"不要整理裙擺","整理裙擺：關"},
        {"不要路過點頭","路過點頭：關"},{"不要忙完放鬆","忙完放鬆：關"},{"不要固定休息位置","固定休息位置：關"},
        {"no snacking","偷吃白飯：關"},{"fewer idle gestures","小動作頻率：較少"},{"no idle gestures","小動作頻率：關"},
        {"no proactive help","主動幫忙：關"},{"help restock when idle","主動幫忙：開"},
        {"不要一直看著我","看著我：關"},{"不要盯著我","看著我：關"},{"不要跟著我","下班陪我：關"},{"下班陪我","下班陪我：開"},
        {"不要迎接我","迎接我：關"},{"不要害羞","被注視反應：關"},{"不要打哈欠","打哈欠：關"},
        {"stop staring at me","看著我：關"},{"don't follow me after work","下班陪我：關"}
    };
    internal static bool Parse(string fact,out string key,out string value)
    {
        key=value=null;
        string text=(fact??"").Trim().TrimEnd('。','.','！','!');
        if(Aliases.TryGetValue(text,out string alias))text="行為偏好｜"+alias;
        const string prefix="行為偏好｜";
        if(!text.StartsWith(prefix,StringComparison.Ordinal))return false;
        var pieces=text.Substring(prefix.Length).Split('：');
        if(pieces.Length!=2 || !Keys.Contains(pieces[0]))return false;
        if(pieces[0]=="小動作頻率" ? pieces[1]!="關" && pieces[1]!="較少" && pieces[1]!="一般" : pieces[1]!="開" && pieces[1]!="關")return false;
        key=pieces[0];value=pieces[1];return true;
    }
    internal static string Canonical(string key,string value)=>"行為偏好｜"+key+"："+value;
    internal static CompanionBehaviorPreferences Resolve(IEnumerable<string> facts,bool help=true,bool gestures=true,bool rice=true)
    {
        var p=new CompanionBehaviorPreferences();
        foreach(string fact in facts??Enumerable.Empty<string>())
        {
            if(!Parse(fact,out string key,out string value))continue;
            bool on=value=="開";
            switch(key){case "主動幫忙":p.Help=on;break;case "偷吃白飯":p.Rice=on;break;
                case "伸懶腰":p.Stretch=on;break;case "左右看":p.Look=on;break;case "整理裙擺":p.Skirt=on;break;case "小動作頻率":p.Frequency=value;break;case "路過點頭":p.Nod=on;break;case "忙完放鬆":p.Relax=on;break;case "固定休息位置":p.RestCorner=on;break;
                case "看著我":p.Gaze=on;break;case "下班陪我":p.Company=on;break;case "迎接我":p.Greet=on;break;case "被注視反應":p.Stare=on;break;case "打哈欠":p.Yawn=on;break;}
        }
        // Explicit config disable is a master switch; a memory cannot override it.
        p.Help &= help;p.Rice &= rice;if(!gestures)p.Frequency="關";
        return p;
    }
    internal static bool Store(List<string> facts,string fact,out string saved)
    {
        saved=fact.Trim();
        if(Parse(saved,out string key,out string value))
        {
            saved=Canonical(key,value);
            int matching=facts.Count(x=>Parse(x,out string k,out _) && k==key);
            if(facts.Count-matching>=24)return false;
            facts.RemoveAll(x=>Parse(x,out string k,out _) && k==key);
        }
        else if(facts.Contains(saved))return true;
        else if(facts.Count>=24)return false;
        facts.Add(saved);return true;
    }
    internal static void Forget(List<string> facts,int index)
    {
        if(Parse(facts[index],out string key,out _))facts.RemoveAll(x=>Parse(x,out string k,out _) && k==key);
        else facts.RemoveAt(index);
    }
    internal JObject Snapshot()=>new JObject{["nod"]=Nod && Frequency!="關",["relax"]=Relax && Frequency!="關",["fixed_rest_spot"]=RestCorner,["look_at_player"]=Gaze,["keep_company_after_hours"]=Company,["greet_on_return"]=GreetOn,["stare_reaction"]=StareOn,["yawn_when_tired"]=YawnOn,["proactive_help"]=Help,["rice_easter_egg"]=Rice,["gesture_interval_scale"]=IntervalScale,
        ["gestures"]=new JArray(Gestures.Select(i=>i==0?"stretch":i==1?"look_around":"smooth_skirt"))};
    internal string Summary(bool english)=>english
        ? "Effective behavior: help="+Help+", rice="+Rice+", gestures="+string.Join(",",Gestures.Select(i=>i==0?"stretch":i==1?"look":"skirt"))+", interval x"+IntervalScale+", nod="+(Nod && Frequency!="關")+", relax="+(Relax && Frequency!="關")+", rest spot="+RestCorner+", gaze="+Gaze+", company="+Company+", greet="+GreetOn+", stare="+StareOn+", yawn="+YawnOn
        : "生效行為：幫忙"+(Help?"開":"關")+"／白飯"+(Rice?"開":"關")+"／小動作"+Frequency+"（"+string.Join("、",Gestures.Select(i=>i==0?"伸懶腰":i==1?"左右看":"整理裙擺"))+"）／點頭"+(Nod && Frequency!="關"?"開":"關")+"／放鬆"+(Relax && Frequency!="關"?"開":"關")+"／固定休息"+(RestCorner?"開":"關")+"／看著你"+(Gaze?"開":"關")+"／下班陪伴"+(Company?"開":"關")+"／迎接"+(GreetOn?"開":"關")+"／被注視反應"+(StareOn?"開":"關")+"／打哈欠"+(YawnOn?"開":"關");
}
