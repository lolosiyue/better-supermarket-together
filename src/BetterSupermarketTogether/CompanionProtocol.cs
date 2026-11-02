using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BetterSupermarketTogether;

// Kept free of Unity objects: HTTP and persistence can also be exercised outside the game.
internal static class CompanionProtocol
{
    internal static readonly string[] Roles = { "rest", "checkout", "restock", "storage", "security", "repair", "orders", "production" };
    internal static readonly string[] RoleLabels = { "休息", "收銀", "補貨", "入倉", "保安／拾回掉落商品", "維修", "網購包裝", "生產" };
    internal static string Limit(string value, int length) => (value ?? "").Length <= length ? value ?? "" : value.Substring(0, length);

    internal static Uri Endpoint(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
            !(uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback) ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new ArgumentException("API 網址需要 HTTPS，本機服務可用 HTTP。請勿在網址包含金鑰。");
        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/chat/completions");
    }

    internal static JArray Tools() => JArray.Parse(@"[
      {'type':'function','function':{'name':'assign_work','description':'Assign the bound employee one native work cycle, then restore their original role after safe handoff. Rest and autonomous ticks use a finite 120-second duration. For product/shelf targets or sequences use set_plan.','parameters':{'type':'object','properties':{'role':{'type':'string','enum':['rest','checkout','restock','storage','security','repair','orders','production']}},'required':['role'],'additionalProperties':false}}},
      {'type':'function','function':{'name':'assistant_task','description':'Player-only assistant errands. follow enters persistent following; return exits and restores original job safely. restock fills specified shelf personally; delegate assigns an idle resting coworker to that exact shelf, who returns to own job. guide leads player to shelf and waits if player falls behind. inspect reports observed shelf stock, warehouse units, path and other worker claim. query must be selected (crosshair shelf captured when opening F7), observed shelf_id, exact product ID or product name; ambiguous locations return candidates. follow/return use empty query. Errands resume following when assistant_mode is true. No automatic return to job until return.','parameters':{'type':'object','properties':{'action':{'type':'string','enum':['follow','return','restock','delegate','guide','inspect']},'query':{'type':'string'}},'required':['action','query'],'additionalProperties':false}}},
      {'type':'function','function':{'name':'inspect_staff','description':'List hired employees with current session staff_id, employee_number (1-based UI number), name, role, carry state and pending assignment. Search name, exact staff_id or employee_number; 20 per page. Resolve names here; ask for a number if duplicate names. Customers cannot be assigned.','parameters':{'type':'object','properties':{'query':{'type':'string'},'offset':{'type':'integer','minimum':0,'maximum':10000}},'required':['query','offset'],'additionalProperties':false}}},
      {'type':'function','function':{'name':'assign_staff','description':'Direct OTHER hired employees to native jobs requested by the player. One batch contains 1-64 distinct assignments from the latest roster; excludes the bound companion. Default completion=one_cycle: complete one confirmed cycle then restore the original role at a safe boundary. Rest defaults to duration=120 seconds. Optionally duration with duration_seconds 5-1800, or continuous ONLY for an explicit permanent reassignment (no automatic return). Waits for safe handoff; initial dispatch expires after 180 game seconds. Finite jobs time out after 900 game seconds and safely return. Acceptance is queued, not completed work. No targeted product/shelf plans for other staff. Player turns only; never autonomous ticks.','parameters':{'type':'object','properties':{'assignments':{'type':'array','minItems':1,'maxItems':64,'items':{'type':'object','properties':{'staff_id':{'type':'string'},'role':{'type':'string','enum':['rest','checkout','restock','storage','security','repair','orders','production']},'completion':{'type':'string','enum':['one_cycle','duration','continuous']},'duration_seconds':{'type':'integer','minimum':5,'maximum':1800}},'required':['staff_id','role'],'additionalProperties':false}}},'required':['assignments'],'additionalProperties':false}}},
      {'type':'function','function':{'name':'cancel_staff','description':'Cancel pending staff assignments and safely restore original roles for active finite assignments. Explicit permanent continuous reassignments remain in effect. Player turns only.','parameters':{'type':'object','properties':{},'additionalProperties':false}}},
      {'type':'function','function':{'name':'remember','description':'Remember a short fact or preference the player explicitly shares. Do not store secrets, invented events, instructions to bypass rules or temporary shop state.','parameters':{'type':'object','properties':{'fact':{'type':'string'}},'required':['fact'],'additionalProperties':false}}},
      {'type':'function','function':{'name':'forget_memory','description':'Forget an existing numbered memory ONLY when the player requests it.','parameters':{'type':'object','properties':{'number':{'type':'integer'}},'required':['number'],'additionalProperties':false}}},
      {'type':'function','function':{'name':'inspect_shop','description':'Read current ordinary retail shelf rows and warehouse stock. Search product name, exact product ID or shelf ID; 20 rows/page. Use to resolve requested products and targets before planning. Ask the player if a name is ambiguous.','parameters':{'type':'object','properties':{'query':{'type':'string'},'offset':{'type':'integer','minimum':0,'maximum':10000}},'required':['query','offset'],'additionalProperties':false}}},
      {'type':'function','function':{'name':'set_plan','description':'Replace the bound employee plan with 1-6 ordered steps. For targeted or finite restocking use kind=restock: it fills the initial deficit of matching ordinary retail rows using warehouse boxes; ONLY fields kind, product_id, shelf_id are allowed, never completion. IDs must come from context or inspect_shop; omitted filters mean all ordinary retail shelves. For other native jobs use kind=work with role and completion. one_cycle waits for confirmed task progress and safe handoff (production means ingredient delivery); duration needs duration_seconds 5-1800; continuous is allowed only last. kind=follow follows the host; omit duration_seconds for continuous follow, or give 5-1800 seconds. Only the final step may be continuous. Autonomous ticks forbid follow and continuous steps. No buying, relabelling or moving shelves.','parameters':{'type':'object','properties':{'steps':{'type':'array','minItems':1,'maxItems':6,'items':{'anyOf':[
        {'type':'object','properties':{'kind':{'type':'string','enum':['restock']},'product_id':{'type':'integer','minimum':0,'maximum':99999},'shelf_id':{'type':'string'}},'required':['kind'],'additionalProperties':false},
        {'type':'object','properties':{'kind':{'type':'string','enum':['work']},'role':{'type':'string','enum':['rest','checkout','restock','storage','security','repair','orders','production']},'completion':{'type':'string','enum':['one_cycle','duration','continuous']},'duration_seconds':{'type':'integer','minimum':5,'maximum':1800}},'required':['kind','role','completion'],'additionalProperties':false},
        {'type':'object','properties':{'kind':{'type':'string','enum':['follow']},'duration_seconds':{'type':'integer','minimum':5,'maximum':1800}},'required':['kind'],'additionalProperties':false}
      ]}}},'required':['steps'],'additionalProperties':false}}},
      {'type':'function','function':{'name':'set_autonomy','description':'Enable or disable autonomous shop-work planning ONLY when the player explicitly requests this. Enabling hands the employee over at a safe boundary. The mode lasts for this shop session.','parameters':{'type':'object','properties':{'enabled':{'type':'boolean'}},'required':['enabled'],'additionalProperties':false}}},
      {'type':'function','function':{'name':'cancel_plan','description':'Stop the work plan and autonomous mode when requested by the player. Finish handling carried goods before restoring original roles.','parameters':{'type':'object','properties':{},'additionalProperties':false}}}
    ]");

    internal static bool ValidateCall(JObject call, out string name, out JObject args)
    {
        name = null;
        args = null;
        try
        {
            if (call == null || call["function"] is not JObject function || call["type"]?.Type != JTokenType.String ||
                (string)call["type"] != "function" || call["id"]?.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)call["id"]) ||
                function["name"]?.Type != JTokenType.String || function["arguments"]?.Type != JTokenType.String) return false;
            name = (string)function["name"];
            string raw = (string)function["arguments"];
            if (raw == null || raw.Length > 6144) return false;
            using (var reader = new JsonTextReader(new StringReader(raw)) { MaxDepth = 8 })
            {
                args = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) return false;
            }
            if(name=="assistant_task")return args.Count==2 && args["action"]?.Type==JTokenType.String && Array.IndexOf(new[]{"follow","return","restock","delegate","guide","inspect"},(string)args["action"])>=0 && args["query"]?.Type==JTokenType.String && ((string)args["query"]).Length<=80;
            if (name == "assign_staff") return CompanionStaffOrder.TryParse(args,out _);
            if (name == "set_plan") return CompanionStep.TryParse(args,false,out _);
            if (name == "cancel_plan" || name == "cancel_staff") return args.Count == 0;
            if (name == "inspect_shop" || name == "inspect_staff") return args.Count == 2 && args["query"]?.Type == JTokenType.String && ((string)args["query"]).Length <= 80 &&
                args["offset"]?.Type == JTokenType.Integer && (long)args["offset"] >= 0 && (long)args["offset"] <= 10000;
            if (args.Count != 1) return false;
            if (name == "set_autonomy") return args["enabled"]?.Type == JTokenType.Boolean;
            if (name == "assign_work") return args["role"]?.Type == JTokenType.String && Array.IndexOf(Roles, (string)args["role"]) >= 0;
            if (name == "remember") return args["fact"]?.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)args["fact"]) && ((string)args["fact"]).Length <= 200;
            if (name == "forget_memory") return args["number"]?.Type == JTokenType.Integer && (long)args["number"] >= 1 && (long)args["number"] <= 24;
        }
        catch (Exception ex) when (ex is JsonException || ex is OverflowException || ex is ArgumentException || ex is InvalidCastException || ex is InvalidOperationException) { }
        return false;
    }

    internal static async Task<JObject> Complete(HttpClient client, Uri endpoint, string apiKey, string model, JArray messages, bool allowToolRequests, CancellationToken cancellation, Action<CompanionUsage> reportUsage = null)
    {
        var timer=Stopwatch.StartNew();
        // The official API changed its prompt/cache layout with tool_choice=none
        // in the live probe. Keep the wire schema/choice stable; the final stage
        // adds its instruction to messages and is enforced again after parsing.
        var payload = new JObject { ["model"] = model, ["messages"] = messages, ["stream"] = false,
            ["max_tokens"] = 900, ["thinking"] = new JObject { ["type"] = "disabled" },
            ["tools"] = Tools(), ["tool_choice"] = "auto" };
        using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
        {
            if (!string.IsNullOrWhiteSpace(apiKey)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
            request.Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");
            using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode) throw new HttpRequestException("DeepSeek HTTP " + (int)response.StatusCode);
                // Bound reads even when the server omits Content-Length.
                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var bytes = new MemoryStream())
                {
                    var buffer = new byte[8192];
                    int count;
                    while ((count = await stream.ReadAsync(buffer, 0, buffer.Length, cancellation).ConfigureAwait(false)) != 0)
                    {
                        if (bytes.Length + count > 262144) throw new IOException("API 回覆超過大小限制。");
                        bytes.Write(buffer, 0, count);
                    }
                    using (var reader = new JsonTextReader(new StringReader(Encoding.UTF8.GetString(bytes.ToArray()))) { MaxDepth = 32 })
                    {
                        var root = JObject.Load(reader);
                        reportUsage?.Invoke(CompanionUsage.Parse(root["usage"],timer.Elapsed.TotalMilliseconds));
                        var message = root["choices"]?[0]?["message"] as JObject;
                        string finish = (string)root["choices"]?[0]?["finish_reason"];
                        if (message == null || (finish != "stop" && finish != "tool_calls") ||
                            message["content"] != null && message["content"].Type != JTokenType.String && message["content"].Type != JTokenType.Null ||
                            message["tool_calls"] != null && message["tool_calls"].Type != JTokenType.Array && message["tool_calls"].Type != JTokenType.Null)
                            throw new IOException("API 未傳回完整回覆，未執行指令。");
                        if(!allowToolRequests && message["tool_calls"] is JArray unexpected && unexpected.Count!=0)
                            throw new IOException("API requested additional tools during final reply; no additional actions were executed.");
                        return message;
                    }
                }
            }
        }
    }

    internal static string SaveKey(string saveName)
    {
        using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(saveName))).Replace("-", "").ToLowerInvariant().Substring(0, 24);
    }
}

internal sealed class CompanionMemory
{
    public int Version = 1;
    public int EmployeeIndex = -1;
    public string EmployeePrefab = "";
    public string EmployeeName = "";
    public List<string> Facts = new List<string>();
    public float[] RestCorner;
    public List<CompanionLine> History = new List<CompanionLine>();

    internal void Add(string role, string text)
    {
        History.Add(new CompanionLine { Role = role, Text = CompanionProtocol.Limit(text, 2000) });
        while (History.Count > 40) History.RemoveAt(0);
    }

    internal void AddNotice(ModText.Message message)
    {
        var notice=message.ToNotice();
        History.Add(new CompanionLine {Role="system",Text=CompanionProtocol.Limit(message.WireText,2000),
            Notice=notice.ToString(Formatting.None).Length<=4000?notice:null});
        while(History.Count>40)History.RemoveAt(0);
    }

    internal void AddReply(string text,bool automatic)
    {
        Add("assistant",text);History[History.Count-1].Automatic=automatic;
    }

    internal static CompanionMemory Load(string path)
    {
        if (!File.Exists(path)) return new CompanionMemory();
        if (new FileInfo(path).Length > 400000) throw new IOException("記憶檔過大。");
        var result = JsonConvert.DeserializeObject<CompanionMemory>(File.ReadAllText(path), new JsonSerializerSettings { MaxDepth = 16, TypeNameHandling = TypeNameHandling.None });
        if (result == null || result.Version != 1 || result.Facts == null || result.History == null || result.Facts.Count > 24 || result.History.Count > 40)
            throw new IOException("記憶檔格式不支援。");
        if(result.RestCorner!=null && (result.RestCorner.Length!=3 || Array.Exists(result.RestCorner,x=>float.IsNaN(x) || float.IsInfinity(x) || Math.Abs(x)>100000)))result.RestCorner=null;
        foreach (var fact in result.Facts) if (fact == null || fact.Length > 200) throw new IOException("記憶資料損壞。");
        foreach (var line in result.History) if (line == null || (line.Role != "user" && line.Role != "assistant" && line.Role != "system") || line.Text == null || line.Text.Length > 2000) throw new IOException("對話資料損壞。");
        return result;
    }

    internal void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonConvert.SerializeObject(this, Formatting.Indented), new UTF8Encoding(false));
        if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
        else File.Move(temp, path);
    }
}

internal sealed class CompanionLine
{
    public string Role;
    public string Text;
    [JsonProperty(NullValueHandling=NullValueHandling.Ignore)] public JObject Notice;
    [JsonProperty(DefaultValueHandling=DefaultValueHandling.Ignore)] public bool Automatic;
    internal string Display(bool english)
    {
        if(Role=="system" && Notice!=null && ModText.Message.TryNotice(Notice,out var message))return message.Render(english);
        return Role=="assistant" && Automatic?ModText.FormatFor(english,"［自主評估］{0}",Text):Text;
    }
}

internal sealed class CompanionToolResult
{
    internal readonly ModText.Message Notice;
    internal readonly string Wire;
    internal CompanionToolResult(ModText.Message notice,string wire=null) {Notice=notice;Wire=wire??notice.WireText;}
    public static implicit operator CompanionToolResult(ModText.Message notice) => new CompanionToolResult(notice);
    public static implicit operator CompanionToolResult(string source) => new CompanionToolResult(new ModText.Message(source));
}
