using SignalClean.Core.Quality;

namespace SignalClean.Core.Tests;

public class QualityScorerTests
{
    private static List<ReadingStatus> Statuses(int valid, int corrected = 0, int invalid = 0, int missing = 0) =>
        Enumerable.Repeat(ReadingStatus.Valid, valid)
            .Concat(Enumerable.Repeat(ReadingStatus.Corrected, corrected))
            .Concat(Enumerable.Repeat(ReadingStatus.Invalid, invalid))
            .Concat(Enumerable.Repeat(ReadingStatus.Missing, missing))
            .ToList();

    [Fact]
    public void PerfectData_Scores100_GradeA()
    {
        var report = QualityScorer.Compute("T", Statuses(100), 0);

        Assert.Equal(100, report.Score);
        Assert.Equal("A", report.Grade);
        Assert.Equal(1, report.Completeness);
        Assert.Equal(1, report.Validity);
    }

    [Fact]
    public void NoData_ReturnsNotApplicable()
    {
        var report = QualityScorer.Compute("T", Array.Empty<ReadingStatus>(), 0);

        Assert.Equal(0, report.Score);
        Assert.Equal("N/A", report.Grade);
        Assert.Equal(0, report.TotalReadings);
    }

    [Fact]
    public void MissingValues_ReduceCompleteness()
    {
        var report = QualityScorer.Compute("T", Statuses(valid: 90, missing: 10), 0);

        Assert.Equal(0.9, report.Completeness, 6);
        Assert.Equal(1, report.Validity);
        Assert.Equal(96.5, report.Score, 6);
    }

    [Fact]
    public void InvalidValues_ReduceValidity()
    {
        var report = QualityScorer.Compute("T", Statuses(valid: 80, invalid: 20), 20);

        Assert.Equal(0.8, report.Validity, 6);
        Assert.Equal(0.2, report.AnomalyRate, 6);
        Assert.Equal(63, report.Score, 6); // 35 + 28 + 0
        Assert.Equal("C", report.Grade);
    }

    [Fact]
    public void CorrectedValues_CountAsUsable()
    {
        var report = QualityScorer.Compute("T", Statuses(valid: 95, corrected: 5), 5);

        Assert.Equal(1, report.Validity);
    }

    [Fact]
    public void AllMissing_ValidityIsZero()
    {
        var report = QualityScorer.Compute("T", Statuses(valid: 0, missing: 10), 0);

        Assert.Equal(0, report.Validity);
        Assert.Equal(30, report.Score, 6);
        Assert.Equal("D", report.Grade);
    }

    [Theory]
    [InlineData(90.0, "A")]
    [InlineData(89.9, "B")]
    [InlineData(75.0, "B")]
    [InlineData(50.0, "C")]
    [InlineData(49.9, "D")]
    public void GradeBoundaries(double score, string grade)
    {
        Assert.Equal(grade, QualityScorer.GradeFor(score));
    }

    [Fact]
    public void NegativeAnomalyCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => QualityScorer.Compute("T", Statuses(1), -1));
    }
}
