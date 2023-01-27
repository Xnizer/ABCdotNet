using System;

namespace ABCdotNet
{
    public sealed partial class ColonySettings
    {
        private class Validator
        {
            /// <summary>
            /// Validates all properties of the specified <see cref="ColonySettings"/>,
            /// and throws an <see cref="InvalidColonySettingException"/> when an invalid value is found.
            /// </summary>
            /// <param name="settings">The <see cref="ColonySettings"/> to be validated.</param>
            /// <exception cref="InvalidColonySettingException"></exception>
            public static void Validate(ColonySettings settings)
            {
                if (settings.Dimensions <= 0)
                    throw new InvalidColonySettingException(
                        nameof(settings.Dimensions),
                        settings.Dimensions,
                        $"{nameof(settings.Dimensions)} must be greater then 0.");

                if (settings.Size <= 1)
                    throw new InvalidColonySettingException(
                        nameof(settings.Size),
                        settings.Size,
                        $"{nameof(settings.Size)} must be greater then 1.");

                if (settings.Cycles <= 0)
                    throw new InvalidColonySettingException(
                        nameof(settings.Cycles),
                        settings.Cycles,
                        $"{nameof(settings.Cycles)} must be greater then 0.");

                if (!Enum.IsDefined(settings.BoundaryCondition))
                    throw new InvalidColonySettingException(
                        nameof(settings.BoundaryCondition),
                        settings.BoundaryCondition,
                        $"Undefined {nameof(settings.BoundaryCondition)} value.");

                if (!Enum.IsDefined(settings.FitnessObjective))
                    throw new InvalidColonySettingException(
                        nameof(settings.FitnessObjective),
                        settings.FitnessObjective,
                        $"Undefinded {nameof(settings.FitnessObjective)} value.");

                if (settings.Constraints.IsEmpty)
                    throw new InvalidColonySettingException(
                        nameof(settings.Constraints),
                        settings.Constraints.ToArray(),
                        $"{nameof(settings.Constraints)} cannot be empty.");

                if (settings.Constraints.Length != settings.Dimensions)
                    throw new InvalidColonySettingException(
                        nameof(settings.Constraints),
                        settings.Constraints.ToArray(),
                        $"The number of {nameof(settings.Constraints)} must match the number of {nameof(settings.Dimensions)}.");

                if (settings.ObjectiveFunction is null)
                    throw new InvalidColonySettingException(
                        nameof(settings.ObjectiveFunction),
                        settings.ObjectiveFunction,
                        $"{nameof(settings.ObjectiveFunction)} cannot be null.");
            }
        }
    }

    public class InvalidColonySettingException : Exception
    {
        public string Name { get; }
        public object? Value { get; }

        public InvalidColonySettingException(string name, object? value, string message) : base(message)
        {
            Name = name;
            Value = value;
        }
    }
}
