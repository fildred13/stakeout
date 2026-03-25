namespace Stakeout.Simulation.Crimes;

public interface ICrimeTemplate
{
    CrimeTemplateType Type { get; }
    string Name { get; }
    Crime Instantiate(SimulationState state);
}
