using System;
using System.Runtime.CompilerServices;
using Redzen.Random;

namespace ABCdotNet;

public delegate double ObjectiveFunction(Span<double> source);

public enum FitnessObjective { Minimize = -1, Maximize = 1 }

public enum BoundaryCondition
{
    /// <summary>
    /// Clamped Boundary Condition.
    /// If a value exceeds the system boundaries,
    /// it will be placed back at the exceeding point.
    /// </summary>
    CBC,
    /// <summary>
    /// Periodic Boundary Condition.
    /// If a value exceeds the system boundaries, 
    /// it will be placed back inside the domain
    /// at a position which is equally-distanced to the boundary as the exceeding point,
    /// but the entrance is made in the same direction, from the other corresponding end of the system.
    /// </summary>
    PBC,
    /// <summary>
    /// Reflective Boundary Condition.
    /// If a value exceeds the system boundaries,
    /// it will be placed back inside the domain
    /// at a position which is equally-distanced to the boundary as the exceeding point,
    /// but in an opposite direction.
    /// </summary>
    RBC
}


public class Colony
{
    private readonly Xoshiro256StarStar  _rng;

    private double[]? _frontBuffer;
    private double[]? _backBuffer;

    private double[]? _solution;

    private double _totalFitness;
    private double _limit;

    private int _sourceSize;
    private int _fitnessOffset;
    private int _trialsOffset;


    /// <summary>
    /// The number of food sources (candidate solutions), and employed bees that exploit those sources.
    /// </summary>
    public int Size { get; init; } = 10;

    /// <summary>
    /// The dimensions of a food source position.
    /// Represent the number of objective function parameters.
    /// </summary>
    public int Dimensions { get; init; } = 2;

    /// <summary>
    /// The number of iterations in the colony simulation.
    /// </summary>
    public int Cycles { get; init; } = 20;

    /// <summary>
    /// Indicates whether the objective of the colony is to minimize or maximize the fitness value of a solution.
    /// </summary>
    public FitnessObjective FitnessObjective { get; init; } = FitnessObjective.Minimize;
    /// <summary>
    /// Indicates how out-of-bound values of are treated.
    /// </summary>
    public BoundaryCondition BoundaryCondition { get; init; } = BoundaryCondition.RBC;

    /// <summary>
    /// The lower boundary for a value of a parameter in a solution.
    /// </summary>
    public double MinValue { get; init; } = -1.0;

    /// <summary>
    /// The upper boundary for a value of a parameter in a solution.
    /// </summary>
    public double MaxValue { get; init; } = 1.0;

    public ReadOnlySpan<double> GetSource(int sourceIndex) => FrontBufferSource(sourceIndex).Slice(0, Dimensions);
    public double GetFitness(int sourceIndex) => FrontBufferSource(sourceIndex)[_fitnessOffset];
    public double GetTrialCount(int sourceIndex) => FrontBufferSource(sourceIndex)[_trialsOffset];

    public ReadOnlySpan<double> Solution => _solution.AsSpan(0, Dimensions);
    public double SolutionFitness => _solution.AsSpan()[_fitnessOffset];

    public ObjectiveFunction ObjectiveFunction { get; init; } = (source) =>
    {
        throw new NotImplementedException();
    };

    public Colony(ulong seed)
    {
        _rng = new Xoshiro256StarStar(seed);
    }

    private void ValidateParameters()
    {
        if (MinValue >= MaxValue)
            throw new Exception($"{nameof(MaxValue)} must be greater then {nameof(MinValue)}.");

        if (Size <= 1)
            throw new Exception($"{nameof(Size)} must be greater then 1.");

        if (Dimensions <= 0)
            throw new Exception($"{nameof(Dimensions)} must be greater then 0.");

        if (Cycles <= 0)
            throw new Exception($"{nameof(Cycles)} must be greater then 0.");

        if (!Enum.IsDefined(FitnessObjective))
            throw new Exception($"{nameof(FitnessObjective)} has an undefined value.");

        if (!Enum.IsDefined(BoundaryCondition))
            throw new Exception($"{nameof(BoundaryCondition)} has an undefined value.");
    }

    bool _running = false;
    public void Run()
    {
        if (_running)
            return;

        _running = true;

        ValidateParameters();

        // init
        _sourceSize = Dimensions + 2;
        _fitnessOffset = Dimensions;
        _trialsOffset = _fitnessOffset + 1;

        _limit = Size * Dimensions;

        _frontBuffer = new double[_sourceSize * Size];
        _backBuffer = new double[_sourceSize * Size];
        _solution = new double[_sourceSize];
        _solution[_fitnessOffset] = Fitness(_solution);

        // initialize sources with random values
        for (int i = 0; i < Size; i++)
            GenerateRandomSource(i);
        SwapFrontAndBackBuffers();

        // colony simulation
        for (int i = 0; i < Cycles; i++)
        {
            EmployedBeePhase();
            OnLookerBeePhase();
            ScoutBeePhase();
        }

        //_frontBuffer = Array.Empty<double>();
        _backBuffer = Array.Empty<double>();

        _running = false;
    }

    private void EmployedBeePhase()
    {
        // the sum of fitness values is needed for
        // calculating the probability value of sources
        // in the next onlooker bee phase.
        _totalFitness = 0.0;

        for (int i = 0; i < Size; i++)
        {
            ExploreNearbySource(i);

            _totalFitness += BackBufferSource(i)[_fitnessOffset]; // TODO: not thread safe
        }

        SwapFrontAndBackBuffers();
    }

    private void OnLookerBeePhase()
    {
        for (int i = 0; i < Size; i++)
        {
            var currentSource = FrontBufferSource(i);
            var outputSource = BackBufferSource(i);

            double probability = currentSource[_fitnessOffset] / _totalFitness;

            double random = _rng.NextDouble();

            if (random <= probability)
                ExploreNearbySource(i);
            else
                currentSource.CopyTo(outputSource);

            MemorizeIfBestSource(outputSource);
        }

        SwapFrontAndBackBuffers();
    }

    private void ScoutBeePhase()
    {
        for (int i = 0; i < Size; i++)
        {
            var currentSource = FrontBufferSource(i);

            if (currentSource[_trialsOffset] > _limit)
                GenerateRandomSource(i);
            else
                currentSource.CopyTo(BackBufferSource(i));
        }

        SwapFrontAndBackBuffers();
    }

    private void GenerateRandomSource(int i)
    {
        var output = BackBufferSource(i);
        
        // generate a value for each dimension using the formula:
        // Xij = Xmin.j + rand[0,1] * (Xmax.j - Xmin.j)
        for (int j = 0; j < Dimensions; j++)
            output[j] = MinValue + _rng.NextDouble() * (MaxValue - MinValue);

        output[_fitnessOffset] = Fitness(output);
        output[_trialsOffset] = 0.0;
    }

    private void ExploreNearbySource(int i)
    {
        // pick a random source (k) that is different from the current source (i)
        int k;
        do
            k = _rng.Next(Size);
        while (k == i);

        var currentSource = FrontBufferSource(i);
        var randomSource = FrontBufferSource(k);

        // pick a random dimension (j)
        int j = _rng.Next(Dimensions);

        // value of (j) dimension of the current source (i)
        double Xij = currentSource[j];

        // value of (j) dimension of the random source (k)
        double Xkj = randomSource[j];

        // pick a random scaling factor between -1 and +1
        double rand = _rng.NextDouble() * 2.0 - 1.0;

        // generate a new value for the (j) dimension
        double Vij = Xij + rand * (Xij - Xkj);

        // generate a new source from the current source (i)
        var output = BackBufferSource(i);
        currentSource.CopyTo(output);

        switch (BoundaryCondition)
        {
            case BoundaryCondition.CBC:
                output[j] = BeeMath.CBC(Vij, MinValue, MaxValue);
                break;
            case BoundaryCondition.PBC:
                output[j] = BeeMath.PBC(Vij, MinValue, MaxValue);
                break;
            case BoundaryCondition.RBC:
                output[j] = BeeMath.RBC(Vij, MinValue, MaxValue);
                break;
            default:
                output[j] = Vij;
                break;
        }

        output[_fitnessOffset] = Fitness(output);
        output[_trialsOffset] = 0.0;

        // greedy selection
        if (CompareFitness(currentSource[_fitnessOffset], output[_fitnessOffset]) >= 0)
        {
            currentSource.CopyTo(output);
            output[_trialsOffset]++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void MemorizeIfBestSource(Span<double> source)
    {
        Span<double> bestSource = _solution;

        if (CompareFitness(bestSource[_fitnessOffset], source[_fitnessOffset]) < 0)
            source.CopyTo(bestSource);  // TODO: not thread safe
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double Fitness(Span<double> source)
    {
        double objectiveValue = ObjectiveFunction(source.Slice(0, Dimensions));

        return BeeMath.Fitness(objectiveValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CompareFitness(double a, double b)
    {
        return (int)FitnessObjective * (a.CompareTo(b));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<double> FrontBufferSource(int index)
    {
        return new ReadOnlySpan<double>(_frontBuffer, index * _sourceSize, _sourceSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Span<double> BackBufferSource(int index)
    {
        return new Span<double>(_backBuffer, index * _sourceSize, _sourceSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SwapFrontAndBackBuffers()
    {
        (_frontBuffer, _backBuffer) = (_backBuffer, _frontBuffer);
    }
}
