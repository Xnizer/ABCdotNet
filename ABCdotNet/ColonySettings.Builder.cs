using System;

namespace ABCdotNet
{
    public sealed partial class ColonySettings
    {
        /// <summary>
        /// Creates an instance of <see cref="Builder"/> that can be used to build a <see cref="ColonySettings"/> object.
        /// </summary>
        /// <returns>A <see cref="Builder"/> instance.</returns>
        public static Builder CreateBuilder() => new Builder();

        public sealed class Builder
        {
            private readonly ColonySettings _settings;

            public Builder()
            {
                // default settings
                _settings = new ColonySettings()
                {
                    Seed = (ulong)Random.Shared.NextInt64(),
                    Dimensions = 2,
                    Size = 10,
                    Cycles = 100,
                    BoundaryCondition = BoundaryCondition.CBC,
                    FitnessObjective = FitnessObjective.Minimize
                };
            }

            public Builder SetSeed(ulong seed)
            {
                _settings.Seed = seed;
                return this;
            }

            public Builder SetDimensions(int dimensions)
            {
                _settings.Dimensions = dimensions;
                return this;
            }

            public Builder SetSize(int size)
            {
                _settings.Size = size;
                return this;
            }

            public Builder SetCycles(int cycles)
            {
                _settings.Cycles = cycles;
                return this;
            }

            public Builder SetBoundaryCondition(BoundaryCondition boundaryCondition)
            {
                _settings.BoundaryCondition = boundaryCondition;
                return this;
            }

            public Builder SetFitnessObjective(FitnessObjective fitnessObjective)
            {
                _settings.FitnessObjective = fitnessObjective;
                return this;
            }

            // Fix: changing dimensions after calling this method makes the constraints invalid.
            public Builder SetConstraints(Constraint constraint)
            {
                _settings._constraints = new Constraint[_settings.Dimensions];
                Array.Fill(_settings._constraints, constraint);
                return this;
            }

            public Builder SetConstraints(params Constraint[] constraints)
            {
                _settings._constraints = constraints.Clone() as Constraint[];
                return this;
            }

            public Builder SetObjectiveFunction(ObjectiveFunction function)
            {
                _settings.ObjectiveFunction = function;
                return this;
            }

            /// <summary>
            /// Validates settings, and returns a valid <see cref="ColonySettings"/> object.
            /// </summary>
            /// <returns>A <see cref="ColonySettings"/> instance.</returns>
            /// <exception cref="InvalidColonySettingException"></exception>
            public ColonySettings Build()
            {
                // TODO: use some default constraints if none has been set.

                Validator.Validate(_settings);

                return _settings;             
            }
        }
    }
}
