using SignalClean.Core.Processing;

namespace SignalClean.Core.Tests;

public class SignalProcessorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = T0.AddHours(1);

    private static List<HistoryPoint> History(params double[] values) =>
        values.Select((v, i) => new HistoryPoint(T0.AddSeconds(i), v)).ToList();

    private static RawPoint At(int second, double? value) => new(T0.AddSeconds(second), value);

    private readonly SignalProcessor _processor = new();

    [Fact]
    public void NormalValue_StaysValid()
    {
        var result = _processor.Process(History(20, 20.1, 19.9, 20, 20.2), new[] { At(5, 20.1) }, -40, 85, Now);

        var p = Assert.Single(result);
        Assert.Equal(ReadingStatus.Valid, p.Status);
        Assert.Equal(20.1, p.CleanValue);
        Assert.Empty(p.Anomalies);
    }

    [Fact]
    public void Spike_IsCorrectedToMedian()
    {
        var result = _processor.Process(History(20, 20.1, 19.9, 20, 20.2), new[] { At(5, 60) }, -40, 85, Now);

        var p = Assert.Single(result);
        Assert.Equal(ReadingStatus.Corrected, p.Status);
        Assert.Equal(60, p.RawValue);
        Assert.Equal(20.0, p.CleanValue!.Value, 9);
        Assert.Contains(p.Anomalies, a => a.Kind == AnomalyKind.Spike);
    }

    [Fact]
    public void OutOfRange_IsInvalid_WithoutCleanValue()
    {
        var result = _processor.Process(History(20, 20.1, 19.9), new[] { At(3, -999) }, -40, 85, Now);

        var p = Assert.Single(result);
        Assert.Equal(ReadingStatus.Invalid, p.Status);
        Assert.Null(p.CleanValue);
        Assert.Contains(p.Anomalies, a => a.Kind == AnomalyKind.OutOfRange);
    }

    [Fact]
    public void NaN_IsInvalid_AndAnomalyValueIsNull()
    {
        var result = _processor.Process(History(), new[] { At(0, double.NaN) }, -40, 85, Now);

        var anomaly = Assert.Single(Assert.Single(result).Anomalies);
        Assert.Equal(AnomalyKind.NotANumber, anomaly.Kind);
        Assert.Null(anomaly.Value);
    }

    [Fact]
    public void Missing_IsMarkedMissing()
    {
        var result = _processor.Process(History(20, 20, 20), new[] { At(3, null) }, -40, 85, Now);

        Assert.Equal(ReadingStatus.Missing, Assert.Single(result).Status);
    }

    [Fact]
    public void StuckSensor_ReportedOnceWhenRunReachesThreshold()
    {
        var options = new ProcessingOptions { StuckRun = 4 };
        var processor = new SignalProcessor(options);
        var points = Enumerable.Range(0, 8).Select(i => At(i, 21.5)).ToList();

        var result = processor.Process(History(), points, -40, 85, Now);

        var stuck = result.SelectMany(p => p.Anomalies).Where(a => a.Kind == AnomalyKind.Stuck).ToList();
        Assert.Single(stuck);
        Assert.Equal(T0.AddSeconds(3), stuck[0].Timestamp);
    }

    [Fact]
    public void Gap_IsDetected()
    {
        var result = _processor.Process(History(20, 20, 20, 20), new[] { At(30, 20) }, -40, 85, Now);

        Assert.Contains(Assert.Single(result).Anomalies, a => a.Kind == AnomalyKind.Gap);
    }

    [Fact]
    public void Points_AreProcessedInChronologicalOrder()
    {
        var result = _processor.Process(History(), new[] { At(2, 1), At(0, 1), At(1, 1) }, -40, 85, Now);

        Assert.Equal(new[] { T0, T0.AddSeconds(1), T0.AddSeconds(2) }, result.Select(p => p.Timestamp).ToArray());
    }

    [Fact]
    public void CorrectedValue_DoesNotPoisonHistory()
    {
        // Після сплеску наступне нормальне значення не повинно вважатися аномалією.
        var points = new[] { At(5, 80), At(6, 20.1) };

        var result = _processor.Process(History(20, 20.1, 19.9, 20, 20.2), points, -40, 85, Now);

        Assert.Equal(ReadingStatus.Corrected, result[0].Status);
        Assert.Equal(ReadingStatus.Valid, result[1].Status);
    }

    [Fact]
    public void InvalidOptions_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SignalProcessor(new ProcessingOptions { Window = 1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SignalProcessor(new ProcessingOptions { StuckRun = 1 }));
    }

    [Fact]
    public void NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => _processor.Process(null!, Array.Empty<RawPoint>(), 0, 1, Now));
        Assert.Throws<ArgumentNullException>(() => _processor.Process(History(), null!, 0, 1, Now));
    }
}
