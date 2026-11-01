using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace BetterSupermarketTogether;

[BepInPlugin("local.supermarkettogether.autorestock2230", "Better Supermarket Together", "1.2.3")]
[BepInProcess("Supermarket Together.exe")]
public class BetterSupermarketPlugin : BaseUnityPlugin
{
	public const string PluginGuid = "local.supermarkettogether.autorestock2230";

	public const string PluginName = "Better Supermarket Together";

	public const string PluginVersion = "1.2.3";

	internal static ManualLogSource Log;

	internal static BetterSupermarketPlugin Instance;

	internal static string PendingNotification;

	private static ConfigEntry<bool> _enable;

	private ConfigEntry<bool> _onlyHost;

	private ConfigEntry<bool> _notifications;

	private ConfigEntry<bool> _autoOpenOrderDepartment;

	private ConfigEntry<float> _orderAfterHour;

	private ConfigEntry<float> _stopBeforeHour;

	private ConfigEntry<float> _checkIntervalSeconds;

	private ConfigEntry<int> _zeroConfirmScans;

	private ConfigEntry<bool> _onlySeenProducts;

	private ConfigEntry<bool> _requireLowShelves;

	private ConfigEntry<int> _shelfThreshold;

	private ConfigEntry<bool> _includeProductionStock;

	private ConfigEntry<int> _boxesPerProduct;

	private ConfigEntry<int> _maxOrdersPerProductPerNight;

	private ConfigEntry<float> _reorderCooldownSeconds;

	private ConfigEntry<float> _fundsBuffer;

	private ConfigEntry<bool> _daytimeEnable;

	private ConfigEntry<float> _daytimeStartHour;

	private ConfigEntry<float> _daytimeEndHour;

	private ConfigEntry<int> _daytimeMaxOrdersPerProductPerDay;

	private ConfigEntry<float> _daytimeReorderCooldownSeconds;

	private ConfigEntry<bool> _daytimeRequireLowShelves;

	private ConfigEntry<int> _daytimeShelfThreshold;

	private ConfigEntry<bool> _daytimeOnlyWhenOpen;

	private ConfigEntry<bool> _pricingEnable;

	private ConfigEntry<float> _pricingUpdateHour;

	private ConfigEntry<int> _pricingPercentage;

	private ConfigEntry<bool> _pricingRoundDown;

	private ConfigEntry<bool> _promoEnable;

	private ConfigEntry<float> _promoHour;

	private ConfigEntry<int> _promoDiscount;

	private ConfigEntry<int> _promoMinStock;

	private ConfigEntry<bool> _promoUseSalesRate;

	private readonly Dictionary<int, int> _soldSnapYesterday = new Dictionary<int, int>();

	private readonly Dictionary<int, int> _soldSnapDayBefore = new Dictionary<int, int>();

	private static ConfigEntry<bool> _smartRestock;

	private ConfigEntry<bool> _i18nOverhaul;

	private ConfigEntry<bool> _showOverlay;

	private ConfigEntry<KeyboardShortcut> _overlayToggleKey;

	private ConfigEntry<KeyboardShortcut> _statusReportKey;

	private ConfigEntry<KeyboardShortcut> _panelKey;

	private ConfigEntry<KeyboardShortcut> _diagKey;

	private bool _loadAnnounced;

	private GUIStyle _overlayStyle;

	private Font _overlayFont;

	private static ConfigEntry<bool> _warehouseGroupPlacement;

	private ConfigEntry<bool> _warehouseConsolidate;

	private ConfigEntry<float> _warehouseConsolidateHour;

	private ConfigEntry<int> _warehouseConsolidateMoves;

	private ConfigEntry<bool> _productionAutoQueue;

    private ConfigEntry<bool> _productionOnlyShelfRecipes;

	private ConfigEntry<bool> _productionAutoTray;

	private static ConfigEntry<bool> _cleanerPatrolEnabled;

	private static ConfigEntry<float> _cleanerPatrolRadius;

	private ConfigEntry<int> _productionQueueThreshold;

	private ConfigEntry<string> _learnedCombinables;

	private ConfigEntry<string> _knownVariants;

	private ConfigEntry<bool> _gateOnIdleEmployee;

	private ConfigEntry<float> _minOrderIntervalSeconds;

	private ConfigEntry<int> _minTotalStockDefault;

	private ConfigEntry<int> _minTotalStockProduction;

	private ConfigEntry<int> _minTotalStockHygiene;

	private ConfigEntry<string> _groupMinStock;

	private ConfigEntry<int> _extraSlotsPurchased;

	private ConfigEntry<int> _extraSlotBaseCost;

	private bool _extraSlotsApplied;

	private ConfigEntry<bool> _autoRename;

	private ConfigEntry<string> _employeeNames;

	private static ConfigEntry<bool> _randomNameButton;

	private static ConfigEntry<string> _randomNames;

    private static ConfigEntry<EmployeeNameStyle> _randomNameStyle;

    private static ConfigEntry<string> _englishRandomNames;

    private static readonly ConfigEntry<string>[] ChineseGenderNames = new ConfigEntry<string>[3];
    private static readonly ConfigEntry<string>[] EnglishGenderNames = new ConfigEntry<string>[3];


    private const string DefaultEnglishNames = "Alex Carter,Emma Wilson,James Parker,Olivia Bennett,Daniel Brooks,Sophia Turner,Oliver Hayes,Amelia Foster,Noah Morgan,Charlotte Reed,Liam Cooper,Mia Collins,Ethan Walker,Evelyn Price,Lucas Bailey,Harper Mitchell,Henry Adams,Isabella Hughes,Jack Evans,Grace Harrison,Leo Thompson,Lily Anderson,Samuel Clark,Ella Roberts,Benjamin Scott,Chloe Richardson,William Green,Zoe Phillips,Thomas Baker,Lucy Campbell,Charlie Lewis,Hannah Edwards,Maxwell Miller,Alice Stewart,Oscar Taylor,Ruby Watson,Arthur James,Ivy Spencer,George Russell,Nora Hamilton";


	private bool _renamedThisSession;

	private bool _idleEmployeeExists;

	private int _consolidatedDay = -1;

	private float _nextProdTime;

	private float _nextUiCheck;

	private MethodInfo _getExistences;

	private bool _warnedReflection;

	private float _nextScanTime;

	private readonly Dictionary<int, int> _zeroStreak = new Dictionary<int, int>();

	private readonly Dictionary<int, float> _cooldownUntilNight = new Dictionary<int, float>();

	private readonly Dictionary<int, float> _cooldownUntilDaytime = new Dictionary<int, float>();

	private readonly Dictionary<int, int> _ordersThisNight = new Dictionary<int, int>();

	private readonly Dictionary<int, int> _ordersThisDaytime = new Dictionary<int, int>();

	private readonly HashSet<int> _seenProducts = new HashSet<int>();

	private int _nightDay = -1;

	private bool _ordering;

	private bool _wasStoreOpen;

	private int _pricingLastDay = -1;

	private int _pricingLastProductCount = -1;

	private int _promoLastDay = -1;

	internal string LastStaffPurchaseMessage = "";

	internal static bool EnablePlugin
	{
		get
		{
			if (_enable != null)
			{
				return _enable.Value;
			}
			return false;
		}
	}

	internal static bool SmartRestockEnabled
	{
		get
		{
			if (_smartRestock != null)
			{
				return _smartRestock.Value;
			}
			return false;
		}
	}

	internal static bool GroupPlacementEnabled
	{
		get
		{
			if (_warehouseGroupPlacement != null)
			{
				return _warehouseGroupPlacement.Value;
			}
			return false;
		}
	}

	internal static bool CleanerPatrolEnabled
	{
		get
		{
			if (_cleanerPatrolEnabled != null)
			{
				return _cleanerPatrolEnabled.Value;
			}
			return false;
		}
	}

	internal static bool RandomNameButtonEnabled
	{
		get
		{
			if (_randomNameButton != null)
			{
				return _randomNameButton.Value;
			}
			return false;
		}
	}

    internal static bool UsesEnglishRandomNames => _randomNameStyle?.Value == EmployeeNameStyle.English;

    internal string RandomNameStyleLabel => UsesEnglishRandomNames ? "英文 / English" : "中文 / Chinese";

    internal void ToggleRandomNameStyle()
    {
        _randomNameStyle.Value = UsesEnglishRandomNames ? EmployeeNameStyle.Chinese : EmployeeNameStyle.English;
    }

    internal static string[] GetRandomNamePool(EmployeeGender gender)
    {
        bool english = UsesEnglishRandomNames;
        var settings = english ? EnglishGenderNames : ChineseGenderNames;
        string configured = settings[(int)gender]?.Value;
        var names = ValidNames(configured);
        if (names.Count == 0) names = ValidNames(EmployeeNameCatalog.DefaultNames(gender, english));
        return names.ToArray();
    }

    private static List<string> ValidNames(string configured)
    {
        var names = new List<string>();
        var seen = new HashSet<string>();
        foreach (string entry in (configured ?? "").Split(','))
        {
            string name = entry.Trim();
            if (name.Length > 0 && name.Length <= 25 && !name.Contains("|") && seen.Add(name)) names.Add(name);
        }
        return names;
    }

	internal static float CleanerPatrolRadius
	{
		get
		{
			if (_cleanerPatrolRadius == null)
			{
				return 12f;
			}
			return _cleanerPatrolRadius.Value;
		}
	}

	internal int ExtraSlotCost => _extraSlotBaseCost.Value * (_extraSlotsPurchased.Value + 1);

	private void Awake()
	{
		Instance = this;
		Log = Logger;
		_enable = Config.Bind("General", "Enable", defaultValue: true, "啟用 Better Supermarket Together (true/false)");
		_onlyHost = Config.Bind("General", "OnlyHost", defaultValue: true, "多人模式時只在主機(host)上執行，避免多個玩家重複下單");
		_notifications = Config.Bind("General", "Notifications", defaultValue: true, "下單時在遊戲內顯示通知");
		_autoOpenOrderDepartment = Config.Bind("General", "AutoOpenOrderDepartment", defaultValue: true, "開業(超市開門)時自動開啟訂單部。遊戲內建的自動開啟按鈕不會存檔(每次讀檔重置)，此功能不依賴它。");
		_orderAfterHour = Config.Bind("Timing", "OrderAfterHour", 22.5f, new ConfigDescription("幾點後開始自動補貨 (22.5 = 22:30 遊戲時間)", new AcceptableValueRange<float>(0f, 24f)));
		_stopBeforeHour = Config.Bind("Timing", "StopBeforeHour", 23.9f, new ConfigDescription("幾點後停止下單 (避免跨日邊界，23.9 = 23:54)", new AcceptableValueRange<float>(0f, 24f)));
		_checkIntervalSeconds = Config.Bind("Safety", "CheckIntervalSeconds", 10f, new ConfigDescription("每隔幾秒(現實時間)檢查一次倉庫", new AcceptableValueRange<float>(2f, 120f)));
		_zeroConfirmScans = Config.Bind("Safety", "ZeroConfirmScans", 2, new ConfigDescription("倉庫+箱子連續幾次檢查都是 0 才判定為真的缺貨（防止 NPC 搬運瞬間的誤判）", new AcceptableValueRange<int>(1, 10)));
		_onlySeenProducts = Config.Bind("Safety", "OnlySeenProducts", defaultValue: true, "只自動補「當天有出現在任何地方(貨架/倉庫/箱子)」的產品，避免把已解鎖但沒在賣的產品也訂一遍");
		_requireLowShelves = Config.Bind("Safety", "RequireLowShelves", defaultValue: false, "開啟後：除了倉庫為 0，還要求貨架上該產品低於 ShelfThreshold 才下單");
		_shelfThreshold = Config.Bind("Safety", "ShelfThreshold", 10, new ConfigDescription("RequireLowShelves 開啟時使用的貨架存量門檻", new AcceptableValueRange<int>(0, 100000)));
		_includeProductionStock = Config.Bind("Safety", "IncludeProductionStock", defaultValue: true, "把生產部容器內的原料存量也納入缺貨判斷：生產部原料全部用完時，22:30 後會自動下單補原料（成品無法購買，只能生產）");
		_boxesPerProduct = Config.Bind("Order", "BoxesPerProduct", 1, new ConfigDescription("每個缺貨產品一次訂幾箱", new AcceptableValueRange<int>(1, 20)));
		_maxOrdersPerProductPerNight = Config.Bind("Order", "MaxOrdersPerProductPerNight", 1, new ConfigDescription("每個產品一晚最多自動下單幾次", new AcceptableValueRange<int>(1, 20)));
		_reorderCooldownSeconds = Config.Bind("Order", "ReorderCooldownSeconds", 300f, new ConfigDescription("同一產品兩次自動下單的最小間隔（現實秒）", new AcceptableValueRange<float>(30f, 3600f)));
		_fundsBuffer = Config.Bind("Order", "FundsBuffer", 0f, new ConfigDescription("下單後遊戲資金至少要剩下多少才會下單", new AcceptableValueRange<float>(0f, 1000000f)));
		_daytimeEnable = Config.Bind("Daytime", "Enable", defaultValue: true, "日間模式：到 StartHour 就開始自動補貨（預設不再要求已開業，早上開業前也會補貨）");
		_daytimeOnlyWhenOpen = Config.Bind("Daytime", "OnlyWhenOpen", defaultValue: false, "改回舊行為：設為 true 時，日間模式只在超市實際開門營業期間運作");
		_daytimeStartHour = Config.Bind("Daytime", "StartHour", 8f, new ConfigDescription("營業模式幾點後開始下單 (遊戲一天從 8:00 開始)", new AcceptableValueRange<float>(0f, 24f)));
		_daytimeEndHour = Config.Bind("Daytime", "EndHour", 22f, new ConfigDescription("營業模式幾點後停止 (遊戲 22:30 自動關店)", new AcceptableValueRange<float>(0f, 24f)));
		_daytimeMaxOrdersPerProductPerDay = Config.Bind("Daytime", "MaxOrdersPerProductPerDay", 2, new ConfigDescription("營業模式：每個產品一天最多自動下單幾次", new AcceptableValueRange<int>(1, 20)));
		_daytimeReorderCooldownSeconds = Config.Bind("Daytime", "ReorderCooldownSeconds", 600f, new ConfigDescription("營業模式：同一產品兩次自動下單的最小間隔（現實秒）", new AcceptableValueRange<float>(30f, 3600f)));
		_daytimeRequireLowShelves = Config.Bind("Daytime", "RequireLowShelves", defaultValue: false, "營業模式額外條件：貨架上該產品低於 DaytimeShelfThreshold 才下單（想省錢可開啟）");
		_daytimeShelfThreshold = Config.Bind("Daytime", "ShelfThreshold", 100, new ConfigDescription("營業模式 RequireLowShelves 使用的貨架存量門檻", new AcceptableValueRange<int>(0, 100000)));
		_pricingEnable = Config.Bind("Pricing", "Enable", defaultValue: true, "自動更新定價：每天定時把所有已解鎖產品的售價設為「成本價 × Percentage%」。遊戲每 7 天通脹一次(進貨成本上漲 5%~15%)，此功能會自動跟上调價。");
		_pricingUpdateHour = Config.Bind("Pricing", "UpdateHour", 8f, new ConfigDescription("每天幾點更新定價 (遊戲一天從 8:00 開始)", new AcceptableValueRange<float>(0f, 24f)));
		_pricingPercentage = Config.Bind("Pricing", "Percentage", 200, new ConfigDescription("售價 = 成本價 × (Percentage/100)。客人最高只接受成本的約 201%~250%(隨機)，200% = 不會被嫌貴的最高價", new AcceptableValueRange<int>(50, 250)));
		_pricingRoundDown = Config.Bind("Pricing", "RoundDown", defaultValue: false, "開啟後價格取到小數 1 位(如 $2.4)，關閉取到小數 2 位(如 $2.47)");
		_promoEnable = Config.Bind("Promo", "Enable", defaultValue: true, "自動促銷：每天定時把「總庫存最多」的產品放上促銷。遊戲的促銷每天結束時自動重置、無法手動取消，所以隔天會重新選。");
		_promoHour = Config.Bind("Promo", "Hour", 8f, new ConfigDescription("每天幾點掛促銷", new AcceptableValueRange<float>(0f, 24f)));
		_promoDiscount = Config.Bind("Promo", "Discount", 15, new ConfigDescription("促銷折扣 %(遊戲限制 5~45)", new AcceptableValueRange<int>(5, 45)));
		_promoMinStock = Config.Bind("Promo", "MinStock", 50, new ConfigDescription("總庫存(貨架+倉庫+箱子+生產部)至少多少才考慮促銷，避免把快賣完的產品打折", new AcceptableValueRange<int>(0, 100000)));
		_promoUseSalesRate = Config.Bind("Promo", "UseSalesRate", defaultValue: true, "進階促銷：用最近兩次評估的平均日銷量計算「庫存 ÷ (日銷+1)」抓滯銷品（庫存高＋賣得慢優先）；無銷量資料時自動退回純庫存模式。");
		_smartRestock = Config.Bind("Restock", "SmartRestock", defaultValue: true, "聰明補貨：修正 NPC 補貨員的任務分配 — 優先補「存貨比例最低」的貨架、優先搬「箱子內最少」的盒子(清出空間)、並且不會兩個 NPC 搶同一箱。出錯時自動退回原版行為。");
		_warehouseGroupPlacement = Config.Bind("Warehouse", "GroupPlacement", defaultValue: true, "入倉集中擺放：NPC 放箱入倉時，優先放進「已經有同產品」的倉庫架（就近者優先），不再塞進最近的任意空位散落各架。");
		_warehouseConsolidate = Config.Bind("Warehouse", "NightlyConsolidate", defaultValue: true, "夜間整理：打烊後自動把散落在其他架的同產品箱子搬回「該產品最多的那個架」（純資料搬移，走遊戲自己的同步路徑，實體外觀和標籤自動對帳）。");
		_warehouseConsolidateHour = Config.Bind("Warehouse", "ConsolidateHour", 23f, new ConfigDescription("幾點執行夜間整理 (預設 23:00，打烊後)", new AcceptableValueRange<float>(0f, 24f)));
		_warehouseConsolidateMoves = Config.Bind("Warehouse", "ConsolidateMovesPerNight", 30, new ConfigDescription("每晚最多整理幾個箱子（避免一次大搬風暴）", new AcceptableValueRange<int>(1, 200)));
		_i18nOverhaul = Config.Bind("Localization", "OverhaulChinese", defaultValue: true, "繁中本地化修正：以英文原文為準重新翻譯的繁中字串表，覆蓋遊戲內建的機翻（含烏克蘭文殘留）。僅在遊戲語言為中文時生效。");
		_productionAutoQueue = Config.Bind("Production", "AutoQueueProducts", defaultValue: true, "生產部成品存量達到 AutoQueueThreshold 門檻時，自動在工作台排程已知配方，使用生產員工生產。");
        _productionOnlyShelfRecipes = Config.Bind("Production", "OnlyShelfRecipes", true, "只自動補目前銷售貨架指定的配方（空貨架保留的配方也算）。舊箱子及歷史配方只計庫存，不會單獨觸發補產；false 則補所有歷史配方。");
		_learnedCombinables = Config.Bind("Production", "LearnedCombinables", "", "已學習的客製配方（自動維護，請勿手改）——讓自動排程照你用過的配方生產。");
		_knownVariants = Config.Bind("Production", "KnownVariants", "", "已知配方變體（自動維護，請勿手改）——每個成品變體的配方標籤，重啟後自動排程仍照配方做。");
		ProductionAutomation.LoadVariants = LoadLearnedVariants;
		ProductionAutomation.PersistVariants = SaveLearnedVariants;
		_productionAutoTray = Config.Bind("Production", "AutoFillTray", defaultValue: true, "自動從一般倉庫補入目前生產配方所需的基礎及客製原料。");
		_productionQueueThreshold = Config.Bind("Production", "AutoQueueThreshold", 0, new ConfigDescription("成品自動生產門檻：生產部側存貨（架子+工作台+地面箱）≤ 此值時自動排程生產一批（0 = 生產部清空才排）", new AcceptableValueRange<int>(0, 10000)));
		_cleanerPatrolEnabled = Config.Bind("Cleaner", "PatrolWhenIdle", defaultValue: true, "沒垃圾時機器人在店內巡邏，並每 0.33 秒動態搶最近的垃圾（原版會走回服務台待機）");
		_cleanerPatrolRadius = Config.Bind("Cleaner", "PatrolRadius", 12f, new ConfigDescription("巡邏半徑（公尺，以服務台為中心）", new AcceptableValueRange<float>(3f, 40f)));
		_gateOnIdleEmployee = Config.Bind("Restock", "GateOnIdleEmployee", defaultValue: true, "自動補貨前先看有沒有空閒的倉儲/補貨員工：沒有就先不下單（取代固定的下單冷卻）。");
		_minOrderIntervalSeconds = Config.Bind("Restock", "MinOrderIntervalSeconds", 30f, new ConfigDescription("啟用員工閘門時，同一產品兩次自動下單的最小間隔（秒）", new AcceptableValueRange<float>(10f, 600f)));
		_minTotalStockDefault = Config.Bind("Restock", "MinTotalStockDefault", 0, new ConfigDescription("一般產品的最低倉庫總量（倉庫+箱子+生產部）：低於此值自動購買（0 = 賣完才補）", new AcceptableValueRange<int>(0, 100000)));
		_minTotalStockProduction = Config.Bind("Restock", "MinTotalStockProduction", 100, new ConfigDescription("生產部原料的最低倉庫總量門檻（確保生產線不斍）", new AcceptableValueRange<int>(0, 100000)));
		_minTotalStockHygiene = Config.Bind("Restock", "MinTotalStockHygiene", 100, new ConfigDescription("紙巾/衛生/清潔類產品的最低倉庫總量門檻", new AcceptableValueRange<int>(0, 100000)));
		_groupMinStock = Config.Bind("Restock", "GroupMinStock", "4:100,5:100", "分類最低庫存覆寫（格式 分類:數量,分類:數量；分類編號見 productGroup0-18，4=衛生 5=清潔）。未列出的分類使用 MinTotalStockDefault。設定面板第 2 頁可調。");
		_extraSlotsPurchased = Config.Bind("Staff", "ExtraSlotsPurchased", 0, new ConfigDescription("已用金錢解鎖的額外員工上限數（自動維護，請勿手改）", new AcceptableValueRange<int>(0, 100)));
		_extraSlotBaseCost = Config.Bind("Staff", "ExtraSlotBaseCost", 5000, new ConfigDescription("第 1 個額外員工上限的價格；之後每個遞增一倍基底（5000、10000、15000……）", new AcceptableValueRange<int>(100, 100000)));
		_autoRename = Config.Bind("Naming", "AutoRenameEmployees", defaultValue: true, "載入存檔時，把所有員工 NPC 依「EmployeeNames」清單依序命名。");
		_employeeNames = Config.Bind("Naming", "EmployeeNames", "小明,小華,小美,阿傑,小玲,大偉,小婷,阿強,小龍,阿珍,小玉,阿豪", "員工名稱清單（逗號分隔）；員工數超過清單時會加編號循環。");
		_randomNameButton = Config.Bind("Naming", "RandomNameButton", defaultValue: true, "員工頁面每個員工名字旁顯示「改名」按鈕（隨機取名）＋「全部隨機改名」按鈕。");
		_randomNames = Config.Bind("Naming", "RandomNames", "陳大明,林秀英,黃志豪,張雅婷,李文彬,王美玲,吳俊賢,劉怡芳,蔡明華,楊淑芬,謝金龍,何雅琴,高志偉,賴淑惠,潘文傑,姜美鳳,洪建宏,曾雅惠,曹志明,盧小燕,柯文謙,施雅婷,廖寶珠,鄧小鳳,許文龍,沈春梅,蘇麗華,唐國強,韓雪梅,鍾明軒,白雪娥,江文雄,方雅玲,馬俊賢,袁美鳳,宋小龍,范雅芬,徐志豪,柳淑芬,鄭文謙", "舊版混合中文名單（保留資料）；性別對應改用 MaleChineseNames、FemaleChineseNames、NeutralChineseNames。");
        _randomNameStyle = Config.Bind("Naming", "RandomNameStyle", EmployeeNameStyle.Chinese, "隨機員工名字風格：Chinese（中文）或 English（英文）。可在 F10 → 員工改名切換。");
        _englishRandomNames = Config.Bind("Naming", "EnglishRandomNames", DefaultEnglishNames, "舊版混合英文名單（保留資料）；性別對應改用 MaleEnglishNames、FemaleEnglishNames、NeutralEnglishNames。");
        ChineseGenderNames[(int)EmployeeGender.Male] = Config.Bind("Naming", "MaleChineseNames", EmployeeNameCatalog.MaleChinese, "男性 NPC 的中文隨機名字（逗號分隔，最多 25 字）。");
        ChineseGenderNames[(int)EmployeeGender.Female] = Config.Bind("Naming", "FemaleChineseNames", EmployeeNameCatalog.FemaleChinese, "女性 NPC 的中文隨機名字（逗號分隔，最多 25 字）。");
        ChineseGenderNames[(int)EmployeeGender.Unknown] = Config.Bind("Naming", "NeutralChineseNames", EmployeeNameCatalog.NeutralChinese, "無法辨識 NPC 性別時使用的中性中文名字。");
        EnglishGenderNames[(int)EmployeeGender.Male] = Config.Bind("Naming", "MaleEnglishNames", EmployeeNameCatalog.MaleEnglish, "男性 NPC 的英文隨機名字（逗號分隔，最多 25 字）。");
        EnglishGenderNames[(int)EmployeeGender.Female] = Config.Bind("Naming", "FemaleEnglishNames", EmployeeNameCatalog.FemaleEnglish, "女性 NPC 的英文隨機名字（逗號分隔，最多 25 字）。");
        EnglishGenderNames[(int)EmployeeGender.Unknown] = Config.Bind("Naming", "NeutralEnglishNames", EmployeeNameCatalog.NeutralEnglish, "無法辨識 NPC 性別時使用的中性英文名字。");

		_showOverlay = Config.Bind("Overlay", "ShowStatusOverlay", defaultValue: true, "在畫面左上角常駐顯示插件狀態列（F8 可隨時開關）");
		_overlayToggleKey = Config.Bind("Overlay", "ToggleKey", new KeyboardShortcut(KeyCode.F8), "開關狀態列的快速鍵");
		_statusReportKey = Config.Bind("Overlay", "StatusReportKey", new KeyboardShortcut(KeyCode.F9), "在遊戲內彈出完整狀態報告的快速鍵");
		_panelKey = Config.Bind("Overlay", "SettingsPanelKey", new KeyboardShortcut(KeyCode.F10), "開關插件設定面板的快速鍵（看板按鈕列也有「插件設定」）");
		_diagKey = Config.Bind("Overlay", "DiagnosticsKey", new KeyboardShortcut(KeyCode.F11), "F11：把遊戲內真實狀態（員工資料/上限/配方變體/庫存）倒進 LogOutput.log 供診斷");
		new Harmony("local.supermarkettogether.autorestock2230").PatchAll();
		try
		{
			string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".", "BetterSupermarketTogether_i18n_zhTW.tsv");
			LocalizationOverride.LoadFromFile(_i18nOverhaul, path);
		}
		catch (Exception ex)
		{
			Log.LogWarning("i18n load failed: " + ex.Message);
		}
		Log.LogInfo(string.Format("{0} {1} loaded. Night window {2:0.#}-{3:0.#}, daytime mode {4} (store open hours).", "Better Supermarket Together", "1.2.3", _orderAfterHour.Value, _stopBeforeHour.Value, _daytimeEnable.Value ? "ON" : "OFF"));
	}

	private void Update()
	{
		bool flag = GameData.Instance != null;
		if (flag && !_loadAnnounced)
		{
			_loadAnnounced = true;
			if (_notifications.Value)
			{
				PendingNotification = "`Better Supermarket Together v1.2.3 已載入（F8 狀態列、F9 詳細狀態）";
			}
		}
		if (!flag)
		{
			_loadAnnounced = false;
			_renamedThisSession = false;
			_extraSlotsApplied = false;
			StaffCapacity.Reset();
			ProductionAutomation.Reset();
			SettingsPanelUI.ResetForSceneChange();
		}
		else if (!_extraSlotsApplied)
		{
			NPC_Manager instance = NPC_Manager.Instance;
			if (instance != null && instance.employeeParentOBJ != null && instance.isServer && instance.initialEmployeesSpawnIsFinished)
			{
				_extraSlotsApplied = true;
				try
				{
					ApplyExtraEmployeeSlots(instance);
				}
				catch (Exception ex)
				{
					Log.LogWarning("Extra slot apply failed: " + ex.Message);
				}
			}
		}
		else if (_autoRename.Value && !_renamedThisSession)
		{
			NPC_Manager instance2 = NPC_Manager.Instance;
			if (instance2 != null && instance2.employeeParentOBJ != null && instance2.employeeParentOBJ.transform.childCount > 0)
			{
				_renamedThisSession = true;
				try
				{
					ApplyEmployeeNames(instance2);
				}
				catch (Exception ex2)
				{
					Log.LogWarning("Employee rename failed: " + ex2.Message);
				}
			}
		}
		if (flag && Time.unscaledTime >= _nextUiCheck)
		{
			_nextUiCheck = Time.unscaledTime + 2f;
			ApplyExtraEmployeeSlots(NPC_Manager.Instance);
			SettingsPanelUI.EnsureBoardButton();
		}
		if (_panelKey.Value.IsDown())
		{
			SettingsPanelUI.TogglePanel();
		}
		if (_diagKey.Value.IsDown() & flag)
		{
			DumpDiagnostics(GameData.Instance);
		}
		if (_overlayToggleKey.Value.IsDown())
		{
			_showOverlay.Value = !_showOverlay.Value;
		}
		if (_statusReportKey.Value.IsDown() & flag)
		{
			PendingNotification = "`" + BuildStatusText().Replace("\n", "  |  ");
		}
		if (!_enable.Value)
		{
			return;
		}
		GameData instance3 = GameData.Instance;
		if (instance3 == null || (_onlyHost.Value && !instance3.isServer))
		{
			return;
		}
		int gameDay = instance3.gameDay;
		if (gameDay != _nightDay)
		{
			_nightDay = gameDay;
			_ordersThisNight.Clear();
			_ordersThisDaytime.Clear();
			_zeroStreak.Clear();
			_seenProducts.Clear();
		}
		bool isSupermarketOpen = instance3.isSupermarketOpen;
		if ((_autoOpenOrderDepartment.Value & isSupermarketOpen) && !_wasStoreOpen)
		{
			OrderPackaging instance4 = OrderPackaging.Instance;
			UpgradesManager component = instance3.GetComponent<UpgradesManager>();
			if (instance4 != null && component != null && component.addonsBought != null && component.addonsBought.Length != 0 && component.addonsBought[0] && !instance4.isOrderDepartmentActivated)
			{
				instance4.CmdActivateOrderDepartment();
				Log.LogInfo("Store opened -> order department activated automatically.");
				if (_notifications.Value)
				{
					PendingNotification = "`Better Supermarket: order department opened";
				}
			}
		}
		_wasStoreOpen = isSupermarketOpen;
		if (_pricingEnable.Value)
		{
			bool num = _pricingLastDay != instance3.gameDay && instance3.timeOfDay >= _pricingUpdateHour.Value;
			bool flag2 = false;
			ProductListing instance5 = ProductListing.Instance;
			if (_pricingLastProductCount != -1 && instance5 != null && instance5.availableProducts != null && instance5.availableProducts.Count != _pricingLastProductCount)
			{
				flag2 = true;
			}
			if ((num | flag2) && RunAutoPricing(instance3))
			{
				_pricingLastDay = instance3.gameDay;
				_pricingLastProductCount = ProductListing.Instance.availableProducts.Count;
			}
		}
		if (_promoEnable.Value && !_ordering && instance3.timeOfDay >= _promoHour.Value && _promoLastDay != instance3.gameDay && TryAutoPromote(instance3))
		{
			_promoLastDay = instance3.gameDay;
		}
		if ((_productionAutoQueue.Value || _productionAutoTray.Value) && Time.unscaledTime >= _nextProdTime)
		{
			NPC_Manager instance6 = NPC_Manager.Instance;
			ManufacturingBase instance7 = ManufacturingBase.Instance;
			if (instance6 != null && instance7 != null)
			{
				_nextProdTime = Time.unscaledTime + Mathf.Max(2f, _checkIntervalSeconds.Value);
				try
				{
					ProductionAutomation.QueueThreshold = _productionQueueThreshold.Value;
                    ProductionAutomation.OnlyShelfRecipes = _productionOnlyShelfRecipes.Value;
					ProductionAutomation.Run(instance6, instance3.GetComponent<ManagerBlackboard>(), _productionAutoQueue.Value ? 2 : 0, _productionAutoTray.Value ? 3 : 0, _notifications.Value);
				}
				catch (Exception ex3)
				{
					Log.LogWarning("Production automation failed: " + ex3.Message);
				}
			}
		}
		if (_warehouseConsolidate.Value && !_ordering && !instance3.isSupermarketOpen && instance3.timeOfDay >= _warehouseConsolidateHour.Value && instance3.timeOfDay <= 23.9f && _consolidatedDay != instance3.gameDay)
		{
			NPC_Manager instance8 = NPC_Manager.Instance;
			if (instance8 != null)
			{
				_consolidatedDay = instance3.gameDay;
				try
				{
					WarehouseConsolidator.Run(instance8, _warehouseConsolidateMoves.Value);
				}
				catch (Exception ex4)
				{
					Log.LogWarning("Warehouse consolidation failed: " + ex4);
				}
			}
		}
		if (!_ordering && !(Time.unscaledTime < _nextScanTime))
		{
			_nextScanTime = Time.unscaledTime + Mathf.Max(2f, _checkIntervalSeconds.Value);
			int num2 = 0;
			if (instance3.timeOfDay >= _orderAfterHour.Value && instance3.timeOfDay <= _stopBeforeHour.Value)
			{
				num2 = 1;
			}
			else if (_daytimeEnable.Value && (!_daytimeOnlyWhenOpen.Value || instance3.isSupermarketOpen) && instance3.timeOfDay >= _daytimeStartHour.Value && instance3.timeOfDay <= _daytimeEndHour.Value)
			{
				num2 = 2;
			}
			if (num2 == 0)
			{
				_zeroStreak.Clear();
			}
			Scan(instance3, num2);
		}
	}

	private void Scan(GameData gd, int orderMode)
	{
		ProductListing instance = ProductListing.Instance;
		if (instance == null || instance.productsData == null || instance.availableProducts == null)
		{
			return;
		}
		ManagerBlackboard component = gd.GetComponent<ManagerBlackboard>();
		if (component == null)
		{
			return;
		}
		if (_getExistences == null)
		{
			_getExistences = AccessTools.Method(typeof(ManagerBlackboard), "GetProductsExistences");
			if (_getExistences == null)
			{
				if (!_warnedReflection)
				{
					Log.LogWarning("ManagerBlackboard.GetProductsExistences not found - game version may be incompatible.");
					_warnedReflection = true;
				}
				return;
			}
		}
		Dictionary<int, int> dictionary = null;
		if (_includeProductionStock.Value)
		{
			dictionary = new Dictionary<int, int>();
			ManufacturingContainer[] array = UnityEngine.Object.FindObjectsByType<ManufacturingContainer>(FindObjectsSortMode.None);
			foreach (ManufacturingContainer manufacturingContainer in array)
			{
				if (manufacturingContainer == null)
				{
					continue;
				}
				int[] productInfoArray = manufacturingContainer.productInfoArray;
				if (productInfoArray == null)
				{
					continue;
				}
				for (int j = 0; j + 1 < productInfoArray.Length; j += 2)
				{
					int num = productInfoArray[j];
					int num2 = productInfoArray[j + 1];
					if (num >= 0 && num2 > 0)
					{
						dictionary.TryGetValue(num, out var value);
						dictionary[num] = value + num2;
					}
				}
			}
		}
		List<int> list = null;
		_idleEmployeeExists = HasIdleStorageEmployee(NPC_Manager.Instance);
		foreach (int item in new List<int>(instance.availableProducts))
		{
			if (item < 0 || item >= instance.productsData.Length || instance.productsData[item] == null)
			{
				continue;
			}
			int[] array2;
			try
			{
				array2 = (int[])_getExistences.Invoke(component, new object[1] { item });
			}
			catch (Exception ex)
			{
				if (!_warnedReflection)
				{
					Log.LogWarning("GetProductsExistences invoke failed: " + ex.Message);
					_warnedReflection = true;
				}
				return;
			}
			if (array2 == null || array2.Length < 3)
			{
				continue;
			}
			int num3 = array2[0];
			int num4 = array2[1];
			int num5 = array2[2];
			int value2 = 0;
			dictionary?.TryGetValue(item, out value2);
			if (num3 > 0 || num4 > 0 || num5 > 0 || value2 > 0)
			{
				_seenProducts.Add(item);
			}
			if (orderMode == 0)
			{
				continue;
			}
			int num6 = MinStockFor(item, instance);
			int num7 = num4 + num5 + value2;
			if (!((num6 <= 0) ? (num7 == 0) : (num7 < num6)))
			{
				_zeroStreak.Remove(item);
				continue;
			}
			bool flag = ((orderMode == 1) ? _requireLowShelves.Value : _daytimeRequireLowShelves.Value);
			int num8 = ((orderMode == 1) ? _shelfThreshold.Value : _daytimeShelfThreshold.Value);
			if (flag && num3 > num8)
			{
				_zeroStreak.Remove(item);
			}
			else
			{
				if (_onlySeenProducts.Value && !_seenProducts.Contains(item))
				{
					continue;
				}
				_zeroStreak.TryGetValue(item, out var value3);
				value3++;
				_zeroStreak[item] = value3;
				if (value3 < Mathf.Max(1, _zeroConfirmScans.Value))
				{
					continue;
				}
				Dictionary<int, int> dictionary2 = ((orderMode == 1) ? _ordersThisNight : _ordersThisDaytime);
				int num9 = ((orderMode == 1) ? _maxOrdersPerProductPerNight.Value : _daytimeMaxOrdersPerProductPerDay.Value);
				if ((dictionary2.TryGetValue(item, out var value4) && value4 >= num9) || (_gateOnIdleEmployee.Value && !_idleEmployeeExists))
				{
					continue;
				}
				((orderMode == 1) ? _cooldownUntilNight : _cooldownUntilDaytime).TryGetValue(item, out var value5);
				if (!(Time.unscaledTime < value5))
				{
					if (list == null)
					{
						list = new List<int>();
					}
					list.Add(item);
				}
			}
		}
		if (list != null && list.Count > 0)
		{
			_ordering = true;
			StartCoroutine(OrderRoutine(gd, component, instance, list, orderMode));
		}
	}

	internal Dictionary<int, int> ParseGroupMinStock()
	{
		Dictionary<int, int> dictionary = new Dictionary<int, int>();
		string[] array = _groupMinStock.Value.Split(new char[1] { ',' });
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split(new char[1] { ':' });
			if (array2.Length == 2 && int.TryParse(array2[0].Trim(), out var result) && int.TryParse(array2[1].Trim(), out var result2))
			{
				dictionary[result] = result2;
			}
		}
		return dictionary;
	}

	internal void SetGroupMinStock(int group, int value)
	{
		Dictionary<int, int> dictionary = ParseGroupMinStock();
		dictionary[group] = Mathf.Max(0, value);
		List<string> list = new List<string>();
		foreach (KeyValuePair<int, int> item in dictionary)
		{
			list.Add(item.Key + ":" + item.Value);
		}
		_groupMinStock.Value = string.Join(",", list);
	}

	internal int GroupMinStockEffective(int group)
	{
		if (!ParseGroupMinStock().TryGetValue(group, out var value))
		{
			return _minTotalStockDefault.Value;
		}
		return value;
	}

	internal void PurchaseEmployeeSlotFromUi()
	{
		if (TryPurchaseEmployeeSlot(out var message))
		{
			LastStaffPurchaseMessage = "已購買：" + message;
			if (_notifications.Value)
			{
				PendingNotification = "`" + message;
			}
		}
		else
		{
			LastStaffPurchaseMessage = message + "（僱用上限 +1）";
			if (_notifications.Value)
			{
				PendingNotification = "`" + message;
			}
		}
	}

	internal bool TryPurchaseEmployeeSlot(out string message)
	{
		GameData instance = GameData.Instance;
		NPC_Manager instance2 = NPC_Manager.Instance;
		if (instance == null || instance2 == null)
		{
			message = "未進入遊戲";
			return false;
		}
		if (!instance.isServer)
		{
			message = "僅主機可購買";
			return false;
		}
		if (!instance2.initialEmployeesSpawnIsFinished)
        {
            message = "員工資料仍在載入，請稍後再試";
            return false;
        }
        ApplyExtraEmployeeSlots(instance2);
        if (_extraSlotsPurchased.Value >= 100) { message = "已達額外名額上限"; return false; }
        int extraSlotCost = ExtraSlotCost;
		if (instance.gameFunds < (float)extraSlotCost)
		{
			message = "資金不足（需 $" + extraSlotCost + "）";
			return false;
		}
		instance.CmdAlterFundsWithoutExperience(-extraSlotCost);
		_extraSlotsPurchased.Value++;
		ApplyExtraEmployeeSlots(instance2);
		message = "員工上限 +1（花費 $" + extraSlotCost + "）";
		Log.LogInfo("Staff: extra employee slot purchased for $" + extraSlotCost + " (total extra: " + _extraSlotsPurchased.Value + ")");
		return true;
	}

	private void ApplyExtraEmployeeSlots(NPC_Manager npc)
    {
        if (_enable.Value) StaffCapacity.Apply(npc, _extraSlotsPurchased.Value);
    }

	private int MinStockFor(int productId, ProductListing pl)
	{
		int num = _minTotalStockDefault.Value;
		if (_minTotalStockProduction.Value > num && ProductionAutomation.IsProductionIngredient(productId))
		{
			num = _minTotalStockProduction.Value;
		}
		if (pl != null && pl.productsData != null && productId >= 0 && productId < pl.productsData.Length && pl.productsData[productId] != null && pl.productGroups != null && pl.productsData[productId].productTier >= 0 && pl.productsData[productId].productTier < pl.productGroups.Length)
		{
			int key = pl.productGroups[pl.productsData[productId].productTier];
			if (ParseGroupMinStock().TryGetValue(key, out var value) && value > num)
			{
				num = value;
			}
		}
		if (_minTotalStockHygiene.Value > num && IsHygieneOrCleaning(productId, pl))
		{
			num = _minTotalStockHygiene.Value;
		}
		return num;
	}

	private static bool IsHygieneOrCleaning(int productId, ProductListing pl)
	{
		if (pl == null || pl.productsData == null || productId < 0 || productId >= pl.productsData.Length)
		{
			return false;
		}
		ProductListing.ProductData productData = pl.productsData[productId];
		if (productData == null)
		{
			return false;
		}
		if (pl.productGroups == null || productData.productTier < 0 || productData.productTier >= pl.productGroups.Length)
		{
			return false;
		}
		int num = pl.productGroups[productData.productTier];
		if (num != 4)
		{
			return num == 5;
		}
		return true;
	}

	private static bool HasIdleStorageEmployee(NPC_Manager npc)
	{
		if (npc == null || npc.employeeParentOBJ == null)
		{
			return false;
		}
		Transform transform = npc.employeeParentOBJ.transform;
		for (int i = 0; i < transform.childCount; i++)
		{
			NPC_Info component = transform.GetChild(i).GetComponent<NPC_Info>();
			if (!(component == null) && (component.taskPriority == 2 || component.taskPriority == 3) && component.boxNumberOfProducts == 0)
			{
				bool flag = false;
				if (component.taskPriority == 2 && component.productAvailableArray != null && component.productAvailableArray.Length >= 1 && component.productAvailableArray[0] != -1)
				{
					flag = true;
				}
				if (component.taskPriority == 3 && (component.randomBox != null || component.currentFreeStorageIndex >= 0))
				{
					flag = true;
				}
				if (!flag && component.state <= 1)
				{
					return true;
				}
			}
		}
		return false;
	}

	internal void KeepChosenEmployeeNames()
    {
        _autoRename.Value = false;
    }

    private void ApplyEmployeeNames(NPC_Manager npc)
    {
        if (npc.hiredEmployeesData == null || npc.employeesArray == null) return;
        string[] names = _employeeNames.Value.Split(',');
        if (names.Length == 0 || string.IsNullOrWhiteSpace(names[0])) return;
        int count = 0;
        for (int index = 0; index < npc.hiredEmployeesData.Length; index++)
        {
            if (string.IsNullOrWhiteSpace(npc.hiredEmployeesData[index])) continue;
            string name = names[count % names.Length].Trim();
            if (count >= names.Length) name += (count / names.Length + 1);
            EmployeeNaming.Rename(npc,index,name,false);
            count++;
        }
        Log.LogInfo($"Assigned employee list names by hiring index: {count}.");
    }
	private void DumpDiagnostics(GameData gd)
	{
		try
		{
			Log.LogInfo("==== AR2230 DIAGNOSTICS START ====");
			Log.LogInfo($"day={gd.gameDay} time={gd.timeOfDay:0.00} funds={gd.gameFunds:0} open={gd.isSupermarketOpen} isServer={gd.isServer}");
			Log.LogInfo($"maxEmployees={NPC_Manager.Instance.maxEmployees} hired={NPC_Manager.Instance.numberOfHiredEmployees} extraSlots={_extraSlotsPurchased.Value} applied={_extraSlotsApplied}");
			NPC_Manager instance = NPC_Manager.Instance;
			if (instance != null && instance.employeeParentOBJ != null)
			{
				Transform transform = instance.employeeParentOBJ.transform;
				Log.LogInfo($"employee NPCs: {transform.childCount}");
				for (int i = 0; i < transform.childCount; i++)
				{
					NPC_Info component = transform.GetChild(i).GetComponent<NPC_Info>();
					if (!(component == null))
					{
						string text = ((component.NPCID >= 0 && component.NPCID < instance.hiredEmployeesData.Length) ? instance.hiredEmployeesData[component.NPCID] : "<NPCID out of range>");
						if (text.Length > 80)
						{
							text = text.Substring(0, 80);
						}
						Log.LogInfo($"  NPC[{i}] NPCID={component.NPCID} name=\"{component.NetworkNPCName}\" prio={component.taskPriority} state={component.state} carrying={component.boxNumberOfProducts} data={text}");
					}
				}
				Log.LogInfo($"hiredEmployeesData entries: {instance.hiredEmployeesData.Length}");
				for (int j = 0; j < instance.hiredEmployeesData.Length; j++)
				{
					string text2 = instance.hiredEmployeesData[j];
					if (string.IsNullOrWhiteSpace(text2))
					{
						Log.LogInfo($"  data[{j}] = <empty>");
						continue;
					}
					string[] array = text2.Split(new char[1] { '|' });
					string arg = ((array.Length > 9) ? array[9] : "?");
					Log.LogInfo($"  data[{j}] name=\"{arg}\" len={text2.Length}");
				}
			}
			ProductionAutomation.LoadPersistedVariants();
			Log.LogInfo($"KnownVariants: {ProductionAutomation.KnownVariants.Count} | threshold={ProductionAutomation.QueueThreshold}");
			if (ManufacturingBase.Instance != null && instance.manufacturingStorageShelvesOBJ != null)
			{
				Dictionary<string, int> dictionary = new Dictionary<string, int>();
				Transform[] array2 = new Transform[2]
				{
					instance.manufacturingStorageShelvesOBJ.transform,
					instance.manufacturingShelvesOBJ.transform
				};
				foreach (Transform transform2 in array2)
				{
					for (int l = 0; l < transform2.childCount; l++)
					{
						ManufacturingContainer component2 = transform2.GetChild(l).GetComponent<ManufacturingContainer>();
						if (component2 == null || component2.productInfoArray == null || component2.combinableInfoArray == null)
						{
							continue;
						}
						for (int m = 0; m + 1 < component2.productInfoArray.Length; m += 2)
						{
							int num = component2.productInfoArray[m];
							int num2 = component2.productInfoArray[m + 1];
							if (num >= 0 && num2 > 0)
							{
								string text3 = ((m / 2 < component2.combinableInfoArray.Length) ? component2.combinableInfoArray[m / 2] : "");
								string key = num + "|" + text3;
								dictionary[key] = (dictionary.TryGetValue(key, out var value) ? (value + num2) : num2);
							}
						}
					}
				}
				foreach (KeyValuePair<string, int> item in dictionary)
				{
					Log.LogInfo($"  PROD STOCK {item.Key} = {item.Value}");
				}
				if (dictionary.Count == 0)
				{
					Log.LogInfo("  PROD STOCK: (production area empty)");
				}
			}
			Log.LogInfo("==== AR2230 DIAGNOSTICS END ====");
		}
		catch (Exception ex)
		{
			Log.LogError("Diagnostics failed: " + ex);
		}
	}

	private string BuildStatusText()
	{
		string text = (LocalizationOverride.Loaded ? (LocalizationOverride.Table.Count + " 條") : "未載入");
		string text2 = "Better Supermarket Together v1.2.3 ｜ 繁中覆蓋：" + text;
		GameData instance = GameData.Instance;
		if (!_enable.Value)
		{
			return text2 + " ｜ ⚠插件已停用";
		}
		if (instance == null)
		{
			return text2 + " ｜（主選單）";
		}
		string text3 = "補貨：待機";
		if (instance.timeOfDay >= _orderAfterHour.Value && instance.timeOfDay <= _stopBeforeHour.Value)
		{
			text3 = "補貨：夜間窗口";
		}
		else if (_daytimeEnable.Value && (!_daytimeOnlyWhenOpen.Value || instance.isSupermarketOpen) && instance.timeOfDay >= _daytimeStartHour.Value && instance.timeOfDay <= _daytimeEndHour.Value)
		{
			text3 = "補貨：日間窗口";
		}
		string text4 = text3 + " ｜ 定價：" + (_pricingEnable.Value ? (_pricingPercentage.Value + "%") : "關") + " ｜ 促銷：" + (_promoEnable.Value ? (_promoDiscount.Value + "%") : "關") + " ｜ 倉庫整理：" + (_warehouseConsolidate.Value ? "開" : "關") + " ｜ 聰明補貨：" + (SmartRestockEnabled ? "開" : "關");
		if (_onlyHost.Value && !instance.isServer)
		{
			text4 += " ｜ ⚠非主機（功能待機）";
		}
		return text2 + "\n" + text4;
	}

	private void OnGUI()
	{
		if (_showOverlay.Value && !(GameData.Instance == null))
		{
			if (_overlayStyle == null)
			{
				_overlayFont = Font.CreateDynamicFontFromOSFont(new string[7] { "Microsoft JhengHei", "Microsoft YaHei", "SimHei", "DFKai-SB", "PingFang TC", "Noto Sans CJK TC", "Segoe UI" }, 13);
				_overlayStyle = new GUIStyle(GUI.skin.label);
				_overlayStyle.font = _overlayFont;
				_overlayStyle.fontSize = 13;
				_overlayStyle.fontStyle = FontStyle.Bold;
				_overlayStyle.normal.textColor = Color.white;
			}
			string text = BuildStatusText();
			Vector2 vector = _overlayStyle.CalcSize(new GUIContent(text));
			GUI.Box(new Rect(6f, 6f, vector.x + 16f, vector.y + 10f), GUIContent.none);
			GUI.Label(new Rect(14f, 11f, vector.x, vector.y), text, _overlayStyle);
		}
	}

	internal void CollectBoolSettings(List<(string, ConfigEntry<bool>)> list)
	{
		list.Add(("總開關（全部功能）", _enable));
		list.Add(("多人時僅主機運作", _onlyHost));
		list.Add(("遊戲內通知", _notifications));
		list.Add(("F8 狀態列", _showOverlay));
		list.Add(("日間補貨模式", _daytimeEnable));
		list.Add(("日間補貨僅限開業中", _daytimeOnlyWhenOpen));
		list.Add(("自動定價", _pricingEnable));
		list.Add(("定價捨去零頭", _pricingRoundDown));
		list.Add(("自動促銷", _promoEnable));
		list.Add(("促銷用銷速抓滯銷", _promoUseSalesRate));
		list.Add(("生產部：成品自動排程", _productionAutoQueue));
		list.Add(("生產部：托盤自動進料", _productionAutoTray));
		list.Add(("倉庫：入倉集中擺放", _warehouseGroupPlacement));
		list.Add(("倉庫：夜間整理", _warehouseConsolidate));
		list.Add(("聰明補貨（NPC 派工修正）", _smartRestock));
		list.Add(("繁中覆蓋（改表後需重啟）", _i18nOverhaul));
	}

	internal Dictionary<string, string> LoadLearnedVariants()
    {
        return RepairPolicies.ReadVariants(_knownVariants.Value, _learnedCombinables.Value);
    }

	internal void SaveLearnedVariants(Dictionary<string, string> variants)
	{
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, string> variant in variants)
		{
			list.Add(variant.Key);
		}
		_knownVariants.Value = string.Join(";", list);
	}

	internal void CollectGroupMinStockSettings(List<(string, Func<float>, Action<float>, float)> list)
	{
		for (int i = 0; i < 19; i++)
		{
			int group = i;
			string item = "分類 " + group;
			try
			{
				if (LocalizationManager.instance != null)
				{
					string localizationString = LocalizationManager.instance.GetLocalizationString("productGroup" + group);
					if (!string.IsNullOrEmpty(localizationString) && localizationString != "LocError")
					{
						item = localizationString;
					}
				}
			}
			catch
			{
			}
			list.Add((item, () => GroupMinStockEffective(group), (float v) =>
			{
				SetGroupMinStock(group, Mathf.RoundToInt(v));
			}, 10f));
		}

        list.Add(("生產原料最低庫存", () => _minTotalStockProduction.Value, (float v) =>
        {
            _minTotalStockProduction.Value = Mathf.RoundToInt(v);
        }, 10f));
	}

	internal void CollectFloatSettings(List<(string, Func<float>, Action<float>, float)> list)
	{
		list.Add(("日間補貨開始（時）", () => _daytimeStartHour.Value, (float v) =>
		{
			_daytimeStartHour.Value = v;
		}, 0.5f));
		list.Add(("日間補貨結束（時）", () => _daytimeEndHour.Value, (float v) =>
		{
			_daytimeEndHour.Value = v;
		}, 0.5f));
		list.Add(("夜間補貨開始（時）", () => _orderAfterHour.Value, (float v) =>
		{
			_orderAfterHour.Value = v;
		}, 0.5f));
		list.Add(("夜間補貨結束（時）", () => _stopBeforeHour.Value, (float v) =>
		{
			_stopBeforeHour.Value = v;
		}, 0.5f));
		list.Add(("定價：成本百分比（%）", () => _pricingPercentage.Value, (float v) =>
		{
			_pricingPercentage.Value = Mathf.RoundToInt(v);
		}, 5f));
		list.Add(("促銷折扣（%）", () => _promoDiscount.Value, (float v) =>
		{
			_promoDiscount.Value = Mathf.RoundToInt(v);
		}, 5f));
		list.Add(("促銷最低總庫存", () => _promoMinStock.Value, (float v) =>
		{
			_promoMinStock.Value = Mathf.RoundToInt(v);
		}, 10f));
		list.Add(("檢查間隔（秒）", () => _checkIntervalSeconds.Value, (float v) =>
		{
			_checkIntervalSeconds.Value = v;
		}, 5f));
		list.Add(("生產：排程門檻（生產部存貨）", () => _productionQueueThreshold.Value, (float v) =>
		{
			_productionQueueThreshold.Value = Mathf.RoundToInt(v);
		}, 10f));
	}

	private IEnumerator OrderRoutine(GameData gd, ManagerBlackboard mb, ProductListing pl, List<int> ids, int orderMode)
	{
		try
		{
			Dictionary<int, float> dictionary = new Dictionary<int, float>();
			float total = 0f;
			foreach (int id in ids)
			{
				ProductListing.ProductData productData = pl.productsData[id];
				float num = 1f;
				if (pl.tierInflation != null && productData.productTier >= 0 && productData.productTier < pl.tierInflation.Length)
				{
					num = pl.tierInflation[productData.productTier];
				}
				float num2 = (dictionary[id] = Mathf.Round(Mathf.Round(productData.basePricePerUnit * num * 100f) / 100f * (float)Mathf.Max(1, productData.maxItemsPerBox) * 100f) / 100f);
				total += num2 * (float)Mathf.Max(1, _boxesPerProduct.Value);
			}
			if (gd.gameFunds < total + _fundsBuffer.Value)
			{
				Log.LogInfo($"Skip auto-order: not enough funds (need {total:F2}, have {gd.gameFunds:F2}).");
				if (_notifications.Value)
				{
					PendingNotification = "`Better Supermarket: not enough funds ($" + total.ToString("F2") + ")";
				}
				yield break;
			}
			string summary = "";
			foreach (int id2 in ids)
			{
				for (int i = 0; i < Mathf.Max(1, _boxesPerProduct.Value); i++)
				{
					mb.AddShoppingListProduct(id2, dictionary[id2]);
				}
				if (summary.Length > 0)
				{
					summary += ", ";
				}
				string text = null;
				try
				{
					if (LocalizationManager.instance != null)
					{
						text = LocalizationManager.instance.GetLocalizationString("product" + id2);
					}
				}
				catch
				{
				}
				summary += (string.IsNullOrEmpty(text) ? ("#" + id2) : text);
			}
			yield return new WaitForEndOfFrame();
			yield return new WaitForEndOfFrame();
			try
			{
				mb.BuyCargo();
			}
			catch (Exception ex)
			{
				Log.LogError("BuyCargo failed: " + ex);
				yield break;
			}
			Dictionary<int, int> dictionary2 = ((orderMode == 1) ? _ordersThisNight : _ordersThisDaytime);
			Dictionary<int, float> dictionary3 = ((orderMode == 1) ? _cooldownUntilNight : _cooldownUntilDaytime);
			float num4;
			if (_gateOnIdleEmployee.Value)
			{
				num4 = _minOrderIntervalSeconds.Value;
			}
			else
			{
				num4 = ((orderMode == 1) ? _reorderCooldownSeconds.Value : _daytimeReorderCooldownSeconds.Value);
			}
			string text2 = ((orderMode == 1) ? "night" : "daytime");
			foreach (int id3 in ids)
			{
				dictionary2.TryGetValue(id3, out var value);
				dictionary2[id3] = value + 1;
				dictionary3[id3] = Time.unscaledTime + num4;
				_zeroStreak.Remove(id3);
			}
			Log.LogInfo($"Auto-ordered ({text2}) {ids.Count} product(s): {summary} | total ${total:F2}");
			if (_notifications.Value)
			{
				PendingNotification = "`AutoRestock ordered: " + summary + " ($" + total.ToString("F2") + ")";
			}
		}
		finally
		{
			_ordering = false;
		}
	}

	private bool RunAutoPricing(GameData gd)
	{
		ProductListing instance = ProductListing.Instance;
		if (instance == null || instance.productsData == null || instance.availableProducts == null || instance.productPlayerPricing == null)
		{
			return false;
		}
		int num = 0;
		foreach (int item in new List<int>(instance.availableProducts))
		{
			if (item >= 0 && item < instance.productsData.Length && instance.productsData[item] != null && item < instance.productPlayerPricing.Length)
			{
				ProductListing.ProductData productData = instance.productsData[item];
				float num2 = 1f;
				if (instance.tierInflation != null && productData.productTier >= 0 && productData.productTier < instance.tierInflation.Length)
				{
					num2 = instance.tierInflation[productData.productTier];
				}
				float num3 = productData.basePricePerUnit * num2 * ((float)_pricingPercentage.Value / 100f);
				num3 = (_pricingRoundDown.Value ? (Mathf.Floor(num3 * 10f) / 10f) : (Mathf.Floor(num3 * 100f) / 100f));
				if (instance.productPlayerPricing[item] != num3)
				{
					instance.CmdUpdateProductPrice(item, num3);
					num++;
				}
			}
		}
		if (num > 0)
		{
			Log.LogInfo($"Auto-pricing: updated {num} product price(s) to {_pricingPercentage.Value}% of cost.");
		}
		return true;
	}

	private bool TryAutoPromote(GameData gd)
	{
		ProductListing instance = ProductListing.Instance;
		if (instance == null || instance.productsData == null || instance.availableProducts == null)
		{
			return false;
		}
		ManagerBlackboard component = gd.GetComponent<ManagerBlackboard>();
		if (component == null)
		{
			return false;
		}
		if (_getExistences == null)
		{
			_getExistences = AccessTools.Method(typeof(ManagerBlackboard), "GetProductsExistences");
			if (_getExistences == null)
			{
				if (!_warnedReflection)
				{
					Log.LogWarning("ManagerBlackboard.GetProductsExistences not found (promo).");
					_warnedReflection = true;
				}
				return false;
			}
		}
		Dictionary<int, int> dictionary = null;
		if (_includeProductionStock.Value)
		{
			dictionary = new Dictionary<int, int>();
			ManufacturingContainer[] array = UnityEngine.Object.FindObjectsByType<ManufacturingContainer>(FindObjectsSortMode.None);
			foreach (ManufacturingContainer manufacturingContainer in array)
			{
				if (manufacturingContainer == null)
				{
					continue;
				}
				int[] productInfoArray = manufacturingContainer.productInfoArray;
				if (productInfoArray == null)
				{
					continue;
				}
				for (int j = 0; j + 1 < productInfoArray.Length; j += 2)
				{
					int num = productInfoArray[j];
					int num2 = productInfoArray[j + 1];
					if (num >= 0 && num2 > 0)
					{
						dictionary.TryGetValue(num, out var value);
						dictionary[num] = value + num2;
					}
				}
			}
		}
		int num3 = -1;
		int num4 = 0;
		double num5 = -1.0;
		double num6 = 0.0;
		List<int> list = null;
		if (_promoUseSalesRate.Value && StatisticsManager.Instance != null)
		{
			list = StatisticsManager.Instance.productsSold;
		}
		foreach (int item in new List<int>(instance.availableProducts))
		{
			if (item < 0 || item >= instance.productsData.Length || instance.productsData[item] == null)
			{
				continue;
			}
			int[] array2;
			try
			{
				array2 = (int[])_getExistences.Invoke(component, new object[1] { item });
			}
			catch (Exception ex)
			{
				if (!_warnedReflection)
				{
					Log.LogWarning("GetProductsExistences invoke failed (promo): " + ex.Message);
					_warnedReflection = true;
				}
				return false;
			}
			if (array2 == null || array2.Length < 3)
			{
				continue;
			}
			int num7 = array2[0] + array2[1] + array2[2];
			if (dictionary != null && dictionary.TryGetValue(item, out var value2))
			{
				num7 += value2;
			}
			double num8 = 0.0;
			if (list != null && item < list.Count)
			{
				int num9 = list[item];
				int num10 = 0;
				int num11 = 0;
				if (_soldSnapYesterday.TryGetValue(item, out var value3))
				{
					num10 += num9 - value3;
					num11++;
					if (_soldSnapDayBefore.TryGetValue(item, out var value4))
					{
						num10 += value3 - value4;
						num11++;
					}
					_soldSnapDayBefore[item] = value3;
				}
				_soldSnapYesterday[item] = num9;
				if (num11 > 0)
				{
					num8 = (double)num10 / (double)num11;
				}
			}
			if (num7 >= _promoMinStock.Value)
			{
				double num12 = (double)num7 / (num8 + 1.0);
				if (num12 > num5 || (num12 == num5 && num7 > num4))
				{
					num5 = num12;
					num4 = num7;
					num6 = num8;
					num3 = item;
				}
			}
		}
		if (num3 < 0)
		{
			return true;
		}
		if (instance.productsIDOnSale != null && instance.productsIDOnSale.Count > 0)
		{
			return true;
		}
		instance.SetProductOnSale(num3, _promoDiscount.Value);
		string text = null;
		try
		{
			if (LocalizationManager.instance != null)
			{
				text = LocalizationManager.instance.GetLocalizationString("product" + num3);
			}
		}
		catch
		{
		}
		string text2 = (string.IsNullOrEmpty(text) ? ("#" + num3) : text);
		string arg = ((num6 > 0.0) ? $" (~{num6:F1}/day, stock {num4})" : $" (stock {num4})");
		Log.LogInfo($"Auto-promo: put '{text2}' on sale (-{_promoDiscount.Value}%){arg}.");
		if (_notifications.Value)
		{
			PendingNotification = "`AutoPromo: " + text2 + " -" + _promoDiscount.Value + "%";
		}
		return true;
	}
}



