namespace SR1CTRL.Presentation.Config;

public static class MotionDefaults
{
    public const int DefaultBaudRate = 115200;

    public const int DefaultChannel = 0;

    public const double LinearMinDefault = 0.20;
    public const double LinearMaxDefault = 0.80;
    public const double LinearSpeedDefault = 0.20;
    public const double LinearSpeedMin = 0.01;
    public const double LinearSpeedMax = 3.0;

    public const double RotateMinDefault = 0.35;
    public const double RotateMaxDefault = 0.65;
    public const double RotateSpeedDefault = 0.10;
    public const double RotateSpeedMin = 0.01;
    public const double RotateSpeedMax = 1.0;

    public const double LinearSpeedStep = 0.1;
    public const double RotateSpeedStep = 0.05;
}
