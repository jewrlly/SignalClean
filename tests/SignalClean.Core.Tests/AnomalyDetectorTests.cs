using SignalClean.Core.Processing;

namespace SignalClean.Core.Tests;

public class AnomalyDetectorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsSpike_DetectsLargeDeviation()
    {
        var previous = new[] { 20.0, 20.1, 19.9, 20.0, 20.2 };

        bool spike = AnomalyDetector.IsSpike(previous, 35, nSigma: 4, minAbsoluteDeviation: 1, out double expected);

        Assert.True(spike);
        Assert.Equal(20.0, expected, 9);
    }

    [Fact]
    public void IsSpike_IgnoresNormalNoise()
    {
        var previous = new[] { 20.0, 20.1, 19.9, 20.0, 20.2 };

        Assert.False(AnomalyDetector.IsSpike(previous, 20.3, 4, 1, out _));
    }

    [Fact]
    public void IsSpike_ConstantHistory_UsesAbsoluteThreshold()
    {
        // MAD = 0: без мінімального порогу будь-яка зміна вважалась би сплеском.
        var previous = new[] { 20.0, 20.0, 20.0, 20.0 };

        Assert.False(AnomalyDetector.IsSpike(previous, 20.5, 4, 1, out _));
        Assert.True(AnomalyDetector.IsSpike(previous, 25, 4, 1, out _));
    }

    [Fact]
    public void IsSpike_TooShortHistory_ReturnsFalse()
    {
        Assert.False(AnomalyDetector.IsSpike(new[] { 1.0, 2.0 }, 100, 4, 1, out double expected));
        Assert.Equal(100, expected);
    }

    [Theory]
    [InlineData(new[] { 1.0, 2.0, 3.0 }, 1)]
    [InlineData(new[] { 1.0, 5.0, 5.0, 5.0 }, 3)]
    [InlineData(new[] { 7.0, 7.0 }, 2)]
    public void TrailingRunLength_CountsEqualTail(double[] values, int expected)
    {
        Assert.Equal(expected, AnomalyDetector.TrailingRunLength(values));
    }

    [Fact]
    public void TrailingRunLength_Empty_IsZero()
    {
        Assert.Equal(0, AnomalyDetector.TrailingRunLength(Array.Empty<double>()));
    }

    [Fact]
    public void IsGap_DetectsLongPause()
    {
        var timestamps = new[] { T0, T0.AddSeconds(1), T0.AddSeconds(2), T0.AddSeconds(3) };

        Assert.True(AnomalyDetector.IsGap(timestamps, T0.AddSeconds(10), 3, out var interval));
        Assert.Equal(TimeSpan.FromSeconds(7), interval);
    }

    [Fact]
    public void IsGap_RegularInterval_NoGap()
    {
        var timestamps = new[] { T0, T0.AddSeconds(1), T0.AddSeconds(2), T0.AddSeconds(3) };

        Assert.False(AnomalyDetector.IsGap(timestamps, T0.AddSeconds(4), 3, out _));
    }

    [Fact]
    public void IsGap_ShortHistory_NoGap()
    {
        Assert.False(AnomalyDetector.IsGap(new[] { T0 }, T0.AddHours(1), 3, out _));
    }
}
