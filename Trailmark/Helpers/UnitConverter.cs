using Trailmark.Models;

namespace Trailmark.Helpers;

public static class UnitConverter
{
    private const double MetersPerMile = 1_609.344;
    private const double FeetPerMeter = 3.280839895;

    public static double ConvertDistance(double meters, DistanceUnit unit) =>
        unit == DistanceUnit.Miles ? meters / MetersPerMile : meters / 1_000d;

    public static double ConvertElevation(double meters, ElevationUnit unit) =>
        unit == ElevationUnit.Feet ? meters * FeetPerMeter : meters;
}
