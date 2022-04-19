using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

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
    private readonly Random _rng;

    private double[] _frontBuffer;
    private double[] _backBuffer;
    private double[] _fitness;
    private int[] _trials;

    private double[] _bestSource;
    private double _bestFitness;

    private double _fitnessSum = 0.0;

    public int Size { get; init; } = 10;
    public int Dimensions { get; init; } = 2;
    public int Cycles { get; init; } = 20;

    public FitnessObjective FitnessObjective { get; init; } = FitnessObjective.Minimize;
    public BoundaryCondition BoundaryCondition { get; init; } = BoundaryCondition.RBC;

    public double MinValue { get; init; } = -1.0;
    public double MaxValue { get; init; } = 1.0;

    public ReadOnlySpan<double> GetSource(int index) => GetSource(_frontBuffer, index);
    public ReadOnlySpan<double> FitnessValues => _fitness;
    public ReadOnlySpan<int> Trials => _trials;
    public ReadOnlySpan<double> Solution => _bestSource;
    public double SolutionFitness => _bestFitness;

    public ObjectiveFunction ObjectiveFunction { get; init; } = (source) =>
    {
        throw new NotImplementedException();
    };

    public Colony(int seed)
    {
        _rng = new Random(seed);

        _frontBuffer = _backBuffer = _fitness = _bestSource = Array.Empty<double>();
        _trials = Array.Empty<int>();
    }

    private void ValidateParameters()
    {
        if (MinValue >= MaxValue)
            throw new Exception($"{nameof(MaxValue)} must be greater then {nameof(MinValue)}.");

        if (Size <= 1)
            throw new Exception($"{nameof(Size)} must be greater then 0.");

        if (Dimensions <= 0)
            throw new Exception($"{nameof(Dimensions)} must be greater then 0.");

        if (Cycles <= 0)
            throw new Exception($"{nameof(Cycles)} must be greater then 0.");

        if (!Enum.IsDefined(FitnessObjective))
            throw new Exception($"{nameof(FitnessObjective)} has an undefined value.");

        if (!Enum.IsDefined(BoundaryCondition))
            throw new Exception($"{nameof(BoundaryCondition)} has an undefined value.");
    }

    private void InitBuffers()
    {
        _frontBuffer = new double[Size * Dimensions];
        _backBuffer = new double[Size * Dimensions];

        _bestSource = new double[Dimensions];

        _fitness = new double[Size];
        _trials = new int[Size];

        _bestFitness = FitnessObjective == FitnessObjective.Minimize ? double.MaxValue : double.MinValue;
    }

    bool _started = false;
    public void Run()
    {
        if (_started)
            throw new Exception();

        _started = true;

        ValidateParameters();

        InitBuffers();

        // generate initial random food sources 
        for (int i = 0; i < Size; i++)
        {
            Span<double> source = GetSource(_frontBuffer, i);

            GenerateRandomSource(source);
            _fitness[i] = Fitness(ObjectiveFunction(source));
            _trials[i] = 0;
        }

        for (int i = 0; i < Cycles; i++)
        {
            EmployedBeePhase();
            OnLookerBeePhase();
            ScoutBeePhase();
        }

        _backBuffer = Array.Empty<double>();
    }

    private void EmployedBeePhase()
    {
        Span<double> buffer = stackalloc double[Dimensions];

        // the sum of fitness values is needed for
        // calculating the probability value of sources
        // in the next onlooker bee phase.
        _fitnessSum = 0.0;

        for (int i = 0; i < Size; i++)
        {
            GenerateSource(buffer, i);
            GreedySelection(buffer, i);

            _fitnessSum += _fitness[i];
        }

        SwapFrontAndBackBuffers();
    }

    private void OnLookerBeePhase()
    {
        // consider looping through food sources again
        // until the number of updated sources
        // equals the number of onlooker bees

        Span<double> buffer = stackalloc double[Dimensions];

        for (int i = 0; i < Size; i++)
        {
            double probability = _fitness[i] / _fitnessSum;

            double random = _rng.NextDouble();

            if (random <= probability)
            {
                GenerateSource(buffer, i);
                GreedySelection(buffer, i);
            }
            else
                GetSource(_frontBuffer, i).CopyTo(GetSource(_backBuffer, i));
        }

        SwapFrontAndBackBuffers();

        MemorizeBestSource();
    }

    private void ScoutBeePhase()
    {
        int limit = (int)Math.Round(0.5 * Size * Dimensions);

        for (int i = 0; i < Size; i++)
        {
            if (_trials[i] > limit)
            {
                Span<double> source = GetSource(_frontBuffer, i);

                GenerateRandomSource(source);
                _fitness[i] = Fitness(ObjectiveFunction(source));
                _trials[i] = 0;
            }
        }
    }

    private void GenerateRandomSource(Span<double> buffer)
    {
        Debug.Assert(buffer.Length == Dimensions);

        for (int j = 0; j < Dimensions; j++)
            // Xij = Xmin.j + rand[0,1] * (Xmax.j - Xmin.j)
            buffer[j] = MinValue + _rng.NextDouble() * (MaxValue - MinValue);
    }

    private void GenerateSource(Span<double> buffer, int i)
    {
        Debug.Assert(buffer.Length == Dimensions);
        Debug.Assert(i >= 0 && i < Size);

        // pick a random dimension (j)
        int j = _rng.Next(Dimensions);

        // pick a random source (k) that is different from the current source (i)
        int k;
        do
            k = _rng.Next(Size);
        while (k == i);

        // value of (j) dimension of the current source (i)
        double Xij = _frontBuffer[i * Dimensions + j];

        // value of (j) dimension of the random source (k)
        double Xkj = _frontBuffer[k * Dimensions + j];

        // pick a random scaling factor between -1 and +1
        double rand = _rng.NextDouble() * 2 - 1;

        // generate a new value for the (j) dimension
        double Vij = Xij + rand * (Xij - Xkj);

        // generate a new source from the current source (i)
        GetSource(_frontBuffer, i).CopyTo(buffer);

        switch (BoundaryCondition)
        {
            case BoundaryCondition.CBC:
                buffer[j] = BeeMath.CBC(Vij, MinValue, MaxValue);
                break;
            case BoundaryCondition.PBC:
                buffer[j] = BeeMath.PBC(Vij, MinValue, MaxValue);
                break;
            case BoundaryCondition.RBC:
                buffer[j] = BeeMath.RBC(Vij, MinValue, MaxValue);
                break;
            default:
                buffer[j] = Vij;
                break;
        }
    }

    private void GreedySelection(Span<double> buffer, int i)
    {
        // the fitness value of the new food source
        double fitness = Fitness(ObjectiveFunction(buffer));

        // copy the new food source to the back buffer when it has better fitness
        if (CompareFitness(fitness, _fitness[i]) > 0)
        {
            buffer.CopyTo(GetSource(_backBuffer, i));
            _fitness[i] = fitness;
            _trials[i] = 0;
        }
        else
        {
            GetSource(_frontBuffer, i).CopyTo(GetSource(_backBuffer, i));
            _trials[i]++;
        }
    }

    private void MemorizeBestSource()
    {
        for (int i = 0; i < Size; i++)
        {
            if (CompareFitness(_fitness[i], _bestFitness) > 0)
            {
                GetSource(_frontBuffer, i).CopyTo(_bestSource);
                _bestFitness = _fitness[i];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Fitness(double f)
    {
        return f >= 0.0 ? 1.0 / (1.0 + f) : 1.0 + Math.Abs(f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CompareFitness(double newFitness, double oldFitness)
    {
        return (int)FitnessObjective * (newFitness.CompareTo(oldFitness));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Span<double> GetSource(double[] buffer, int index)
    {
        return buffer.AsSpan(index * Dimensions, Dimensions);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SwapFrontAndBackBuffers()
    {
        (_frontBuffer, _backBuffer) = (_backBuffer, _frontBuffer);
    }
}
