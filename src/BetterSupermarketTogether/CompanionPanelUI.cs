using System;
using System.Linq;
using StarterAssets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BetterSupermarketTogether;

internal static partial class SettingsPanelUI
{
    private static GameObject _page4;
    private static Text _chatText, _chatStatus, _binding, _facts, _cacheStats, _workStatus, _autoLabel, _eraseLabel;
    private static InputField _messageInput;
    private static ScrollRect _chatScroll;
    private static bool _companionOpen, _eraseArmed, _submitPending;
    private static float _eraseArmedAt;
    private const float EraseConfirmSeconds = 4f;
    internal static bool CompanionOpen => _companionOpen && PanelOpen;
    internal static bool Typing => EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null && EventSystem.current.currentSelectedGameObject.GetComponent<InputField>()?.isFocused == true;
    private static DeepSeekCompanion Companion => BetterSupermarketPlugin.Instance?.Companion;

    internal static void OpenCompanion()
    {
        if (!BetterSupermarketPlugin.IsHost || Companion == null) return;
        if (_companionOpen) { TogglePanel(); return; }
        Companion.WorkWorld.PointAtShelf();
        if (_panel == null) BuildPanel();
        if (_panel == null) return;
        if (_page4 == null) BuildCompanionPage();
        _panel.SetActive(true); _backdrop?.SetActive(true);
        ShowPage(4);
        _companionOpen = true;
        CapturePlayer();
        RefreshCompanion();
    }

    internal static void CloseCompanion()
    {
        if (!_companionOpen) return;
        _companionOpen = false; _submitPending = false;
        SetEraseArmed(false);
        if (_page4 != null) _page4.SetActive(false);
        if (_messageInput != null) _messageInput.DeactivateInputField();
    }

    internal static void RefreshCompanion()
    {
        if (!_companionOpen || _chatText == null || Companion == null) return;
        if (_eraseArmed && Time.unscaledTime - _eraseArmedAt > EraseConfirmSeconds) SetEraseArmed(false);
        _binding.text = Companion.BindingLabel;
        _chatStatus.text = Companion.Status + "\n" + Companion.AvatarStatus;
        _facts.text = Companion.Facts;
        _cacheStats.text = Companion.CacheStatus;
        _workStatus.text = Companion.WorkStatus;
        _autoLabel.text = ModText.Format("自主工作：{0}",new ModText.Message(Companion.Autonomous?"開":"關"));
        string transcript = Companion.Transcript;
        if (_chatText.text != transcript)
        {
            _chatText.text = transcript;
            Canvas.ForceUpdateCanvases(); _chatScroll.verticalNormalizedPosition = 0;
        }
    }

    private static void SetEraseArmed(bool armed)
    {
        _eraseArmed = armed;
        _eraseArmedAt = Time.unscaledTime;
        if (_eraseLabel != null) ModText.Bind(_eraseLabel, armed ? "再按一次確認清除" : "清除對話與記憶");
    }

    private static void SubmitMessage()
    {
        if (_messageInput == null || Companion == null) return;
        Companion.Send(_messageInput.text);
        if (Companion.Busy) _messageInput.text = "";
    }

    // InputField is mid-edit inside onValidateInput, so the send runs on the next host tick.
    private static void ProcessPendingSubmit()
    {
        if (!_submitPending) return;
        _submitPending = false;
        if (_companionOpen) SubmitMessage();
    }

    private static bool EnterPressed() => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    private static bool ShiftHeld() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

    private static void BuildCompanionPage()
    {
        _page4 = NewStretch(_panel.transform, "DeepSeekCompanion");
        var parent = _page4.transform;
        NewText(parent, "DeepSeek 娘 · 超市同伴", 22, new Vector2(-285, 292), new Vector2(410, 32), TextAnchor.MiddleLeft);
        AddButton(parent, "返回", new Vector2(355,292), new Vector2(85,32), new Color(.2f,.35f,.55f), () => ShowPage(1));
        _binding = NewText(parent, "", 15, new Vector2(-100,249), new Vector2(770,28), TextAnchor.MiddleLeft);
        AddButton(parent, "解除綁定", new Vector2(415,249), new Vector2(130,30), new Color(.4f,.3f,.35f), () => Companion.Unbind());
        NewText(parent, "員工編號", 14, new Vector2(-435,212), new Vector2(110,28), TextAnchor.MiddleLeft);
        var index = CompanionInput(parent, new Vector2(-335,212), new Vector2(90,30), "1", 4);
        index.contentType = InputField.ContentType.IntegerNumber;
        AddButton(parent, "綁定員工", new Vector2(-205,212), new Vector2(140,30), new Color(.15f,.4f,.55f), () => {
            if (int.TryParse(index.text, out int number)) Companion.Bind(number - 1);
        });
        var auto=AddButton(parent,"自主工作：關",new Vector2(25,212),new Vector2(170,30),new Color(.15f,.4f,.35f),()=>Companion.SetAutonomy(!Companion.Autonomous));
        _autoLabel=auto.GetComponentInChildren<Text>();
        AddButton(parent,"停止計畫",new Vector2(230,212),new Vector2(150,30),new Color(.5f,.25f,.3f),()=>Companion.StopWork());
        AddButton(parent,"指向貨架",new Vector2(410,212),new Vector2(150,30),new Color(.2f,.35f,.5f),()=>Companion.SelectShelf());

        var viewport = NewRect(parent, "ChatViewport", new Vector2(650,305), new Vector2(-160,32), new Color(.05f,.07f,.1f,.8f));
        viewport.AddComponent<RectMask2D>();
        _chatScroll = viewport.AddComponent<ScrollRect>(); _chatScroll.horizontal = false;
        _chatScroll.movementType = ScrollRect.MovementType.Clamped;
        var content = new GameObject("Conversation", typeof(RectTransform), typeof(Text));
        content.transform.SetParent(viewport.transform, false);
        var rect = content.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0,1); rect.anchorMax = new Vector2(1,1); rect.pivot = new Vector2(.5f,1);
        rect.sizeDelta = new Vector2(-20,0); rect.anchoredPosition = new Vector2(0,-8);
        _chatText = content.GetComponent<Text>(); _chatText.font = UiFont; _chatText.fontSize = 16; _chatText.color = Color.white;
        _chatText.supportRichText = false; _chatText.alignment = TextAnchor.UpperLeft;
        var fitter = content.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _chatScroll.viewport = viewport.GetComponent<RectTransform>(); _chatScroll.content = rect;
        NewText(parent,"工作計畫",16,new Vector2(325,166),new Vector2(280,28),TextAnchor.MiddleLeft);
        _workStatus=CompanionReadout(parent,"WorkPlanViewport",new Vector2(330,104),new Vector2(285,94),12);
        _workStatus.supportRichText=false;
        NewText(parent, "本商店的記憶", 14, new Vector2(325,40), new Vector2(280,24), TextAnchor.MiddleLeft);
        _facts = CompanionReadout(parent,"MemoryViewport",new Vector2(330,-10),new Vector2(285,70),12);
        _facts.supportRichText = false;
        _cacheStats = NewText(parent,"",11,new Vector2(330,-75),new Vector2(285,62),TextAnchor.UpperLeft);
        _cacheStats.supportRichText = false;
        _messageInput = CompanionInput(parent, new Vector2(-205,-157), new Vector2(560,54), "", 1000);
        _messageInput.lineType = InputField.LineType.MultiLineNewline;
        // Enter sends, Shift+Enter inserts a newline. Pasted newlines and IME commits carry no Enter key-down.
        _messageInput.onValidateInput = (text, index, ch) =>
        {
            if (ch == '\n' && EnterPressed() && !ShiftHeld()) { _submitPending = true; return '\0'; }
            return ch;
        };
        var hint = NewText(_messageInput.transform, "輸入訊息：Enter 傳送，Shift+Enter 換行", 14, Vector2.zero, new Vector2(546,50), TextAnchor.MiddleLeft);
        hint.color = new Color(.45f,.45f,.5f); hint.fontStyle = FontStyle.Italic;
        _messageInput.placeholder = hint;
        AddButton(parent, "傳送", new Vector2(145,-157), new Vector2(100,48), new Color(.15f,.5f,.5f), SubmitMessage);
        AddButton(parent, "取消請求", new Vector2(330,-125), new Vector2(200,30), new Color(.4f,.3f,.35f), () => Companion.Cancel());
        var erase = AddButton(parent, "清除對話與記憶", new Vector2(330,-165), new Vector2(200,30), new Color(.4f,.3f,.35f), () => {
            if (_eraseArmed) { Companion.ClearMemory(); SetEraseArmed(false); }
            else SetEraseArmed(true);
        });
        _eraseLabel = erase.GetComponentInChildren<Text>();
        _chatStatus = NewText(parent, "", 13, new Vector2(0,-202), new Vector2(960,38), TextAnchor.MiddleLeft);
        _chatStatus.supportRichText = false;
        NewText(parent, "API 網址", 12, new Vector2(-440,-243), new Vector2(100,28), TextAnchor.MiddleLeft);
        var url = CompanionInput(parent, new Vector2(-210,-243), new Vector2(340,28), Companion.BaseUrl, 250);
        NewText(parent, "模型", 12, new Vector2(0,-243), new Vector2(60,28), TextAnchor.MiddleLeft);
        var model = CompanionInput(parent, new Vector2(155,-243), new Vector2(230,28), Companion.Model, 100);
        NewText(parent, "API 金鑰", 12, new Vector2(-440,-283), new Vector2(100,28), TextAnchor.MiddleLeft);
        var key = CompanionInput(parent, new Vector2(-210,-283), new Vector2(340,28), "", 512);
        key.contentType = InputField.ContentType.Password;
        AddButton(parent, "儲存 API 設定", new Vector2(150,-283), new Vector2(230,30), new Color(.2f,.35f,.55f), () => {
            Companion.Configure(url.text, model.text, key.text); key.text = "";
        });
        NewText(parent, "留空金鑰沿用原值", 12, new Vector2(385,-283), new Vector2(200,28), TextAnchor.MiddleCenter);
        _page4.SetActive(false);
    }

    private static Text CompanionReadout(Transform parent,string name,Vector2 position,Vector2 size,int fontSize)
    {
        var viewport=NewRect(parent,name,size,position,new Color(.05f,.07f,.1f,.3f));viewport.AddComponent<RectMask2D>();
        var scroll=viewport.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
        var text=NewText(viewport.transform,"",fontSize,Vector2.zero,new Vector2(size.x-12,0),TextAnchor.UpperLeft);
        text.resizeTextForBestFit=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Overflow;
        var rect=text.rectTransform;rect.anchorMin=new Vector2(0,1);rect.anchorMax=new Vector2(1,1);rect.pivot=new Vector2(.5f,1);
        rect.sizeDelta=new Vector2(-12,0);rect.anchoredPosition=new Vector2(0,-4);
        text.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport=viewport.GetComponent<RectTransform>();scroll.content=rect;return text;
    }

    private static InputField CompanionInput(Transform parent, Vector2 position, Vector2 size, string value, int limit)
    {
        var box = NewRect(parent, "Input", size, position, new Color(.95f,.95f,.95f));
        var input = box.AddComponent<InputField>();
        var text = NewText(box.transform, "", 15, Vector2.zero, size - new Vector2(14,4), TextAnchor.MiddleLeft);
        text.color = Color.black; text.supportRichText = false;
        input.textComponent = text; input.characterLimit = limit; input.text = value;
        return input;
    }
}
