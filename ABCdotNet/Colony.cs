using System;
using System.Runtime.CompilerServices;
using Redzen.Random;

namespace ABCdotNet;

public delegate double ObjectiveFunction(ReadOnlySpan<double> source);

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

    private double[] _frontBuffer;
    private double[] _backBuffer;

    private double[] _solution;

    private double _totalFitness;
    private double _limit;

    private int _sourceSize;
    private uint _sourceSizeInBytes;
    private int _fitnessOffset;
    private int _trialsOffset;


    /// <summary>
    /// The number of food sources (candidate solutions),
    /// and employed bees that exploit those sources.
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


    public ReadOnlySpan<double> GetSource(int sourceIndex) => GetSource(_frontBuffer, sourceIndex);
    public double GetFitness(int sourceIndex) => _frontBuffer[ItemIndex(sourceIndex, _fitnessOffset)];
    public double GetTrialCount(int sourceIndex) => _frontBuffer[ItemIndex(sourceIndex, _trialsOffset)];

    public ReadOnlySpan<double> Solution => _solution.AsSpan(0, Dimensions);
    public double SolutionFitness => _solution.AsSpan()[_fitnessOffset];

    public ObjectiveFunction ObjectiveFunction { get; init; } = (source) =>
    {
        throw new NotImplementedException();
    };

    public Colony(ulong seed)
    {
        _rng = new Xoshiro256StarStar(seed);

        _frontBuffer = _backBuffer = _solution = Array.Empty<double>();
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
        _sourceSizeInBytes = (uint)_sourceSize * sizeof(double);
        _fitnessOffset = Dimensions;
        _trialsOffset = _fitnessOffset + 1;

        _limit = Size * Dimensions;

        _frontBuffer = new double[_sourceSize * Size];
        _backBuffer = new double[_sourceSize * Size];
        _solution = new double[_sourceSize];
        _solution[_fitnessOffset] = Fitness(_solution, 0);

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

            _totalFitness += _backBuffer[ItemIndex(i, _fitnessOffset)];
        }

        SwapFrontAndBackBuffers();
    }

    private void OnLookerBeePhase()
    {
        for (int i = 0; i < Size; i++)
        {
            double fitness = _frontBuffer[ItemIndex(i, _fitnessOffset)];

            double probability = fitness / _totalFitness;

            double random = _rng.NextDouble();

            if (random <= probability)
                ExploreNearbySource(i);
            else
                CopySourceFromFrontToBackBuffer(i);

            MemorizeIfBestSource(i);
        }

        SwapFrontAndBackBuffers();
    }

    private void ScoutBeePhase()
    {
        for (int i = 0; i < Size; i++)
        {
            double trials = _frontBuffer[ItemIndex(i, _trialsOffset)];

            if (trials > _limit)
                GenerateRandomSource(i);
            else
                CopySourceFromFrontToBackBuffer(i);
        }

        SwapFrontAndBackBuffers();
    }

    private void GenerateRandomSource(int sourceIndex)
    {
        // generate a value for each dimension using the formula:
        // Xij = Xmin.j + rand[0,1] * (Xmax.j - Xmin.j)
        for (int j = 0; j < Dimensions; j++)
            _backBuffer[ItemIndex(sourceIndex, j)] = MinValue + _rng.NextDouble() * (MaxValue - MinValue);

        _backBuffer[ItemIndex(sourceIndex, _fitnessOffset)] = Fitness(_backBuffer, sourceIndex);
        _backBuffer[ItemIndex(sourceIndex, _trialsOffset)] = 0.0;
    }

    private void ExploreNearbySource(int i)
    {
        // pick a random source (k) that is different from the current source (i)
        int k;
        do
            k = _rng.Next(Size);
        while (k == i);

        // pick a random dimension (j)
        int j = _rng.Next(Dimensions);

        // value of (j) dimension of the current source (i)
        double Xij = _frontBuffer[ItemIndex(i, j)];

        // value of (j) dimension of the random source (k)
        double Xkj = _frontBuffer[ItemIndex(k, j)];

        // pick a random scaling factor between -1 and +1
        double rand = _rng.NextDouble() * 2.0 - 1.0;

        // generate a new value for the (j) dimension
        double Vij = Xij + rand * (Xij - Xkj);

        // generate a new source from the current source (i)
        CopySourceFromFrontToBackBuffer(i);

        switch (BoundaryCondition)
        {
            case BoundaryCondition.CBC:
                Vij = BeeMath.CBC(Vij, MinValue, MaxValue);
                break;
            case BoundaryCondition.PBC:
                Vij = BeeMath.PBC(Vij, MinValue, MaxValue);
                break;
            case BoundaryCondition.RBC:
                Vij = BeeMath.RBC(Vij, MinValue, MaxValue);
                break;
            default:
                break;
        }

        _backBuffer[ItemIndex(i, j)] = Vij;

        double fitness = Fitness(_backBuffer, i);
        int fitnessIndex = ItemIndex(i, _fitnessOffset);

        _backBuffer[fitnessIndex] = fitness;

        int trialsIndex = ItemIndex(i, _trialsOffset);

        _backBuffer[trialsIndex] = 0.0;

        // greedy selection
        if (CompareFitness(_frontBuffer[fitnessIndex], fitness) >= 0)
        {
            CopySourceFromFrontToBackBuffer(i);
            _backBuffer[trialsIndex]++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void MemorizeIfBestSource(int sourceIndex)
    {
        if (CompareFitness(_solution[_fitnessOffset], _backBuffer[ItemIndex(sourceIndex, _fitnessOffset)]) < 0)
            Unsafe.CopyBlock(
                ref Unsafe.As<double, byte>(ref _solution[0]),
                ref Unsafe.As<double, byte>(ref _backBuffer[ItemIndex(sourceIndex, 0)]),
                _sourceSizeInBytes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double Fitness(double[] buffer, int sourceIndex)
    {
        double objectiveValue = ObjectiveFunction(GetSource(buffer, sourceIndex));

        return BeeMath.Fitness(objectiveValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CompareFitness(double a, double b)
    {
        return (int)FitnessObjective * (a.CompareTo(b));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ItemIndex(int sourceIndex, int offset)
    {
        return sourceIndex * _sourceSize + offset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CopySourceFromFrontToBackBuffer(int sourceIndex)
    {
        int index = ItemIndex(sourceIndex, 0);

        Unsafe.CopyBlock(
            ref Unsafe.As<double, byte>(ref _backBuffer[index]),
            ref Unsafe.As<double, byte>(ref _frontBuffer[index]),
            _sourceSizeInBytes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<double> GetSource(double[] buffer, int sourceIndex)
    {
        return new ReadOnlySpan<double>(buffer, sourceIndex * _sourceSize, Dimensions);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SwapFrontAndBackBuffers()
    {
        (_frontBuffer, _backBuffer) = (_backBuffer, _frontBuffer);
    }
}
