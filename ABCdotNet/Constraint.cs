using System;

namespace ABCdotNet
{
    public struct Constraint
    {
        public double MinValue { get; }
        public double MaxValue { get; }

        public static implicit operator Constraint((double min, double max) tuple) => new Constraint(tuple.min, tuple.max);

        public Constraint(double min, double max)
        {
            if (min > max)
                throw new ArgumentException($"The value of '{nameof(min)}' must be greater then the value of '{nameof(max)}'.");

            MinValue = min;
            MaxValue = max;
        }
    }
}
