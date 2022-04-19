using ABCdotNet;
using System;

namespace Example
{
    internal static class ColonyPrinter
    {
        public static void Print(Colony colony)
        {
            Console.WriteLine($"{"Source:",-36}{"Fitness:",-20}{"Trials:"}");

            for (int i = 0; i < colony.Size; i++)
                PrintSource(colony.GetSource(i), colony.FitnessValues[i], colony.Trials[i]);

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            PrintSource(colony.Solution, colony.SolutionFitness, 0);
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        private static void PrintSource(ReadOnlySpan<double> source, double fitness, int trials)
        {
            string str = "";

            for (int j = 0; j < 4; j++)
            {
                if (source.Length > j)
                {
                    str += $"{source[j],6:F3}";
                    if (source.Length > j + 1)
                        str += " / ";
                    else
                        str += "   ";
                }
                else
                    str += "         ";
            }

            Console.WriteLine($"{str}{fitness,-20:F12}{trials}");
        }
    }
}
