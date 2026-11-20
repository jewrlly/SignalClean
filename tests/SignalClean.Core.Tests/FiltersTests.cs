using SignalClean.Core.Processing;

namespace SignalClean.Core.Tests;

public class FiltersTests
{
    [Theory]
    [InlineData(new[] { 5.0 }, 5.0)]
    [InlineData(new[] { 3.0, 1.0, 2.0 }, 2.0)]
    [InlineData(new[] { 4.0, 1.0, 3.0, 2.0 }, 2.5)]
    public void Median_ReturnsMiddleValue(double[] values, double expected)
    {
        Assert.Equal(expected, Filters.Median(values), 9);
    }

    [Fact]
    public void Median_DoesNotModifyInput()
    {
        var values = new[] { 3.0, 1.0, 2.0 };

        Filters.Median(values);

        Assert.Equal(new[] { 3.0, 1.0, 2.0 }, values);
    }

    [Fact]
    public void Median_EmptyInput_Throws()
    {
        Assert.Throws<ArgumentException>(() => Filters.Median(Array.Empty<double>()));
    }

    [Fact]
    public void Mad_IsRobustToOutlier()
    {
        // MAD майже не реагує на один великий викид, на відміну від стандартного відхилення.
        var mad = Filters.MedianAbsoluteDeviation(new[] { 1.0, 2.0, 3.0, 4.0, 1000.0 });

        Assert.Equal(1.0, mad, 9);
    }

    [Fact]
    public void MovingAverage_ComputesWindowedMean()
    {
        var result = Filters.MovingAverage(new[] { 1.0, 2.0, 3.0, 4.0 }, 2);

        Assert.Equal(new[] { 1.0, 1.5, 2.5, 3.5 }, result);
    }

    [Fact]
    public void MovingAverage_InvalidWindow_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Filters.MovingAverage(new[] { 1.0 }, 0));
    }

    [Fact]
    public void Hampel_ReplacesOutlierWithMedian()
    {
        var values = new[] { 10.0, 10.2, 9.9, 10.1, 50.0, 10.0, 9.8, 10.1 };

        var (cleaned, outliers) = Filters.Hampel(values, halfWindow: 3, nSigma: 3);

        Assert.True(outliers[4]);
        Assert.InRange(cleaned[4], 9.8, 10.2);
        Assert.Equal(1, outliers.Count(o => o));
    }

    [Fact]
    public void Hampel_ConstantSignal_HasNoOutliers()
    {
        var (cleaned, outliers) = Filters.Hampel(new[] { 5.0, 5.0, 5.0, 5.0, 5.0 });

        Assert.DoesNotContain(true, outliers);
        Assert.Equal(new[] { 5.0, 5.0, 5.0, 5.0, 5.0 }, cleaned);
    }
}
