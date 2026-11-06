using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BetterSupermarketTogether;

// Per-shop relationship, persisted in the companion memory file. Only grows from local,
// observed interaction (with a daily cap), so it cannot be farmed by spamming chat.
internal sealed class CompanionRelationship
{
    public int Affinity;
    public int DaysTogether;
    public int LastGameDay = -1;
    public int Chats;
    public string LastPlayed;
    public int GainDay = -1;
    public int GainedToday;
    public bool ChattedToday;

    internal const int DailyCap = 60, Max = 1000, FirstChat = 10, Chat = 2, NewDay = 5;
    internal static readonly int[] Thresholds = { 0, 100, 300, 600, 850 };
    private static readonly string[] Wire = { "new coworker", "familiar coworker", "friend", "close friend", "very close" };
    private static readonly string[] Chinese = { "新同事", "熟悉的同事", "朋友", "好朋友", "很親近" };
    private static readonly string[] English = { "New coworker", "Familiar coworker", "Friend", "Close friend", "Very close" };

    [JsonIgnore] internal int Stage { get { int stage = 0; for (int i = 0; i < Thresholds.Length; i++) if (Affinity >= Thresholds[i]) stage = i; return stage; } }
    [JsonIgnore] internal string StageWire => Wire[Stage];

    internal void Sanitize()
    {
        Affinity = Math.Max(0, Math.Min(Max, Affinity)); DaysTogether = Math.Max(0, DaysTogether); Chats = Math.Max(0, Chats);
        GainedToday = Math.Max(0, Math.Min(DailyCap, GainedToday));
        if (LastPlayed != null && !DateTime.TryParseExact(LastPlayed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) LastPlayed = null;
    }

    // Real days since the previous session; marks today as played.
    internal int Arrive(DateTime today)
    {
        int away = 0;
        if (LastPlayed != null && DateTime.TryParseExact(LastPlayed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var last))
            away = Math.Max(0, (today.Date - last.Date).Days);
        LastPlayed = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return away;
    }

    // Returns how many stages were gained (0 normally).
    internal int Gain(int day, int amount)
    {
        Roll(day);
        int before = Stage, allowed = Math.Max(0, Math.Min(amount, DailyCap - GainedToday));
        GainedToday += allowed; Affinity = Math.Min(Max, Affinity + allowed);
        return Stage - before;
    }

    internal int Talked(int day)
    {
        Roll(day); Chats++;
        int amount = ChattedToday ? Chat : FirstChat;
        ChattedToday = true;
        return Gain(day, amount);
    }

    internal int Day(int day)
    {
        if (day == LastGameDay) return 0;
        LastGameDay = day; DaysTogether++;
        return Gain(day, NewDay);
    }

    private void Roll(int day) { if (GainDay != day) { GainDay = day; GainedToday = 0; ChattedToday = false; } }

    internal JObject Snapshot() => new JObject { ["stage"] = StageWire, ["days_together"] = DaysTogether, ["conversations"] = Chats };
    internal string Summary(bool english) => english
        ? "Relationship: " + English[Stage] + " (" + Affinity + "/" + Max + "), " + DaysTogether + " days together"
        : "關係：" + Chinese[Stage] + "（" + Affinity + "/" + Max + "），一起度過 " + DaysTogether + " 天";
}

internal sealed class CompanionDiaryEntry
{
    public int Day;
    public string Date;
    public string Text;
}

// Short first-person diary entries written at closing time from the day's notes.
internal sealed class CompanionDiary
{
    public List<CompanionDiaryEntry> Entries = new List<CompanionDiaryEntry>();
    public string LongAgo;
    public int NotesDay = -1;
    public List<string> Notes = new List<string>();
    internal const int MaxEntries = 14, RollCount = 7, MaxNotes = 12, EntryLength = 200, NoteLength = 160, LongAgoLength = 400;

    internal void Sanitize()
    {
        Entries = (Entries ?? new List<CompanionDiaryEntry>()).Where(x => x != null && !string.IsNullOrWhiteSpace(x.Text)).ToList();
        foreach (var entry in Entries) entry.Text = Clip(entry.Text, EntryLength);
        while (Entries.Count > MaxEntries + RollCount) Entries.RemoveAt(0);
        Notes = (Notes ?? new List<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => Clip(x, NoteLength)).ToList();
        while (Notes.Count > MaxNotes) Notes.RemoveAt(0);
        if (Notes.Count == 0) NotesDay = -1;
        LongAgo = string.IsNullOrWhiteSpace(LongAgo) ? null : Clip(LongAgo, LongAgoLength);
    }

    // Notes for a new day wait until the previous day's diary has been taken.
    internal bool Note(int day, string note)
    {
        note = Clip(note, NoteLength);
        if (note.Length == 0) return false;
        if (NotesDay != day) { if (Notes.Count > 0) return false; NotesDay = day; }
        if (Notes.Count > 0 && Notes[Notes.Count - 1] == note) return false;
        Notes.Add(note);
        while (Notes.Count > MaxNotes) Notes.RemoveAt(0);
        return true;
    }

    // Any other day (including an older save being loaded) flushes the pending notes.
    internal bool Due(int gameDay, bool closing) => Notes.Count > 0 && (NotesDay != gameDay || closing);

    internal List<string> Take(out int day)
    {
        day = NotesDay; var notes = Notes.ToList();
        Notes.Clear(); NotesDay = -1;
        return notes;
    }

    // A failed write puts the notes back unless a new day has already started collecting.
    internal void Restore(int day, List<string> notes)
    {
        if (Notes.Count > 0 || notes == null) return;
        NotesDay = day; Notes.AddRange(notes.Take(MaxNotes));
    }

    internal void Add(int day, string date, string text)
    {
        text = Clip(text, EntryLength);
        if (text.Length == 0) return;
        Entries.RemoveAll(x => x.Day == day);
        Entries.Add(new CompanionDiaryEntry { Day = day, Date = date, Text = text });
        Entries.Sort((a, b) => a.Day.CompareTo(b.Day));
        while (Entries.Count > MaxEntries + RollCount) Entries.RemoveAt(0);
    }

    internal bool NeedsRollUp => Entries.Count > MaxEntries;
    internal List<CompanionDiaryEntry> Oldest => Entries.Take(RollCount).ToList();
    internal void RolledUp(List<CompanionDiaryEntry> rolled, string summary)
    {
        summary = Clip(summary, LongAgoLength);
        if (summary.Length == 0) return;
        Entries.RemoveAll(x => rolled.Contains(x));
        LongAgo = summary;
    }

    internal JObject Snapshot(int gameDay, int count = 5)
    {
        var recent = Entries.Skip(Math.Max(0, Entries.Count - count))
            .Select(x => new JObject { ["days_ago"] = Math.Max(0, gameDay - x.Day), ["entry"] = x.Text });
        return new JObject { ["long_ago"] = LongAgo == null ? JValue.CreateNull() : (JToken)LongAgo, ["recent_days"] = new JArray(recent) };
    }

    internal string Summary(bool english, int count = 3)
    {
        if (Entries.Count == 0) return english ? "Diary: nothing written yet." : "日記：尚未寫下。";
        var lines = Entries.Skip(Math.Max(0, Entries.Count - count)).Reverse().Select(x => (english ? "Day " : "第 ") + x.Day + (english ? ": " : " 天：") + x.Text);
        return (english ? "Diary:\n" : "日記：\n") + string.Join("\n", lines);
    }

    internal static string Clip(string text, int length)
    {
        text = (text ?? "").Replace("\r", " ").Replace("\n", " ").Trim().Trim('"', '「', '」').Trim();
        return text.Length <= length ? text : text.Substring(0, length);
    }
}
