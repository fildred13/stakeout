namespace Stakeout.Simulation.City;

public struct Cell
{
    public PlotType PlotType { get; set; }
    public int? AddressId { get; set; }
    public int? StreetId { get; set; }
    public FacingDirection FacingDirection { get; set; }
}
