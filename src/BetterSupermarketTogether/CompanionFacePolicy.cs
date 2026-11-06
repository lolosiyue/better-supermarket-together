using System;
using System.Collections.Generic;

namespace BetterSupermarketTogether;

// Pure facial animation: emotion blending, natural blinking and talking mouth shapes.
// Output names are VRM 1.0 expression presets; CompanionFaceRig maps them to blend shapes.
internal sealed class CompanionFacePolicy
{
    internal static readonly string[] Emotions = { "happy", "angry", "sad", "relaxed", "surprised" };
    internal static readonly string[] Visemes = { "aa", "ih", "ou", "ee", "oh" };
    private static readonly double[] VisemeOdds = { .35, .15, .15, .15, .20 };
    private readonly Random _random;
    private readonly double[] _emotion = new double[Emotions.Length], _mouth = new double[Visemes.Length];
    private readonly Dictionary<string, double> _output = new Dictionary<string, double>();
    private int _base = -1, _event = -1, _viseme = -1;
    private double _baseIntensity, _eventIntensity, _eventUntil = double.NegativeInfinity, _talkUntil = double.NegativeInfinity;
    private double _nextSyllable, _visemeTarget, _nextBlink = -1, _blinkStart = -1, _blinkScale = 1;
    private bool _double;
    internal bool Tired;
    internal const double EmotionRate = 3, MouthRate = 18, TalkingEmotionCap = .5, Close = .06, Hold = .03, Open = .10;

    internal CompanionFacePolicy(Random random) { _random = random ?? new Random(); }

    internal static int Index(string emotion) => Array.IndexOf(Emotions, (emotion ?? "").Trim().ToLowerInvariant());

    // Resting face derived from mood; null or unknown means neutral.
    internal void SetBase(string emotion, double intensity) { _base = Index(emotion); _baseIntensity = Clamp01(intensity); }

    // A short-lived reaction that replaces the resting face.
    internal void Express(double now, string emotion, double intensity, double seconds)
    {
        int index = Index(emotion);
        if (index < 0 || seconds <= 0) return;
        _event = index; _eventIntensity = Clamp01(intensity); _eventUntil = now + seconds;
    }

    internal void Speak(double now, double seconds) { if (seconds > 0) _talkUntil = Math.Max(_talkUntil, now + seconds); }
    internal bool Talking(double now) => now < _talkUntil;
    internal void Reset()
    {
        Array.Clear(_emotion, 0, _emotion.Length); Array.Clear(_mouth, 0, _mouth.Length);
        _base = _event = _viseme = -1; _eventUntil = _talkUntil = double.NegativeInfinity; _nextBlink = _blinkStart = -1;
    }

    internal IReadOnlyDictionary<string, double> Update(double now, double dt)
    {
        dt = Math.Max(0, Math.Min(.25, dt));
        bool talking = Talking(now);
        bool reacting = now < _eventUntil && _event >= 0;
        double sum = 0;
        for (int i = 0; i < Emotions.Length; i++)
        {
            double target = reacting ? (i == _event ? _eventIntensity : 0) : (i == _base ? _baseIntensity : 0);
            if (talking) target = Math.Min(target, TalkingEmotionCap);
            _emotion[i] = Toward(_emotion[i], target, EmotionRate * dt);
            sum += _emotion[i];
            _output[Emotions[i]] = _emotion[i];
        }
        // VRM "blend" override: strong emotions (closed happy eyes, open surprised mouth) damp blink and speech.
        double expressive = Clamp01(sum);
        _output["blink"] = Blink(now) * (1 - expressive);
        _output["blinkLeft"] = 0; _output["blinkRight"] = 0;
        if (talking && now >= _nextSyllable)
        {
            double roll = _random.NextDouble();
            if (roll < .15) { _viseme = -1; _visemeTarget = 0; }
            else
            {
                double pick = _random.NextDouble(), edge = 0; _viseme = Visemes.Length - 1;
                for (int i = 0; i < Visemes.Length; i++) { edge += VisemeOdds[i]; if (pick < edge) { _viseme = i; break; } }
                _visemeTarget = .5 + .4 * _random.NextDouble();
            }
            _nextSyllable = now + .08 + .08 * _random.NextDouble();
        }
        if (!talking) _viseme = -1;
        for (int i = 0; i < Visemes.Length; i++)
        {
            _mouth[i] = Toward(_mouth[i], i == _viseme ? _visemeTarget : 0, MouthRate * dt);
            _output[Visemes[i]] = _mouth[i] * (1 - .5 * expressive);
        }
        return _output;
    }

    private double Blink(double now)
    {
        if (_nextBlink < 0) _nextBlink = now + Interval();
        if (_blinkStart < 0 && now >= _nextBlink)
        {
            _blinkStart = now; _blinkScale = Tired ? 1.6 : 1; _double = _random.NextDouble() < .15;
        }
        if (_blinkStart < 0) return 0;
        double t = now - _blinkStart, close = Close * _blinkScale, hold = Hold * _blinkScale, open = Open * _blinkScale;
        if (t < close) return t / close;
        if (t < close + hold) return 1;
        if (t < close + hold + open) return 1 - (t - close - hold) / open;
        if (_double) { _double = false; _blinkStart = now; return 0; }
        _blinkStart = -1; _nextBlink = now + Interval();
        return 0;
    }

    private double Interval() => (2 + 4 * _random.NextDouble()) * (Tired ? .7 : 1);
    private static double Toward(double value, double target, double step) => value < target ? Math.Min(target, value + step) : Math.Max(target, value - step);
    private static double Clamp01(double value) => value < 0 ? 0 : value > 1 ? 1 : value;
}
