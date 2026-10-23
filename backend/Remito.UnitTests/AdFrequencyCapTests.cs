using Remito.Application.Engagement;

namespace Remito.UnitTests;

public sealed class AdFrequencyCapTests
{
    [Theory]
    [InlineData(0, 3, false)]
    [InlineData(2, 3, false)]
    [InlineData(3, 3, true)]
    [InlineData(8, 3, true)]
    public void Cap_is_reached_at_or_above_the_configured_impression_count(
        int impressionCount,
        int dailyCap,
        bool expected)
    {
        Assert.Equal(expected, AdFrequencyCap.HasReached(impressionCount, dailyCap));
    }
}
