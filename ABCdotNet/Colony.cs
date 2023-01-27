using System;
using System.Runtime.CompilerServices;
using Redzen.Random;

namespace ABCdotNet;

public delegate double ObjectiveFunction(ReadOnlySpan<double> source);

public enum FitnessObjective { Minimize, Maximize }

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

public sealed class Colony
{
    private double[] _frontBuffer;
    private double[] _backBuffer;

    private readonly double[] _solution;

    private readonly ColonySettings _settings;
    private readonly Xoshiro256StarStar _rng;
    private readonly int _sourceSize;
    private readonly uint _sourceSizeInBytes;
    private readonly int _fitnessOffset;
    private readonly int _trialsOffset;


    public ColonySettings Settings => _settings;

    public ReadOnlySpan<double> Solution => _solution.AsSpan(0, _settings.Dimensions);

    public double SolutionFitness => _solution.AsSpan()[_fitnessOffset];

    public ReadOnlySpan<double> Source(int sourceIndex) => SourceFromBuffer(_frontBuffer, sourceIndex);
    public double Fitness(int sourceIndex) => _frontBuffer[ItemIndex(sourceIndex, _fitnessOffset)];
    public double TrialCount(int sourceIndex) => _frontBuffer[ItemIndex(sourceIndex, _trialsOffset)];

    public Colony(ColonySettings settings)
    {
        _settings = settings;

        _rng = new Xoshiro256StarStar(settings.Seed);
        _sourceSize = _settings.Dimensions + 2;
        _sourceSizeInBytes = (uint)_sourceSize * sizeof(double);
        _fitnessOffset = _settings.Dimensions;
        _trialsOffset = _fitnessOffset + 1;

        _frontBuffer = new double[_sourceSize * _settings.Size];
        _backBuffer = new double[_sourceSize * _settings.Size];
        _solution = new double[_sourceSize];
    }

    public void Run()
    {
        // initialize solution with a valid value
        for (int i = 0; i < _settings.Dimensions; i++)
            _solution[i] = BeeMath.CBC(
                _solution[i],
                _settings.Constraints[i].MinValue,
                _settings.Constraints[i].MaxValue);
        _solution[_fitnessOffset] = Fitness(SourceFromBuffer(_solution, 0));

        // initialize sources with random values
        for (int i = 0; i < _settings.Size; i++)
            GenerateRandomSource(i);
        SwapFrontAndBackBuffers();

        double limit = _settings.Size * _settings.Dimensions;

        // colony simulation
        int cycle = 0;
        while (cycle++ < _settings.Cycles)
        {
            double totalFitness = 0.0;

            // employed bee
            for (int i = 0; i < _settings.Size; i++)
            {
                ExploreNearbySource(i);
                totalFitness += _backBuffer[ItemIndex(i, _fitnessOffset)];
            }

            SwapFrontAndBackBuffers();

            // onlooker and scout bees
            for (int i = 0; i < _settings.Size; i++)
            {
                double trials = _frontBuffer[ItemIndex(i, _trialsOffset)];
                double fitness = _frontBuffer[ItemIndex(i, _fitnessOffset)];
                double probability = fitness / totalFitness;
                double random = _rng.NextDouble();

                if (random <= probability)
                {
                    if (!ExploreNearbySource(i) && trials++ > limit)
                        GenerateRandomSource(i);
                }
                else if (trials > limit)
                    GenerateRandomSource(i);
                else
                    CopySourceFromFrontToBackBuffer(i);
            }

            SwapFrontAndBackBuffers();
        }
    }

    private void GenerateRandomSource(int sourceIndex)
    {
        // generate a random value for each dimension using the formula:
        // Xij = Xmin.j + rand[0,1] * (Xmax.j - Xmin.j)
        for (int j = 0; j < _settings.Dimensions; j++)
        {
            double min = _settings.Constraints[j].MinValue;
            double max = _settings.Constraints[j].MaxValue;
            _backBuffer[ItemIndex(sourceIndex, j)] = min + _rng.NextDouble() * (max - min);
        }

        // calculate fitness and set trials to 0
        _backBuffer[ItemIndex(sourceIndex, _fitnessOffset)] = Fitness(SourceFromBuffer(_backBuffer, sourceIndex));
        _backBuffer[ItemIndex(sourceIndex, _trialsOffset)] = 0.0;
    }

    private bool ExploreNearbySource(int i)
    {
        // pick a random source (k) that is different from the current source (i)
        int k;
        do
            k = _rng.NextInt(_settings.Size);
        while (k == i);

        // pick a random dimension (j)
        int j = _rng.NextInt(_settings.Dimensions);

        int ijIndex = ItemIndex(i, j);
        int kjIndex = ItemIndex(k, j);

        // value of (j) dimension of the current source (i)
        double Xij = _frontBuffer[ijIndex];

        // value of (j) dimension of the random source (k)
        double Xkj = _frontBuffer[kjIndex];

        // pick a random scaling factor between -1 and +1
        double rand = _rng.NextDouble() * 2.0 - 1.0;

        // generate a new value for the (j) dimension
        double Vij = Xij + rand * (Xij - Xkj);

        double min = _settings.Constraints[j].MinValue;
        double max = _settings.Constraints[j].MaxValue;

        switch (_settings.BoundaryCondition)
        {
            case BoundaryCondition.CBC:
                Vij = BeeMath.CBC(Vij, min, max);
                break;
            case BoundaryCondition.PBC:
                Vij = BeeMath.PBC(Vij, min, max);
                break;
            case BoundaryCondition.RBC:
                Vij = BeeMath.RBC(Vij, min, max);
                break;
            default:
                break;
        }

        CopySourceFromFrontToBackBuffer(i);
        _backBuffer[ijIndex] = Vij;

        int fitnessIndex = ItemIndex(i, _fitnessOffset);
        int trialsIndex = ItemIndex(i, _trialsOffset);

        double fitness = Fitness(SourceFromBuffer(_backBuffer, i));

        if (BetterFitness(fitness, _frontBuffer[fitnessIndex]))
        {
            _backBuffer[fitnessIndex] = fitness;
            _backBuffer[trialsIndex] = 0.0;

            // memorize best source
            if (BetterFitness(fitness, _solution[_fitnessOffset]))
            {
                Unsafe.CopyBlock(
                    ref Unsafe.As<double, byte>(ref _solution[0]),
                    ref Unsafe.As<double, byte>(ref _backBuffer[ItemIndex(i)]),
                    _sourceSizeInBytes);
            }

            return true;
        }
        else
        {
            _backBuffer[ijIndex] = _frontBuffer[ijIndex];
            _backBuffer[trialsIndex]++;

            return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double Fitness(ReadOnlySpan<double> source)
    {
        double objectiveValue = _settings.ObjectiveFunction(source);

        return BeeMath.Fitness(objectiveValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool BetterFitness(double a, double b)
    {
        return (a > b) ^ (_settings.FitnessObjective == FitnessObjective.Minimize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ItemIndex(int sourceIndex, int offset)
    {
        return sourceIndex * _sourceSize + offset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ItemIndex(int sourceIndex)
    {
        return sourceIndex * _sourceSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<double> SourceFromBuffer(double[] buffer, int sourceIndex)
    {
        return new ReadOnlySpan<double>(buffer, sourceIndex * _sourceSize, _settings.Dimensions);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CopySourceFromFrontToBackBuffer(int sourceIndex)
    {
        int index = ItemIndex(sourceIndex);

        Unsafe.CopyBlockUnaligned(
            ref Unsafe.As<double, byte>(ref _backBuffer[index]),
            ref Unsafe.As<double, byte>(ref _frontBuffer[index]),
            _sourceSizeInBytes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SwapFrontAndBackBuffers()
    {
        (_frontBuffer, _backBuffer) = (_backBuffer, _frontBuffer);
    }
}
