using BetterSupermarketTogether;
using Newtonsoft.Json.Linq;
internal static class AssistantChecks
{
    internal static void Run(Action<bool,string> check)
    {
        JObject Call(string action,string query="selected")=>new JObject{["id"]="assistant_1",["type"]="function",["function"]=new JObject{["name"]="assistant_task",["arguments"]=new JObject{["action"]=action,["query"]=query}.ToString()}};
        foreach(string action in new[]{"follow","return","restock","delegate","guide","inspect"})
            check(CompanionProtocol.ValidateCall(Call(action),out _,out _),"assistant schema accepts "+action);
        check(!CompanionProtocol.ValidateCall(Call("purchase"),out _,out _),"assistant cannot invent unsupported purchase action");
        check(!CompanionProtocol.ValidateCall(Call("guide",new string('x',81)),out _,out _),"assistant query bounded");
        check(!CompanionToolPolicy.Allowed("assistant_task",true),"autonomous calls cannot enter assistant or delegate");
        var budget=new CompanionToolBudget();
        check(budget.Accept(new JArray(Call("follow")),false),"explicit player can enter assistant mode");
        var second=Call("return");second["id"]="assistant_2";
        check(!budget.Accept(new JArray(second),false),"second mode mutation in one response rejected");
        check(!CompanionStep.TryParse(JObject.Parse("{steps:[{kind:'guide',shelf_id:'S1'}]}"),false,out _),"guide only enters through validated assistant location resolver");
        var label=new CompanionStep{Kind="guide",Shelf="S1"}.Label;
        check(label.WireText.Contains("S1"),"guide label includes resolved shelf identity");
        var readBudget=new CompanionToolBudget();
        var read=Call("inspect");var dispatch=Call("delegate");dispatch["id"]="assistant_dispatch";
        check(readBudget.Accept(new JArray(read),false) && readBudget.Accept(new JArray(dispatch),false),"local diagnosis does not consume dispatch budget");
        var names=CompanionProtocol.Tools().Select(x=>(string)x["function"]["name"]).ToArray();
        check(names.Count(n=>n=="assistant_task")==1,"assistant tool published exactly once");
    }
}
