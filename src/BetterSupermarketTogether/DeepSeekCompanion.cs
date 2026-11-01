using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using HutongGames.PlayMaker;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BetterSupermarketTogether;

internal sealed class DeepSeekCompanion
{
    private readonly BetterSupermarketPlugin _owner;
    private readonly HttpClient _http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(45) };
    private readonly ConfigEntry<string> _url, _model, _persona;
    private readonly ConfigEntry<KeyboardShortcut> _key;
    private readonly ConfigEntry<bool> _avatarEnabled;
    private readonly CompanionAvatar _avatar;
    private readonly CompanionRiceBreak _riceBreak;
    private readonly ConfigEntry<bool> _riceEnabled;
    internal bool RiceOwns(NPC_Info employee) => _riceBreak.Owns(employee);
    internal bool NeedsSafeHandoff => _plan.HandingOff;
    private readonly CompanionContext _context = new CompanionContext();
    private CompanionUsageStats _usage = new CompanionUsageStats();
    internal CompanionWorkWorld WorkWorld { get; } = new CompanionWorkWorld();
    internal CompanionStaffWorld StaffWorld { get; } = new CompanionStaffWorld();
    private readonly CompanionPlanRunner _plan = new CompanionPlanRunner();
    private readonly CompanionAutonomyGate _autoGate = new CompanionAutonomyGate();
    private readonly ConfigEntry<int> _autoInterval, _autoLimit;
    private bool _autonomy, _automaticRequest, _automaticSucceeded;
    private float _nextWorkTick, _nextAutoScan, _nextStaffTick;
    private ModText.Message _lastPlanEvent,_lastRiceEvent;
    private int _lastPlanEventVersion=-1;
    private CompanionMemory _memory;
    private string _saveName, _memoryPath;
    private NPC_Info _bound;
    private CancellationTokenSource _request;
    private Coroutine _conversation;
    private int _generation;
    private float _nextSend;
    private float _nextUiRefresh;
    private string _transcriptCache;
    private bool _transcriptEnglish;
    internal bool Busy => _conversation != null;
    private ModText.Message _status = "讀取房主商店後可綁定一名員工。";
    private bool _memoryWriteFailed;
    internal string Status { get => _status.ToString()+(_memoryWriteFailed?ModText.Get("（記憶尚未寫入磁碟）"):""); private set => _status = value; }
    internal string BaseUrl => _url.Value;
    internal string Model => _model.Value;
    internal string AvatarStatus => ModText.Get(_avatar.Status);
    internal string CacheStatus => _usage.Summary(ModText.English);
    internal bool Autonomous => _autonomy;
    internal string WorkStatus
    {
        get
        {
            bool english=ModText.English;
            return ModText.FormatFor(english,"自主模式：{0} · 本小時決策 {1}/{2}",ModText.Translate(_autonomy?"開":"關",english),_autoGate.Count(Time.unscaledTime),_autoLimit.Value)+
                "\n"+_plan.Summary(english)+(WorkWorld.SelectedShelf!=null?"\n"+ModText.FormatFor(english,"指向貨架：{0}",WorkWorld.SelectedShelf):"")+
                "\n"+ModText.FormatFor(english,"其他員工臨時任務：{0}（待交接 {1}）",StaffWorld.Queue.Count,StaffWorld.Queue.WaitingCount);
        }
    }
    internal bool HasMemory => _memory != null;
    internal int BoundIndex => _memory?.EmployeeIndex ?? -1;
    internal NPC_Info BoundEmployee => _bound;
    internal Sprite Portrait => _avatar.Portrait;
    internal string Transcript
    {
        get
        {
            bool english = ModText.English;
            if (_transcriptCache == null || _transcriptEnglish != english)
            {
                _transcriptEnglish = english;
                _transcriptCache = _memory == null ? ModText.Get("尚未讀取商店記憶。") : string.Join("\n\n", _memory.History.Select(x => ModText.Get(x.Role == "user" ? "你：" : x.Role == "assistant" ? "DeepSeek 娘：" : "［執行結果］") + x.Display(english)));
            }
            return _transcriptCache;
        }
    }
    internal string Facts => _memory == null || _memory.Facts.Count == 0 ? ModText.Get("尚未儲存偏好；可以說「記住我叫阿明」。") : string.Join("\n", _memory.Facts.Select((x, i) => (i + 1) + ". " + x));
    internal string BindingLabel => _memory == null || BoundIndex < 0 ? ModText.Get("未綁定員工") : ModText.Format("員工 {0}：{1}", BoundIndex + 1, _memory.EmployeeName) + (_bound == null ? ModText.Get("（等待員工載入／需重新綁定）") : " · " + ModText.Get(CompanionProtocol.RoleLabels[Mathf.Clamp(_bound.taskPriority, 0, 7)]));
    private string KeyPath => Path.Combine(Paths.ConfigPath, "BetterSupermarketTogether.deepseek.key");

    internal DeepSeekCompanion(BetterSupermarketPlugin owner, ConfigFile config)
    {
        _owner = owner;
        _url = config.Bind("Companion", "BaseUrl", "https://api.deepseek.com", ModText.Description("DeepSeek／相容 API 的基底網址；本機 HTTP 亦可。", "DeepSeek-compatible API base URL. Local loopback HTTP is also supported."));
        _model = config.Bind("Companion", "Model", "deepseek-flash", ModText.Description("同伴 API 模型名稱。", "Model name for the companion API."));
        _persona = config.Bind("Companion", "Personality", "你是 DeepSeek 娘，一位溫柔、有主見、偶爾俏皮的超市同伴。用繁體中文自然簡短回答，也能配合玩家的廣東話。", ModText.Description("同伴性格；工作能力仍受遊戲工具限制。", "Companion personality. Available work actions remain restricted by the game tools."));
        _key = config.Bind("Companion", "ChatKey", new KeyboardShortcut(KeyCode.F7), ModText.Description("房主同伴聊天面板。", "Keyboard shortcut for the host companion chat panel."));
        _avatarEnabled = config.Bind("Companion", "UseDeepSeekAvatar", true, ModText.Description("房主本機顯示隨附 DeepSeek 娘外觀；原生員工動畫仍會運行。", "Show the bundled DeepSeek avatar locally for the host while retaining native employee animations."));
        _autoInterval=config.Bind("Companion","AutonomyIntervalSeconds",120,new ConfigDescription(
            ModText.Description("自主評估最短間隔；只在狀態改變且無玩家計畫時評估。","Minimum interval between autonomous decisions; only changed state without an active player plan is evaluated."),new AcceptableValueRange<int>(60,1800)));
        _autoLimit=config.Bind("Companion","AutonomyDecisionsPerHour",20,new ConfigDescription(
            ModText.Description("每小時自主決策上限；每次決策最多四個 API 回覆。","Maximum autonomous decisions per hour; each decision has at most four API replies."),new AcceptableValueRange<int>(1,60)));
        _avatar = owner.gameObject.AddComponent<CompanionAvatar>();
        _riceBreak=new CompanionRiceBreak(_avatar);
        _riceEnabled=config.Bind("Companion","RiceEasterEgg",true,ModText.Description("低頻率偷吃一份貨架米商品；每遊戲日最多一次，需同伴休息且空手。","Rarely eat one stocked rice product while the companion is resting and empty-handed; at most once per game day."));
    }

    internal void Tick()
    {
        if (!BetterSupermarketPlugin.IsHost || GameData.Instance == null) { Reset(); return; }
        string saveName = FsmVariables.GlobalVariables.GetFsmString("CurrentFilename")?.Value;
        if (string.IsNullOrWhiteSpace(saveName)) { if (_saveName != null) Reset(); return; }
        if (_saveName != saveName)
        {
            Reset();
            _saveName = saveName;
            _memoryPath = Path.Combine(Paths.ConfigPath, "BetterSupermarketTogether.Companion", CompanionProtocol.SaveKey(saveName) + ".json");
            try { _memory = CompanionMemory.Load(_memoryPath); Status = "已讀取本商店記憶；請綁定員工並設定 API。"; }
            catch (Exception) { Status = "記憶檔無法讀取，已保留原檔。請先備份並修復或移走該 JSON 檔再重進商店。"; }
        }
        var employee = ResolveEmployee();
        if (_bound != null && employee != _bound) { Cancel();_plan.Reset(WorkWorld);StaffWorld.Queue.CancelAll();_riceBreak.Abort();_autonomy=false; }
        _bound = employee;
        WorkWorld.Bind(_bound);
        _avatar.Bind(_avatarEnabled.Value ? _bound : null, Path.Combine(Path.GetDirectoryName(typeof(DeepSeekCompanion).Assembly.Location), "Companion", "DeepSeek-chan.vrm"));
        if (_bound != null && _memory.EmployeeName != _bound.NPCName)
        {
            _memory.EmployeeName = _bound.NPCName;
            Persist();
        }
        if (_key.Value.IsDown() && !SettingsPanelUI.Typing) SettingsPanelUI.OpenCompanion();
        if (!BetterSupermarketPlugin.EnablePlugin)
        {
            if(Busy)Cancel();
            if(_plan.Owns)
            {
                _plan.Cancel();
                if(_bound!=null && Time.timeScale>0 && !NPC_Manager.Instance.employeesOnRiot)_plan.Tick(Time.time,WorkWorld);
            }
            StaffWorld.Queue.CancelAll();
            _riceBreak.Abort();
            _autonomy=false;
        }
        else if(_bound!=null && Time.timeScale>0)
        {
            if(Time.unscaledTime>=_nextWorkTick)
            {
                _nextWorkTick=Time.unscaledTime+.2f;
                if(!NPC_Manager.Instance.employeesOnRiot)_plan.Tick(Time.time,WorkWorld);
                if(WorkWorld.TakeExternalEvent())_autonomy=false;
                if((_plan.Phase==CompanionPlanPhase.Completed || _plan.Phase==CompanionPlanPhase.Failed) && _lastPlanEventVersion!=_plan.EventVersion)
                {
                    _lastPlanEvent=_plan.Status;_lastPlanEventVersion=_plan.EventVersion;_memory.AddNotice(_plan.Status);Persist();_autoGate.Changed();
                }
            }
            if(!NPC_Manager.Instance.employeesOnRiot)TickAutonomy();
        }
        if(Time.timeScale>0 && Time.unscaledTime>=_nextStaffTick)
        {
            _nextStaffTick=Time.unscaledTime+.2f;
            StaffWorld.Queue.Tick(Time.time,StaffWorld);
            bool staffChanged=false;
            foreach(var notice in StaffWorld.Queue.Drain()) { _memory?.AddNotice(notice);staffChanged=true; }
            if(staffChanged)Persist();
            var snack=_riceBreak.Tick(_bound,BetterSupermarketPlugin.EnablePlugin && _riceEnabled.Value && _bound!=null &&
                !Busy && !_plan.Owns && !_autonomy && !NPC_Manager.Instance.employeesOnRiot);
            if(snack!=null) { _lastRiceEvent=snack;_memory?.AddNotice(snack);Persist(); }
        }
        if (Time.unscaledTime >= _nextUiRefresh) { _nextUiRefresh = Time.unscaledTime + .2f; SettingsPanelUI.RefreshCompanion(); }
    }

    private NPC_Info ResolveEmployee()
    {
        var npc = NPC_Manager.Instance;
        int index = BoundIndex;
        if (npc == null || !npc.isServer || !npc.initialEmployeesSpawnIsFinished || index < 0 ||
            npc.employeesArray == null || index >= npc.employeesArray.Length || npc.employeesArray[index] == null ||
            npc.hiredEmployeesData == null || index >= npc.hiredEmployeesData.Length) return null;
        string[] data = (npc.hiredEmployeesData[index] ?? "").Split('|');
        if (data.Length <= 9 || data[0] != _memory.EmployeePrefab) return null;
        var result = npc.employeesArray[index].GetComponent<NPC_Info>();
        if (result == null || !result.isEmployee || result.employeeDismissed) return null;
        // During a live session the object is authoritative, allowing native renames.
        if (_bound != result && result.NPCName != _memory.EmployeeName) return null;
        return result;
    }

    internal void Bind(int index)
    {
        var npc = NPC_Manager.Instance;
        if (!BetterSupermarketPlugin.EnablePlugin || _memory == null || npc == null || !npc.isServer ||
            !npc.initialEmployeesSpawnIsFinished || npc.hiredEmployeesData == null || npc.employeesArray == null ||
            index < 0 || index >= npc.hiredEmployeesData.Length || index >= npc.employeesArray.Length || npc.employeesArray[index] == null)
        { Status = "請先啟用插件，並選擇已載入的在職員工。"; return; }
        var employee = npc.employeesArray[index].GetComponent<NPC_Info>();
        string[] data = (npc.hiredEmployeesData[index] ?? "").Split('|');
        if (employee == null || employee.employeeDismissed || !employee.isEmployee || data.Length <= 9) return;
        Cancel();
        _plan.Reset(WorkWorld);_autonomy=false;
        StaffWorld.Queue.CancelAll();_riceBreak.Abort();
        _lastPlanEvent=null;_lastPlanEventVersion=-1;
        _context.Reset();
        _memory.EmployeeIndex = index;
        _memory.EmployeePrefab = data[0];
        _memory.EmployeeName = employee.NPCName;
        _bound = employee;
        WorkWorld.Bind(employee);
        _status = new ModText.Message("已綁定 {0}。", new ModText.LiveArgument(() => BindingLabel));
        Persist();
    }

    internal void Unbind()
    {
        if (!BetterSupermarketPlugin.IsHost || _memory == null) return;
        Cancel();
        _plan.Reset(WorkWorld);_autonomy=false;
        StaffWorld.Queue.CancelAll();_riceBreak.Abort();
        _lastPlanEvent=null;_lastPlanEventVersion=-1;
        _context.Reset();
        _bound = null;
        _avatar.Clear();
        _memory.EmployeeIndex = -1;
        _memory.EmployeePrefab = _memory.EmployeeName = "";
        Status = "已解除綁定；保留聊天記憶，員工繼續目前職務。";
        Persist();
    }

    internal void Dismissed(int index) { if (BoundIndex == index) Unbind(); }

    internal void Configure(string url, string model, string newKey)
    {
        if (!BetterSupermarketPlugin.IsHost) return;
        try
        {
            CompanionProtocol.Endpoint(url.Trim());
            if (string.IsNullOrWhiteSpace(model) || model.Length > 100) throw new ArgumentException("請填寫模型名稱。");
            bool changed = _url.Value != url.Trim() || _model.Value != model.Trim() || !string.IsNullOrWhiteSpace(newKey);
            Cancel();
            if (changed) { _context.Reset(); _usage = new CompanionUsageStats(); }
            _url.Value = url.Trim(); _model.Value = model.Trim();
            if (!string.IsNullOrWhiteSpace(newKey))
            {
                if (newKey.Trim().Any(char.IsControl)) throw new ArgumentException("金鑰包含無效字元。");
                File.WriteAllText(KeyPath, newKey.Trim(), new System.Text.UTF8Encoding(false));
            }
            Status = "API 設定已儲存。留空金鑰會沿用本機金鑰或 DEEPSEEK_API_KEY。";
        }
        catch (ArgumentException ex) { Status = ex.Message; }
        catch (Exception) { Status = "API 設定無法儲存，請檢查本機設定目錄權限。"; }
    }

    internal void ClearMemory()
    {
        if (!BetterSupermarketPlugin.IsHost || _memory == null) return;
        Cancel(); _context.Reset(); _memory.Facts.Clear(); _memory.History.Clear();
        _transcriptCache = null;
        // An explicit erase also removes the automatically retained previous version.
        try { _memory.Save(_memoryPath); if (File.Exists(_memoryPath + ".bak")) File.Delete(_memoryPath + ".bak"); Status = "已清除本商店對話及偏好，保留員工綁定。"; }
        catch (Exception) { Status = "清除未能寫入磁碟，請檢查設定目錄權限。"; }
    }

    internal void Send(string text)
    {
        if(Busy && _automaticRequest)Cancel(); // Player messages preempt in-flight autonomous decisions.
        StartRequest(text,false);
    }

    private void StartRequest(string text,bool automatic)
    {
        if (!BetterSupermarketPlugin.EnablePlugin || _memory == null || ResolveEmployee() == null) { Status = "請先綁定一名在職員工，並啟用插件。"; return; }
        if (Busy || Time.unscaledTime < _nextSend) { Status = "請等待目前回覆，或按取消。"; return; }
        text = (text ?? "").Trim();
        if (text.Length == 0 || text.Length > 1000) { Status = "訊息長度需為 1–1000 字。"; return; }
        string key;
        Uri endpoint;
        try
        {
            endpoint = CompanionProtocol.Endpoint(_url.Value);
            key = File.Exists(KeyPath) ? File.ReadAllText(KeyPath).Trim() : Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
            if (string.IsNullOrWhiteSpace(key) && !endpoint.IsLoopback) { if(automatic)_autoGate.Failure(Time.unscaledTime);Status = "請先在 API 設定輸入金鑰；金鑰不會顯示於聊天或日誌。"; return; }
        }
        catch (Exception) { if(automatic)_autoGate.Failure(Time.unscaledTime);Status = "無法讀取 API 設定或金鑰。"; return; }
        _automaticRequest=automatic;_automaticSucceeded=false;
        _riceBreak.Abort();
        var messages = BuildMessages(text,automatic);
        if(!automatic){_memory.Add("user", text);Persist();}
        _request = new CancellationTokenSource(TimeSpan.FromSeconds(50));
        _nextSend = Time.unscaledTime + 2f;
        int generation = _generation;
        Status = "DeepSeek 娘正在想……";
        _conversation = _owner.StartCoroutine(Converse(endpoint, key, _model.Value, messages, generation, _request.Token,automatic));
    }

    private JArray BuildMessages(string text,bool automatic)
    {
        var employee = _bound;
        var snapshot = new JObject {
            ["day"] = GameData.Instance.gameDay, ["hour"] = GameData.Instance.timeOfDay, ["open"] = GameData.Instance.isSupermarketOpen,
            ["bound_employee"] = BindingLabel, ["state"] = employee.state, ["carrying"] = employee.boxNumberOfProducts,
            ["riot"] = NPC_Manager.Instance.employeesOnRiot, ["production"] = ProductionAutomation.Status };
        var current = new JObject { ["memories"] = new JArray(_memory.Facts.Select((fact,index)=>new JObject { ["number"]=index+1,["fact"]=fact })),
            ["shop"] = snapshot,["work_board"]=WorkWorld.Snapshot(),["staff_roster"]=StaffWorld.Inspect("",0),["work_plan"]=_plan.Summary(true),["latest_work_event"]=_lastPlanEvent?.WireText,["latest_rice_event"]=_lastRiceEvent?.WireText,
            ["authorization_mode"]=automatic?"autonomous":"player",["autonomy_enabled"]=_autonomy,
            ["reply_language"] = ModText.English ? "English" : "Traditional Chinese; follow the player's language preference" };
        return _context.Begin(_persona.Value,_memory.History,current,text);
    }

    private bool Current(int generation, CancellationToken token) => generation == _generation && !token.IsCancellationRequested && BetterSupermarketPlugin.IsHost && BetterSupermarketPlugin.EnablePlugin && _bound != null && ResolveEmployee() == _bound;

    private IEnumerator Converse(Uri endpoint, string key, string model, JArray messages, int generation, CancellationToken token,bool automatic)
    {
        // First yield ensures StartCoroutine returns before any completion clears the handle.
        yield return null;
        var stats = _usage;
        Action<CompanionUsage> reportUsage = usage => { stats.Record(usage); BetterSupermarketPlugin.Log.LogInfo(usage.LogText); };
        int rounds=0;var budget=new CompanionToolBudget();bool finalOnly=false;
        while(true)
        {
        rounds++;
        var task = CompanionProtocol.Complete(_http, endpoint, key, model, messages, !finalOnly, token,reportUsage);
        while (!task.IsCompleted && Current(generation, token)) yield return null;
        if (!Current(generation, token)) { if (generation == _generation) Finish("請求已取消或員工狀態已改變。"); yield break; }
        if (!ReadResult(task, out var response)) yield break;
        var calls = response["tool_calls"] as JArray;
        if (calls != null && calls.Count > 0)
        {
            if (!budget.Accept(calls,automatic))
            {
                if(budget.TryCorrection(calls,rounds))
                {
                    messages.Add(_context.Assistant(response));
                    foreach(var call in calls)messages.Add(_context.Tool((string)call["id"],CompanionToolBudget.Rejection));
                    continue;
                }
                Finish("模型指令格式無效或一次派了多個工作，未執行任何指令。請換個說法。");yield break;
            }
            messages.Add(_context.Assistant(response));
            foreach (JObject call in calls)
            {
                if (!Current(generation, token)) { Finish("員工或商店狀態已改變，已停止派工。"); yield break; }
                CompanionProtocol.ValidateCall(call, out string name, out JObject args);
                CompanionToolResult result;
                try { result = Execute(name, args,automatic); }
                catch(Exception) { result=new CompanionToolResult("工具執行失敗，未確認成功。"); }
                messages.Add(_context.Tool((string)call["id"],result.Wire));
                _memory.AddNotice(result.Notice);
            }
            Persist();
            Status = "已取得執行結果，正在回覆……";
            finalOnly=budget.Controls>0 || rounds>=3 || budget.Calls>=8;
            if(finalOnly)messages.Add(_context.FinalOnly());
            continue;
        }
        string answer = (string)response["content"];
        if (!string.IsNullOrWhiteSpace(answer)) { _context.Answer(answer); _memory.AddReply(answer,automatic); Persist(); if(automatic){_autoGate.Success();_automaticSucceeded=true;}Finish("已回覆。" ); }
        else Finish("API 沒有文字回覆；已執行的結果可見對話記錄。");
        yield break;
        }
    }

    private CompanionToolResult Execute(string name, JObject args,bool automatic)
    {
        if(!CompanionToolPolicy.Allowed(name,automatic))return "自主模式不能指揮其他員工；請由玩家發出派工指令。";
        if(name=="inspect_staff")
        {
            var data=StaffWorld.Inspect((string)args["query"],(int)args["offset"]);
            return new CompanionToolResult(new ModText.Message("已查閱員工名單，符合 {0} 人；顯示第 {1} 頁。",(int)data["total"],(int)args["offset"]/20+1),data.ToString(Newtonsoft.Json.Formatting.None));
        }
        if(name=="assign_staff")
        {
            if(!CompanionStaffOrder.TryParse(args,out var orders))return "員工派工格式無效，未執行任何指令。";
            return StaffWorld.Queue.Enqueue(orders,Time.time,StaffWorld);
        }
        if(name=="cancel_staff")
        {
            StaffWorld.Queue.CancelAll();return "已取消其他員工臨時派工；已開始的工作會在安全交接後恢復原職。";
        }
        if(name=="inspect_shop")
        {
            var data=WorkWorld.Inspect((string)args["query"],(int)args["offset"]);
            return new CompanionToolResult(new ModText.Message("已查閱商店資料，符合 {0} 列；顯示第 {1} 頁。",(int)data["total"],(int)args["offset"]/20+1),data.ToString(Newtonsoft.Json.Formatting.None));
        }
        if(name=="set_autonomy"){SetAutonomy((bool)args["enabled"]);return _autonomy?"已開啟本次商店的自主工作。":"已關閉自主工作。";}
        if(name=="cancel_plan"){StopWork(false);return "已停止臨時計畫及自主模式；安全交接後恢復原職。";}
        if(name=="set_plan")
        {
            if(!CompanionStep.TryParse(args,automatic,out var steps))return "計畫格式無效，未排入任何工作。";
            return QueuePlan(steps,automatic);
        }
        if (name == "remember")
        {
            string fact = ((string)args["fact"]).Trim();
            if (_memory.Facts.Contains(fact)) return "已存在此記憶。";
            if (_memory.Facts.Count >= 24) return "記憶已滿（24 項），請先忘記不需要的資料。";
            _memory.Facts.Add(fact); return new ModText.Message("已記住：{0}", fact);
        }
        if (name == "forget_memory")
        {
            int index = (int)args["number"] - 1;
            if (index >= _memory.Facts.Count) return "沒有此編號的記憶。";
            _memory.Facts.RemoveAt(index); return "已忘記指定偏好；原始對話仍會保留，清空全部可在面板操作。";
        }
        return QueuePlan(new List<CompanionStep>{new CompanionStep {Kind="work",Role=(string)args["role"],Completion=automatic || (string)args["role"]=="rest"?"duration":"one_cycle",Seconds=automatic || (string)args["role"]=="rest"?120:0}},automatic);
    }

    private CompanionToolResult QueuePlan(List<CompanionStep> steps,bool automatic)
    {
        if(ResolveEmployee()==null || NPC_Manager.Instance==null || NPC_Manager.Instance.employeesOnRiot)
            return "未接受計畫：員工不可用或罷工中。";
        var error=WorkWorld.Validate(steps);
        if(error!=null)return new ModText.Message("計畫未被接受：{0}",error);
        _riceBreak.Abort();
        _lastPlanEvent=null;_plan.Replace(steps,automatic);
        _plan.Tick(Time.time,WorkWorld);
        ModText.Message notice=!_plan.Owns && _plan.Phase==CompanionPlanPhase.Failed?new ModText.Message("計畫未能開始：{0}",_plan.Status):
            new ModText.Message("計畫已排入，尚未完成：{0}",steps.Count==1?steps[0].Label:new ModText.Message("{0}（另有 {1} 步）",steps[0].Label,steps.Count-1));
        return new CompanionToolResult(notice,notice.WireText+"\n"+string.Join(" → ",steps.Select(x=>x.Label.WireText))+"\n"+_plan.Summary(true));
    }
    internal void SetAutonomy(bool enabled)
    {
        if(!BetterSupermarketPlugin.IsHost || _memory==null || _bound==null)return;
        if(Busy && _automaticRequest)Cancel();
        _riceBreak.Abort();
        _autonomy=enabled;_autoGate.Changed();
        if(enabled)_plan.Cancel();
        Status=enabled?"已開啟自主工作；每次商店讀檔後需重新開啟。":"已關閉自主工作。";
    }
    internal void StopWork(bool cancelRequest=true)
    {
        if(!BetterSupermarketPlugin.IsHost || _memory==null)return;
        if(cancelRequest)Cancel();
        _riceBreak.Abort();
        _autonomy=false;_plan.Cancel();StaffWorld.Queue.CancelAll();Status="已停止臨時計畫及自主模式；安全交接後恢復原職。";
    }
    internal void SelectShelf() { _status=WorkWorld.PointAtShelf(); }
    internal void ExternalRoleChange(int index)
    {
        if(index==BoundIndex)_riceBreak.Abort();
        if(StaffWorld.ChangingNativeRole)return;
        StaffWorld.ExternalChange(index);
        if(BetterSupermarketPlugin.IsHost && Busy && !WorkWorld.ChangingNativeRole)Cancel();
        if(!BetterSupermarketPlugin.IsHost || index<0 || index!=BoundIndex || WorkWorld.ChangingNativeRole)return;
        Cancel();_autonomy=false;WorkWorld.ExternalChange();_plan.Reset(WorkWorld);
        Status="職務已由原生介面更改，同伴計畫及自主模式已停止。";
    }
    private void TickAutonomy()
    {
        if(!_autonomy || Busy || _plan.Owns || !WorkWorld.Safe || Time.unscaledTime<_nextAutoScan || Time.unscaledTime<_nextSend)return;
        _nextAutoScan=Time.unscaledTime+5f;
        var board=WorkWorld.Snapshot();
        board["open"]=GameData.Instance.isSupermarketOpen;
        board["role"]=_bound.taskPriority;
        string fingerprint=board.ToString(Newtonsoft.Json.Formatting.None);
        if(!_autoGate.TryBegin(Time.unscaledTime,fingerprint,true,_autoInterval.Value,_autoLimit.Value))return;
        StartRequest("AUTONOMOUS_TICK: Evaluate the latest work board under the enabled autonomous shop-work mode. Choose useful finite work for the bound employee, or briefly report that no change is needed. Use only inspect_shop, set_plan or assign_work. Do not follow, change memories, change autonomy or purchase anything.",true);
    }

    private bool ReadResult(Task<JObject> task, out JObject result)
    {
        result = null;
        if (task.IsCanceled) { Finish("請求逾時或已取消，可重試。" ); return false; }
        if (task.IsFaulted)
        {
            // Never log server bodies, headers, keys or chat content.
            var error = task.Exception?.GetBaseException();
            object code = error is HttpRequestException && error.Message.StartsWith("DeepSeek HTTP ") ? (object)new ModText.Message("（{0}）",error.Message) : "";
            Finish(new ModText.Message("API 連線失敗{0}；請檢查網址、模型、金鑰及網絡。已執行的結果會保留於對話。", code)); return false;
        }
        result = task.Result; return true;
    }

    private void Persist()
    {
        _transcriptCache = null;
        if (_memory == null || _memoryPath == null) return;
        try { _memory.Save(_memoryPath); _memoryWriteFailed = false; }
        catch (Exception) { _memoryWriteFailed = true; Status = "記憶寫入失敗；本次對話暫時只保留於記憶體。"; }
    }
    private void Finish(ModText.Message status)
    {
        if(_automaticRequest && !_automaticSucceeded)_autoGate.Failure(Time.unscaledTime);
        _automaticRequest=false;
        _context.Stop("Previous request ended. Only recorded tool results occurred; do not retry older instructions.");
        _status = status; _conversation = null; _request?.Dispose(); _request = null;
    }
    internal void Cancel()
    {
        _context.Stop("Previous request was cancelled. Only recorded tool results occurred; do not retry older instructions.");
        _generation++;
        _request?.Cancel(); _request?.Dispose(); _request = null;
        if (_conversation != null) _owner.StopCoroutine(_conversation);
        _conversation = null;
        _automaticRequest=false;
        Status = "已取消待處理回覆；已完成的派工保持有效。";
    }
    internal void Reset()
    {
        if (_saveName == null && !Busy) return;
        Cancel();_plan.Reset(WorkWorld);WorkWorld.Bind(null);WorkWorld.ClearTargets();
        StaffWorld.Reset();_riceBreak.Reset();_lastRiceEvent=null;
        _autonomy=false;_autoGate.Reset();_nextAutoScan=_nextWorkTick=_nextStaffTick=0;_lastPlanEvent=null;_lastPlanEventVersion=-1;
        _bound = null; _saveName = _memoryPath = null; _memory = null; _nextSend = 0;
        _context.Reset(); _usage = new CompanionUsageStats();
        _transcriptCache = null; _nextUiRefresh = 0;
        _memoryWriteFailed = false;
        _avatar.Clear();
        SettingsPanelUI.CloseCompanion();
    }
    internal void Dispose() { Reset(); _http.Dispose(); }
}

[HarmonyPatch(typeof(NPC_Manager), "UserCode_CmdDismissEmployeeData__Int32")]
internal static class CompanionDismissPatch
{
    private static void Prefix(int employeeIndex)
    {
        if (BetterSupermarketPlugin.IsHost) BetterSupermarketPlugin.Instance?.Companion?.Dismissed(employeeIndex);
    }
}
