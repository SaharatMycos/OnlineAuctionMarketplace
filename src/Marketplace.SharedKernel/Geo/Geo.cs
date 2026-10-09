using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace Marketplace.SharedKernel.Geo;

/// <summary>WGS 84 points (SRID 4326). Map columns with <see cref="ColumnType"/> so distances are in metres.</summary>
public static class GeoPoints
{
    public const string ColumnType = "geography (point, 4326)";

    private static readonly GeometryFactory Factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    public static Point Create(double latitude, double longitude)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
            throw ProblemException.BadRequest("invalid_coordinates", "Latitude must be -90..90 and longitude -180..180.");
        return Factory.CreatePoint(new Coordinate(longitude, latitude));
    }

    public static double Latitude(this Point point) => point.Y;
    public static double Longitude(this Point point) => point.X;
}

/// <summary>
/// Location privacy (feature design §9.2): public points are snapped to the centre of a ~1 km grid cell,
/// fixed at creation, so repeated queries can't average out the exact location. Distances shown to users
/// are computed from public points and rounded to whole km.
/// </summary>
public static class PublicGrid
{
    /// <summary>Cell height in degrees of latitude (~1.1 km).</summary>
    public const double CellDegrees = 0.01;

    public static Point Snap(double latitude, double longitude)
    {
        var lat = Math.Floor(latitude / CellDegrees) * CellDegrees + CellDegrees / 2;
        // Keep cells roughly square: widen longitude cells away from the equator.
        var lngCell = CellDegrees / Math.Max(Math.Cos(lat * Math.PI / 180), 0.1);
        var lng = Math.Floor(longitude / lngCell) * lngCell + lngCell / 2;
        return GeoPoints.Create(Math.Round(lat, 5), Math.Round(lng, 5));
    }

    /// <summary>Whole km for display; 0 means "under 1 km". Never show unrounded distances.</summary>
    public static int RoundDistanceKm(double metres) => metres < 1000 ? 0 : (int)Math.Round(metres / 1000, MidpointRounding.AwayFromZero);
}
