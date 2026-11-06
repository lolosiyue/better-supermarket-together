using BetterSupermarketTogether;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;

try
{
int checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
void Reject(Action action, string name) { try { action(); } catch { checks++; Console.WriteLine("PASS " + name); return; } throw new Exception(name); }
async Task RejectAsync(Func<Task> action, string name) { try { await action(); } catch { checks++; Console.WriteLine("PASS " + name); return; } throw new Exception(name); }
JObject Call(string name, string args) => new JObject { ["id"]="call_1",["type"]="function",["function"]=new JObject { ["name"]=name,["arguments"]=args } };
CacheChecks.Run(Check);
PlanningChecks.Run(Check);
LocalizationChecks.Run(Check);
MotionChecks.Run(Check);
StaffCommandChecks.Run(Check);
RiceChecks.Run(Check);
LifeChecks.Run(Check);
BehaviorPreferenceChecks.Run(Check);
SocialChecks.Run(Check);
AssistantChecks.Run(Check);
ChatChecks.Run(Check);
PresenceChecks.Run(Check);
var rigs=JArray.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"employee-bone-names.json")));
Check(rigs.Count==7,"all seven native employee models covered");
foreach(var rig in rigs) {
    var names=((JArray)rig["bones"]).Values<string>().ToArray();
    Check((bool)rig["generic"] && CompanionRigPolicy.Required.All(role=>CompanionRigPolicy.Find(role,names,false)!=null),"generic native rig maps required body bones: "+(string)rig["model"]);
    Check(CompanionRigPolicy.Find("chest",names,false)=="Bip01 Spine2","native upper torso selected when VRM has no upperChest: "+(string)rig["model"]);
}
Check(CompanionRigPolicy.Find("leftHand",new[]{"LeftHandTarget","Bip01 L Finger0","Bip01 L Hand"},false)=="Bip01 L Hand","IK target is never mistaken for the hand bone");
Check(CompanionRigPolicy.Find("leftUpperArm",new[]{"mixamorig:LeftArm"},false)=="mixamorig:LeftArm","namespaced bone alias supported");
Check(CompanionRigPolicy.Find("leftHand",new[]{"LeftHandTarget"},false)==null,"unsupported rig stays explicit rather than guessing");
var retry=new CompanionAvatarRetry();
Check(retry.Due(0),"new binding starts a load attempt"); retry.Failed(0);
Check(!retry.Due(.5f) && retry.Due(1),"missing model retries without rebinding employee"); retry.Failed(1);
Check(!retry.Due(2) && retry.Due(3),"repeated not-ready attempts back off"); retry.Succeeded();
Check(!retry.Due(100),"loaded model is not reloaded every tick"); retry.Reset();
Check(retry.Due(100),"scene or source replacement restarts loading"); retry.Failed(100,30);
Check(!retry.Due(110) && retry.Due(130),"heavy load failures use a longer retry interval");
var sourceBind=System.Numerics.Quaternion.CreateFromYawPitchRoll(.4f,.2f,-.1f);
var targetBind=System.Numerics.Quaternion.CreateFromYawPitchRoll(-.3f,.5f,.2f);
var alignment=System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY,MathF.PI);
var unchanged=CompanionRigPolicy.Rotation(sourceBind,sourceBind,alignment,targetBind);
Check(MathF.Abs(System.Numerics.Quaternion.Dot(unchanged,targetBind))>.99999f,"bind pose remains stable across different bone axes");
var delta=System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitX,.8f);
var animated=CompanionRigPolicy.Rotation(delta*sourceBind,sourceBind,alignment,targetBind);
var reconstructed=alignment*animated*System.Numerics.Quaternion.Inverse(targetBind)*System.Numerics.Quaternion.Inverse(alignment);
Check(MathF.Abs(System.Numerics.Quaternion.Dot(reconstructed,delta))>.99999f,"posed source motion transfers across opposite rig frames");
var savedEmployees=Enumerable.Range(0,28).Select(i=>"employee"+i).ToArray();
var loadingEmployeeManager=new NPC_Manager { hiredEmployeesData=new string[18],priorityArray=new int[18],employeesArray=new UnityEngine.GameObject[18] };
Mirror.NetworkServer.active=false; Mirror.NetworkClient.active=false;
EmployeeSaveCapacityPatch.Prefix(new SaveBehaviour { NPCManager=new UnityEngine.GameObject(loadingEmployeeManager) },savedEmployees);
for(int i=0;i<savedEmployees.Length;i++) loadingEmployeeManager.hiredEmployeesData[i]=savedEmployees[i];
Check(loadingEmployeeManager.hiredEmployeesData[27]=="employee27" && loadingEmployeeManager.priorityArray.Length==28,"local 28-person save loads before host network starts");
Mirror.NetworkClient.active=true;
Mirror.NetworkClient.isConnected=false;
var startingHostManager=new NPC_Manager { hiredEmployeesData=new string[18],priorityArray=new int[18],employeesArray=new UnityEngine.GameObject[18] };
EmployeeSaveCapacityPatch.Prefix(new SaveBehaviour { NPCManager=new UnityEngine.GameObject(startingHostManager) },savedEmployees);
for(int i=0;i<savedEmployees.Length;i++) startingHostManager.hiredEmployeesData[i]=savedEmployees[i];
Check(startingHostManager.hiredEmployeesData.SequenceEqual(savedEmployees) && startingHostManager.employeesArray.Length==28,"active but unconnected host startup loads all 28 employees");
Mirror.NetworkClient.isConnected=true;
var joiningEmployeeManager=new NPC_Manager { hiredEmployeesData=new string[18],priorityArray=new int[18],employeesArray=new UnityEngine.GameObject[18] };
EmployeeSaveCapacityPatch.Prefix(new SaveBehaviour { NPCManager=new UnityEngine.GameObject(joiningEmployeeManager) },savedEmployees);
Check(joiningEmployeeManager.hiredEmployeesData.Length==18,"joining client cannot expand staff arrays");
Mirror.NetworkServer.active=true;
EmployeeSaveCapacityPatch.Prefix(new SaveBehaviour { NPCManager=new UnityEngine.GameObject(joiningEmployeeManager) },savedEmployees);
Check(joiningEmployeeManager.hiredEmployeesData.Length==28,"active host retains staff load expansion");
if (args.Contains("--staff-only")) { Console.WriteLine($"Staff load regression passed ({checks} checks). No game save was modified."); return 0; }
Check(CompanionProtocol.Endpoint("https://api.deepseek.com/").AbsoluteUri == "https://api.deepseek.com/chat/completions", "official endpoint");
Check(CompanionProtocol.Endpoint("http://127.0.0.1:9999/v1").AbsoluteUri == "http://127.0.0.1:9999/v1/chat/completions", "local compatible endpoint");
foreach (var endpoint in new[] { "http://example.com", "https://key@example.com", "https://example.com?key=secret", "file:///tmp/api", "garbage" }) Reject(()=>CompanionProtocol.Endpoint(endpoint),"invalid endpoint " + endpoint.Split(':')[0]);
foreach (string role in CompanionProtocol.Roles) Check(CompanionProtocol.ValidateCall(Call("assign_work","{\"role\":\""+role+"\"}"),out _,out _),"native job " + role);
foreach (var bad in new[] { Call("execute_code","{}"), Call("assign_work","{\"role\":\"delete_save\"}"), Call("assign_work","{\"role\":2}"), Call("assign_work","{\"role\":\"restock\",\"employee\":2}"), Call("assign_work","{"), Call("remember","{\"fact\":\"\"}"), Call("forget_memory","{\"number\":0}"), Call("forget_memory","{\"number\":1.5}"), Call("remember","{\"fact\":\""+new string('a',201)+"\"}") })
    Check(!CompanionProtocol.ValidateCall(bad,out _,out _),"reject unsupported or malformed tool");
Check(CompanionProtocol.ValidateCall(Call("remember","{\"fact\":\"我叫阿明\"}"),out _,out _),"Chinese persistent fact");
Check(!CompanionProtocol.ValidateCall(Call("forget_memory","{\"number\":"+new string('9',100)+"}"),out _,out _),"oversized integer cannot crash dispatcher");
Check(!CompanionProtocol.ValidateCall(new JObject{["type"]="function",["id"]="1",["function"]="invalid"},out _,out _),"malformed function object cannot crash dispatcher");
Check(!CompanionProtocol.ValidateCall(Call("assign_work","{\"role\":\"restock\",\"role\":\"rest\"}"),out _,out _),"duplicate JSON properties rejected");
Check(!CompanionProtocol.ValidateCall(Call("assign_work","{\"role\":\"restock\"} {}"),out _,out _),"trailing JSON object rejected");
string dir=Path.Combine(Path.GetTempPath(),"bst-companion-test-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
try {
    string path=Path.Combine(dir,"memory.json");
    var memory=new CompanionMemory { EmployeeIndex=3,EmployeePrefab="17",EmployeeName="小魚" };
    memory.Facts.Add("玩家叫阿明");
    for(int i=0;i<45;i++) memory.Add(i%2==0?"user":"assistant",new string('魚',2000));
    Check(memory.History.Count==40,"bounded recent dialogue");
    memory.Save(path); var loaded=CompanionMemory.Load(path);
    Check(loaded.EmployeeIndex==3 && loaded.EmployeeName=="小魚" && loaded.Facts.Single()=="玩家叫阿明" && loaded.History.Count==40,"UTF-8 memory restart at maximum size");
    memory.Save(path); Check(File.Exists(path+".bak"),"atomic save retains prior version");
    Check(CompanionProtocol.SaveKey("shop1.es3")!=CompanionProtocol.SaveKey("shop2.es3"),"separate shop memory");
    Check(CompanionProtocol.SaveKey("../../bad.es3").Length==24 && !CompanionProtocol.SaveKey("../../bad.es3").Contains('/'),"save filename cannot escape directory");
    File.WriteAllText(path,"{broken"); Reject(()=>CompanionMemory.Load(path),"corrupt memory is rejected");
    Check(File.ReadAllText(path)=="{broken","corrupt memory is not overwritten by load");
} finally { Directory.Delete(dir,true); }

var handler=new FakeHandler(); using var client=new HttpClient(handler);
var messages=new JArray(new JObject{["role"]="user",["content"]="幫我補貨"});
handler.Reply="{\"choices\":[{\"finish_reason\":\"tool_calls\",\"message\":{\"role\":\"assistant\",\"content\":null,\"tool_calls\":[{\"id\":\"c1\",\"type\":\"function\",\"function\":{\"name\":\"assign_work\",\"arguments\":\"{\\\"role\\\":\\\"restock\\\"}\"}}]}}]}";
var response=await CompanionProtocol.Complete(client,CompanionProtocol.Endpoint("https://example.com"),"test-secret","test-model",messages,true,CancellationToken.None);
Check(response["tool_calls"]?.Count()==1,"real transport decodes tool response");
var sent=JObject.Parse(handler.Body);
Check((string)sent["thinking"]?["type"]=="disabled" && (string)sent["model"]=="test-model" && sent["tools"]?.Count()==11 &&
    sent["tools"].Select(x=>(string)x["function"]?["name"]).Contains("assign_staff"),"non-thinking tool request includes staff dispatch schema");
Check(handler.Auth=="Bearer test-secret" && !handler.Body.Contains("test-secret"),"credential stays in authorization header");
handler.Reply="{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"role\":\"assistant\",\"content\":\"收到\"}}]}";
response=await CompanionProtocol.Complete(client,CompanionProtocol.Endpoint("http://localhost:8888/v1"),"","test-model",messages,false,CancellationToken.None);
var followupPayload=JObject.Parse(handler.Body);
Check((string)response["content"]=="收到" && JToken.DeepEquals(sent["tools"],followupPayload["tools"]) && (string)followupPayload["tool_choice"]=="auto" && handler.Auth==null,"followup keeps identical tool schema and choice while allowing keyless local endpoint");
CompanionUsage capturedUsage=null;
handler.Reply="{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"ok\"}}],\"usage\":{\"prompt_tokens\":100,\"completion_tokens\":2,\"prompt_cache_hit_tokens\":70,\"prompt_cache_miss_tokens\":30}}";
await CompanionProtocol.Complete(client,CompanionProtocol.Endpoint("https://example.com"),"fake","model",messages,true,CancellationToken.None,u=>capturedUsage=u);
Check(capturedUsage?.Hit==70 && capturedUsage.Milliseconds>=0,"real transport reports response usage and elapsed time");
handler.Reply="{\"choices\":[{\"finish_reason\":\"tool_calls\",\"message\":{\"content\":null,\"tool_calls\":[{\"id\":\"bad\"}]}}]}";
await RejectAsync(()=>CompanionProtocol.Complete(client,CompanionProtocol.Endpoint("https://example.com"),"fake","model",messages,false,CancellationToken.None),"extra tool calls in final-response stage are rejected client-side");
handler.Status=HttpStatusCode.Unauthorized;
await RejectAsync(()=>CompanionProtocol.Complete(client,CompanionProtocol.Endpoint("https://example.com"),"fake","model",messages,true,CancellationToken.None),"HTTP 401 fails without executing commands");
handler.Status=HttpStatusCode.OK;
handler.Reply="{\"choices\":[{\"finish_reason\":\"length\",\"message\":{\"tool_calls\":[]}}]}";
await RejectAsync(()=>CompanionProtocol.Complete(client,CompanionProtocol.Endpoint("https://example.com"),"fake","model",messages,true,CancellationToken.None),"truncated model response rejected");
handler.Reply=new string('x',262145);
await RejectAsync(()=>CompanionProtocol.Complete(client,CompanionProtocol.Endpoint("https://example.com"),"fake","model",messages,true,CancellationToken.None),"oversized API response rejected");
using(var cancellation=new CancellationTokenSource()) { cancellation.Cancel(); await RejectAsync(()=>CompanionProtocol.Complete(client,CompanionProtocol.Endpoint("https://example.com"),"fake","model",messages,true,cancellation.Token),"cancelled request stops transport"); }
Console.WriteLine($"Completed {checks} checks. No live API was called.");
return 0;
}
catch(Exception error)
{
    // A failed check must return a failing exit code, not open a Windows crash dialog.
    Console.Error.WriteLine("FAIL: "+error);
    return 1;
}

sealed class FakeHandler : HttpMessageHandler {
    public string Reply="",Body="",Auth;
    public HttpStatusCode Status=HttpStatusCode.OK;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        Auth=request.Headers.Authorization?.ToString(); Body=await request.Content.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(Status) { Content=new StringContent(Reply,Encoding.UTF8,"application/json") };
    }
}
