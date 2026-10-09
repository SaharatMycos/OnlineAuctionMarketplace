using Marketplace.SharedKernel.Events;
using Marketplace.SharedKernel.Geo;

namespace Marketplace.UnitTests.Location;

/// <summary>Location privacy (feature design §9.2): fixed ~1 km cells, rounded distances, masked handles.</summary>
public class PublicGridTests
{
    [Fact]
    public void Snapping_is_deterministic_and_stays_within_about_a_kilometre()
    {
        var a = PublicGrid.Snap(41.909123, -87.677456);
        var b = PublicGrid.Snap(41.909123, -87.677456);
        Assert.Equal(a.X, b.X);
        Assert.Equal(a.Y, b.Y);

        // Half a cell diagonal is ~0.8 km at Chicago's latitude.
        Assert.True(Math.Abs(a.Y - 41.909123) <= PublicGrid.CellDegrees);
        Assert.True(Math.Abs(a.X - -87.677456) <= PublicGrid.CellDegrees * 1.5);
    }

    [Fact]
    public void Nearby_points_in_the_same_cell_share_a_public_point()
    {
        var centre = PublicGrid.Snap(41.909123, -87.677456);
        foreach (var (dLat, dLng) in new[] { (0.002, 0.002), (-0.002, 0.003), (0.004, -0.004) })
        {
            var nearby = PublicGrid.Snap(centre.Y + dLat, centre.X + dLng);
            Assert.Equal((centre.X, centre.Y), (nearby.X, nearby.Y));
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(999, 0)]
    [InlineData(1000, 1)]
    [InlineData(1499, 1)]
    [InlineData(1500, 2)]
    [InlineData(24_600, 25)]
    public void Distances_are_rounded_to_whole_km(double metres, int km) =>
        Assert.Equal(km, PublicGrid.RoundDistanceKm(metres));

    [Theory]
    [InlineData("alice", "a***e")]
    [InlineData("bo", "b***")]
    [InlineData("x", "x***")]
    public void Handles_are_masked(string handle, string masked) =>
        Assert.Equal(masked, Handles.Mask(handle));
}
