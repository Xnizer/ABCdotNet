using ABCdotNet;

namespace Example
{
    class Program
    {
        static void Main()
        {
            ColonySettings settings = ColonySettings.CreateBuilder()
                .SetSeed(1337)
                .SetDimensions(2)
                .SetSize(10)
                .SetCycles(100)
                .SetFitnessObjective(FitnessObjective.Maximize)
                .SetBoundaryCondition(BoundaryCondition.RBC)

                .SetConstraints((-100, 100))

                .SetObjectiveFunction((source) =>
                {
                    double x = source[0];
                    double y = source[1];

                    return
                        x * x -
                        x * y +
                        y * y +
                        2.0 * x +
                        4.0 * y +
                        3.0;
                })

                .Build();

            Colony colony = new Colony(settings);

            colony.Run();

            ColonyPrinter.Print(colony);
        }
    }
}
