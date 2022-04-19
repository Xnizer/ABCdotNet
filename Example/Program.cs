using ABCdotNet;
using System;

namespace Example
{
    class Program
    {
        static void Main()
        {
            Colony colony = new Colony(0)
            {
                MinValue = -5,
                MaxValue = 5,
                Dimensions = 2,
                Size = 10,
                Cycles = 100,
                FitnessObjective = FitnessObjective.Maximize,
                BoundaryCondition = BoundaryCondition.RBC,
                ObjectiveFunction = (source) =>
                {
                    double x = source[0];
                    double y = source[1];

                    return
                        Math.Pow(x, 2) -
                        x * y +
                        Math.Pow(y, 2) +
                        2 * x +
                        4 * y +
                        3;
                }
            };

            colony.Run();

            ColonyPrinter.Print(colony);
        }
    }
}
