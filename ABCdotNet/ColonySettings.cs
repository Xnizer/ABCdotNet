using System;

namespace ABCdotNet
{
    public sealed partial class ColonySettings
    {
        private ColonySettings()
        {
        }

        /// <summary>
        /// The seed of the random number generator.
        /// </summary>
        public ulong Seed { get; private set; }

        /// <summary>
        /// The dimensions of a food source position.
        /// Represent the number of objective function parameters.
        /// </summary>
        public int Dimensions { get; private set; }

        /// <summary>
        /// The number of food sources (candidate solutions),
        /// and employed bees that exploit those sources.
        /// </summary>
        public int Size { get; private set; }

        /// <summary>
        /// The number of iterations in the colony simulation.
        /// </summary>
        public int Cycles { get; private set; }

        /// <summary>
        /// Indicates how out-of-bound values of are treated.
        /// </summary>
        public BoundaryCondition BoundaryCondition { get; private set; }

        /// <summary>
        /// Indicates whether the objective of the colony is to minimize or maximize the fitness value of a solution.
        /// </summary>
        public FitnessObjective FitnessObjective { get; private set; }


        private Constraint[]? _constraints;
        /// <summary>
        /// The constraints for each dimension of a food source.
        /// </summary>
        public ReadOnlySpan<Constraint> Constraints => _constraints;

        public ObjectiveFunction? ObjectiveFunction { get; private set; }
    }
}
