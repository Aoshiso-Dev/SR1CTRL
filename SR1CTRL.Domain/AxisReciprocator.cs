namespace SR1CTRL.Domain;

public sealed class AxisReciprocator
{
    public AxisMotionSettings Settings { get; private set; }
    public bool Enabled { get; private set; } = true;

    private bool _towardMax = true;

    public DateTime NextAtUtc { get; private set; } = DateTime.UtcNow;

    public AxisReciprocator(AxisMotionSettings settings)
    {
        Settings = settings.Normalize();
    }

    public void UpdateSettings(AxisMotionSettings settings, bool applyImmediately)
    {
        Settings = settings.Normalize();

        if (applyImmediately)
        {
            NextAtUtc = DateTime.UtcNow;
        }
    }

    public string Reapply(DateTime nowUtc)
    {
        var oneWayMs = Settings.OneWayRampMs();

        var target = _towardMax ? Settings.Max : Settings.Min;
        NextAtUtc = nowUtc + TimeSpan.FromMilliseconds(oneWayMs);

        return TCodeFormatter.AxisWithInterval(Settings, target, oneWayMs);
    }

    public string Step(DateTime nowUtc)
    {
        var oneWayMs = Settings.OneWayRampMs();

        var target = _towardMax ? Settings.Max : Settings.Min;
        _towardMax = !_towardMax;
        NextAtUtc = nowUtc + TimeSpan.FromMilliseconds(oneWayMs);

        return TCodeFormatter.AxisWithInterval(Settings, target, oneWayMs);
    }
}
