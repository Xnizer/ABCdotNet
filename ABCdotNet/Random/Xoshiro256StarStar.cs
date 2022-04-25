//
// The content of this file is taken from Redzen by Colin Green.
//
// Redzen : https://github.com/colgreen/Redzen
// Lisence: https://github.com/colgreen/Redzen/blob/master/LICENSE.txt
//
// ===========================================================================
//
// A C# port of the xorshiro256** pseudo random number generator (PRNG).
// Original C source code was obtained from:
//    http://xoshiro.di.unimi.it/xoshiro256starstar.c
//
// See original headers below for more info.
//
// ===========================================================================
//
// Written in 2018 by David Blackman and Sebastiano Vigna (vigna@acm.org)
//
// To the extent possible under law, the author has dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.

// See <http://creativecommons.org/publicdomain/zero/1.0/>. */
//
// --------
//
// This is xoshiro256** 1.0, our all-purpose, rock-solid generator. It has
// excellent (sub-ns) speed, a state space (256 bits) that is large enough
// for any parallel application, and it passes all tests we are aware of.
//
// For generating just floating-point numbers, xoshiro256+ is even faster.
//
// The state must be seeded so that it is not everywhere zero. If you have
// a 64-bit seed, we suggest to seed a splitmix64 generator and use its
// output to fill s.

using System.Numerics;

namespace Redzen.Random;

/// <summary>
/// Xoshiro256** (xor, shift, rotate) pseudo-random number generator (PRNG).
/// </summary>
internal sealed class Xoshiro256StarStar
{
    // Constants.
    const double INCR_DOUBLE = 1.0 / (1UL << 53);

    // RNG state.
    ulong _s0;
    ulong _s1;
    ulong _s2;
    ulong _s3;

    /// <summary>
    /// Initialises a new instance with the provided seed.
    /// </summary>
    /// <param name="seed">Seed value.</param>
    public Xoshiro256StarStar(ulong seed)
    {
        Reinitialise(seed);
    }

    /// <summary>
    /// Re-initialises the random number generator state using the provided seed.
    /// </summary>
    /// <param name="seed">Seed value.</param>
    public void Reinitialise(ulong seed)
    {
        // Notes.
        // The first random sample will be very strongly correlated to the value we give to the
        // state variables here; such a correlation is undesirable, therefore we significantly
        // weaken it by hashing the seed's bits using the splitmix64 PRNG.
        //
        // It is required that at least one of the state variables be non-zero;
        // use of splitmix64 satisfies this requirement because it is an equidistributed generator,
        // thus if it outputs a zero it will next produce a zero after a further 2^64 outputs.

        // Use the splitmix64 RNG to hash the seed.
        _s0 = Splitmix64Rng(ref seed);
        _s1 = Splitmix64Rng(ref seed);
        _s2 = Splitmix64Rng(ref seed);
        _s3 = Splitmix64Rng(ref seed);
    }

    /// <summary>
    /// Returns a random integer sampled from the uniform distribution with interval [0, maxValue),
    /// i.e., exclusive of <paramref name="maxValue"/>.
    /// </summary>
    /// <param name="maxValue">The maximum value to be sampled (exclusive).</param>
    /// <returns>A new random sample.</returns>
    public int Next(int maxValue)
    {
        // Notes.
        // Here we sample an integer value within the interval [0, maxValue). Rejection sampling is used in
        // order to produce unbiased samples. An alternative approach is:
        //
        //  return (int)(NextDoubleInner() * maxValue);
        //
        // I.e. generate a double precision float in the interval [0,1) and multiply by maxValue. However the
        // use of floating point arithmetic will introduce bias therefore this method is not used.
        //
        // The rejection sampling method used here operates as follows:
        //
        //  1) Calculate N such that  2^(N-1) < maxValue <= 2^N, i.e. N is the minimum number of bits required
        //     to represent maxValue states.
        //  2) Generate an N bit random sample.
        //  3) Reject samples that are >= maxValue, and goto (2) to re-sample.
        //
        // Repeat until a valid sample is generated.

        // Log2Ceiling(numberOfStates) gives the number of bits required to represent maxValue states.
        int bitCount = Log2Ceiling((uint)maxValue);

        // Rejection sampling loop.
        // Note. The expected number of samples per generated value is approx. 1.3862,
        // i.e. the number of loops, on average, assuming a random and uniformly distributed maxValue.
        int x;
        do
        {
            x = (int)(NextULong() >> (64 - bitCount));
        }
        while (x >= maxValue);

        return x;
    }

    /// <summary>
    /// Returns a random <see cref="double"/> sampled from the uniform distribution with interval [0, 1),
    /// i.e., inclusive of 0.0 and exclusive of 1.0.
    /// </summary>
    /// <returns>A new random sample, of type <see cref="double"/>.</returns>
    public double NextDouble()
    {
        // Notes.
        // Here we generate a random integer in the interval [0, 2^53-1]  (i.e. the max value is 53 binary 1s),
        // and multiply by the fractional value 1.0 / 2^53, thus the result has a min value of 0.0 and a max value of
        // 1.0 - (1.0 / 2^53), or 0.99999999999999989 in decimal.
        //
        // I.e. we break the interval [0,1) into 2^53 uniformly distributed discrete values, and thus the interval between
        // two adjacent values is 1.0 / 2^53. This increment is chosen because it is the smallest value at which each
        // distinct value in the full range (from 0.0 to 1.0 exclusive) can be represented directly by a double precision
        // float, and thus no rounding occurs in the representation of these values, which in turn ensures no bias in the
        // random samples.
        return (NextULong() >> 11) * INCR_DOUBLE;
    }

    /// <summary>
    /// Get the next 64 random bits from the underlying PRNG.
    /// </summary>
    /// <returns>A <see cref="ulong"/> containing random bits from the underlying PRNG algorithm.</returns>
    private ulong NextULong()
    {
        ulong s0 = _s0;
        ulong s1 = _s1;
        ulong s2 = _s2;
        ulong s3 = _s3;

        // Generate a new random sample.
        ulong result = BitOperations.RotateLeft(s1 * 5, 7) * 9;

        // Update PRNG state.
        ulong t = s1 << 17;
        s2 ^= s0;
        s3 ^= s1;
        s1 ^= s2;
        s0 ^= s3;
        s2 ^= t;
        s3 = BitOperations.RotateLeft(s3, 45);

        _s0 = s0;
        _s1 = s1;
        _s2 = s2;
        _s3 = s3;

        return result;
    }

    /// <summary>
    /// Splitmix64 PRNG.
    /// </summary>
    /// <param name="x">PRNG state. This can take any value, including zero.</param>
    /// <returns>A new random UInt64.</returns>
    private static ulong Splitmix64Rng(ref ulong x)
    {
        ulong z = (x += 0x9E3779B97F4A7C15UL);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>
    /// Evaluate the binary logarithm of a non-zero Int32, with rounding up of fractional results.
    /// I.e. returns the exponent of the smallest power of two that is greater than or equal to the specified number.
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <returns>The exponent of the smallest integral power of two that is greater than or equal to x.</returns>
    private static int Log2Ceiling(uint x)
    {
        // Log2(x) gives the required power of two, however this is integer Log2() therefore any fractional
        // part in the result is truncated, i.e., the result may be 1 too low. To compensate we add 1 if x
        // is not an exact power of two.
        int exp = BitOperations.Log2(x);

        // Return (exp + 1) if x is non-zero, and not an exact power of two.
        if (BitOperations.PopCount(x) > 1)
            exp++;

        return exp;
    }
}