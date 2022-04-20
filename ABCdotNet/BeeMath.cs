using System;
using System.Runtime.CompilerServices;

namespace ABCdotNet;

public static class BeeMath
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CBC(double value, double min, double max)
    {
        return value < min ? min : value > max ? max : value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double PBC(double value, double min, double max)
    {
        double norm = value - min;

        double diff = max - min;

        return min + norm - (diff * Math.Floor(norm / diff));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double RBC(double value, double min, double max)
    {
        double diff = max - min;

        double phase = value - min - diff;

        double period = diff * 2.0;

        return min + Math.Abs(phase - (period * Math.Floor(phase / period)) - diff);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Fitness(double f)
    {
        return f >= 0.0 ? 1.0 / (1.0 + f) : 1.0 + Math.Abs(f);
    }
}
