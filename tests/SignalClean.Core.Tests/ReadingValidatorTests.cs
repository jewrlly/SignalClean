using SignalClean.Core;
using SignalClean.Core.Validation;

namespace SignalClean.Core.Tests;

public class ReadingValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ValidValue_IsUsable()
    {
        var result = ReadingValidator.Validate(22.5, Now, -40, 85, Now);

        Assert.True(result.IsUsable);
        Assert.Equal(ReadingStatus.Valid, result.Status);
        Assert.Null(result.Anomaly);
    }

    [Fact]
    public void NullValue_IsMissing_WithoutAnomaly()
    {
        var result = ReadingValidator.Validate(null, Now, -40, 85, Now);

        Assert.Equal(ReadingStatus.Missing, result.Status);
        Assert.Null(result.Anomaly);
        Assert.False(result.IsUsable);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonFiniteValue_IsInvalid(double value)
    {
        var result = ReadingValidator.Validate(value, Now, -40, 85, Now);

        Assert.Equal(ReadingStatus.Invalid, result.Status);
        Assert.Equal(AnomalyKind.NotANumber, result.Anomaly);
    }

    [Theory]
    [InlineData(-40.0, true)]   // нижня межа включно
    [InlineData(85.0, true)]    // верхня межа включно
    [InlineData(-40.01, false)]
    [InlineData(85.01, false)]
    [InlineData(-999.0, false)] // типовий код помилки датчика
    public void BoundaryValues(double value, bool expectedUsable)
    {
        var result = ReadingValidator.Validate(value, Now, -40, 85, Now);

        Assert.Equal(expectedUsable, result.IsUsable);
        if (!expectedUsable)
        {
            Assert.Equal(AnomalyKind.OutOfRange, result.Anomaly);
        }
    }

    [Fact]
    public void FutureTimestamp_BeyondSkew_IsInvalid()
    {
        var result = ReadingValidator.Validate(20, Now.AddMinutes(6), -40, 85, Now);

        Assert.Equal(AnomalyKind.InvalidTimestamp, result.Anomaly);
    }

    [Fact]
    public void FutureTimestamp_WithinSkew_IsAccepted()
    {
        var result = ReadingValidator.Validate(20, Now.AddMinutes(4), -40, 85, Now);

        Assert.True(result.IsUsable);
    }

    [Fact]
    public void AncientTimestamp_IsInvalid()
    {
        var result = ReadingValidator.Validate(20, default, -40, 85, Now);

        Assert.Equal(AnomalyKind.InvalidTimestamp, result.Anomaly);
    }

    [Fact]
    public void InvertedRange_Throws()
    {
        Assert.Throws<ArgumentException>(() => ReadingValidator.Validate(1, Now, 10, 10, Now));
    }
}
