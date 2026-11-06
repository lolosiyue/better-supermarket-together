using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace BetterSupermarketTogether;

// Local inner state derived only from observed game facts. It never calls the API;
// the model receives coarse words so replies can reflect fatigue and recent events.
internal sealed class CompanionMood
{
    private struct Memory { public double At; public string Text; }
    private readonly List<Memory> _events = new List<Memory>();
    private double _last = -1, _lastChat = -1, _streakStart = -1, _lastWork = -1, _lastChatBoost = -1, _since = -1;
    private int _day = -1;
    internal double Energy { get; private set; } = 80;
    internal double Mood { get; private set; } = 20;
    internal const double LonelyMinutes = 20, EventMinutes = 10;

    internal void Reset()
    {
        _events.Clear(); _last = _lastChat = _streakStart = _lastWork = _lastChatBoost = _since = -1; _day = -1; Energy = 80; Mood = 20;
    }

    internal void Observe(double now, bool working, int day)
    {
        if (_day != day) { if (_day >= 0) Energy = Math.Max(Energy, 85); _day = day; }
        if (_last < 0) { _last = _since = now; return; }
        double delta = Math.Max(0, Math.Min(1, now - _last)); _last = now;
        if (working)
        {
            if (_streakStart < 0) _streakStart = now;
            _lastWork = now;
            Energy = Clamp(Energy - .04 * delta, 0, 100);
        }
        else
        {
            if (_streakStart >= 0 && now - _lastWork > 20) _streakStart = -1;
            Energy = Clamp(Energy + .08 * delta, 0, 100);
        }
        double baseline = (Energy - 50) / 5;
        Mood += (baseline - Mood) * Math.Min(1, .004 * delta);
        _events.RemoveAll(x => now - x.At > EventMinutes * 60);
    }

    internal void Event(double now, string text, double mood, double energy = 0)
    {
        Mood = Clamp(Mood + mood, -100, 100); Energy = Clamp(Energy + energy, 0, 100);
        _events.Add(new Memory { At = now, Text = text });
        while (_events.Count > 5) _events.RemoveAt(0);
    }

    // Being talked to cheers her up, but spamming does not stack.
    internal void Chatted(double now)
    {
        _lastChat = now;
        if (_lastChatBoost < 0 || now - _lastChatBoost > 120) { Mood = Clamp(Mood + 6, -100, 100); _lastChatBoost = now; }
    }

    internal double? MinutesSinceChat(double now) => _lastChat < 0 ? (double?)null : (now - _lastChat) / 60;
    internal bool Lonely(double now) => _since >= 0 && Mood < 40 && (now - Math.Max(_lastChat, _since)) / 60 >= LonelyMinutes;

    internal string EnergyWord => Energy >= 70 ? "energetic" : Energy >= 40 ? "fine" : Energy >= 15 ? "a bit tired" : "exhausted";
    internal string MoodWord(double now) => Lonely(now) ? "a little lonely (the player has not talked to you for a while)" :
        Mood >= 40 ? "cheerful" : Mood >= 10 ? "content" : Mood > -15 ? "calm" : "frustrated";

    internal JObject Snapshot(double now)
    {
        var result = new JObject { ["energy"] = EnergyWord, ["mood"] = MoodWord(now),
            ["working_without_break_minutes"] = _streakStart < 0 ? 0 : (int)((now - _streakStart) / 60),
            ["recent_events"] = new JArray(_events.Select(x => new JObject { ["minutes_ago"] = (int)((now - x.At) / 60), ["event"] = x.Text })) };
        var since = MinutesSinceChat(now);
        result["minutes_since_player_talked"] = since.HasValue ? (JToken)(int)since.Value : JValue.CreateNull();
        return result;
    }

    private static double Clamp(double value, double min, double max) => value < min ? min : value > max ? max : value;
}

// Limits unprompted chat lines: a minimum gap plus a sliding one-hour budget.
internal sealed class CompanionRemarkGate
{
    private readonly Queue<double> _recent = new Queue<double>();
    private double _last = double.NegativeInfinity;
    internal const double MinimumGap = 180;
    internal bool Ready(double now, int perHour)
    {
        while (_recent.Count > 0 && now - _recent.Peek() >= 3600) _recent.Dequeue();
        return perHour > 0 && now - _last >= MinimumGap && _recent.Count < perHour;
    }
    internal void Used(double now) { _last = now; _recent.Enqueue(now); }
    internal void Reset() { _recent.Clear(); _last = double.NegativeInfinity; }
}
