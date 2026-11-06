using System;

namespace BetterSupermarketTogether;

// Pure timing rules for companionship reactions. Unity sampling lives in the callers.

// Natural eye contact: look for a while, glance away briefly, look back. While working
// she only glances over occasionally and never fully turns.
internal sealed class CompanionGazePolicy
{
    private double _nextSwitch = double.NegativeInfinity;
    private bool _looking;
    internal double Weight { get; private set; }
    internal bool Looking => _looking;

    internal double Update(double now, double dt, bool available, bool working, double random)
    {
        random = Math.Max(0, Math.Min(1, random));
        if (!available) { _looking = false; _nextSwitch = double.NegativeInfinity; }
        else if (now >= _nextSwitch)
        {
            _looking = !_looking;
            _nextSwitch = now + (_looking ? (working ? 1.2 + .8 * random : 4 + 5 * random) : (working ? 4 + 6 * random : 1 + 1.5 * random));
        }
        double target = available && _looking ? (working ? .6 : 1) : 0;
        double step = (target > Weight ? 2.5 : 3) * Math.Max(0, dt);
        Weight = target > Weight ? Math.Min(target, Weight + step) : Math.Max(target, Weight - step);
        return Weight;
    }
}

// Reacts once when the player keeps her in the crosshair; re-arms only after looking away.
internal sealed class CompanionStarePolicy
{
    private double _since = -1, _awaySince = -1, _cooldown = double.NegativeInfinity;
    private bool _armed = true;
    internal const double Hold = 2, Rearm = .5, Cooldown = 60;

    internal bool Observe(double now, bool staring)
    {
        if (!staring)
        {
            _since = -1;
            if (_awaySince < 0) _awaySince = now;
            if (now - _awaySince >= Rearm) _armed = true;
            return false;
        }
        _awaySince = -1;
        if (_since < 0) _since = now;
        if (!_armed || now < _cooldown || now - _since < Hold) return false;
        _armed = false; _cooldown = now + Cooldown;
        return true;
    }
    internal void Reset() { _since = _awaySince = -1; _cooldown = double.NegativeInfinity; _armed = true; }
}

// Detects the player returning after being idle (AFK) for a while.
internal sealed class CompanionPresencePolicy
{
    private double _lastActive = -1;
    internal const double AwaySeconds = 180;

    // Returns how long the player was away when activity resumes after a long pause, otherwise 0.
    internal double Observe(double now, bool active)
    {
        if (_lastActive < 0) { _lastActive = now; return 0; }
        if (!active) return 0;
        double away = now - _lastActive; _lastActive = now;
        return away >= AwaySeconds ? away : 0;
    }
    internal double IdleFor(double now) => _lastActive < 0 ? 0 : Math.Max(0, now - _lastActive);
    internal void Reset() => _lastActive = -1;
}
