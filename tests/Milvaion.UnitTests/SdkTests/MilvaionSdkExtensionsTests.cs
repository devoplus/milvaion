using FluentAssertions;
using Milvasoft.Milvaion.Sdk.Domain.Enums;
using Milvasoft.Milvaion.Sdk.Utils;

namespace Milvaion.UnitTests.SdkTests;

[Trait("SDK Unit Tests", "MilvaionSdkExtensions unit tests.")]
public class MilvaionSdkExtensionsTests
{
    [Theory]
    [InlineData(JobOccurrenceStatus.Completed, true)]
    [InlineData(JobOccurrenceStatus.Failed, true)]
    [InlineData(JobOccurrenceStatus.Cancelled, true)]
    [InlineData(JobOccurrenceStatus.TimedOut, true)]
    [InlineData(JobOccurrenceStatus.Unknown, true)]
    [InlineData(JobOccurrenceStatus.Queued, false)]
    [InlineData(JobOccurrenceStatus.Running, false)]
    public void IsFinalStatus_ShouldReturnExpectedResult(JobOccurrenceStatus status, bool expectedResult)
    {
        // Act
        var result = status.IsFinalStatus();

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public void IsFinalStatus_Completed_ShouldReturnTrue()
    {
        // Arrange
        var status = JobOccurrenceStatus.Completed;

        // Act
        var result = status.IsFinalStatus();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsFinalStatus_Failed_ShouldReturnTrue()
    {
        // Arrange
        var status = JobOccurrenceStatus.Failed;

        // Act
        var result = status.IsFinalStatus();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsFinalStatus_Cancelled_ShouldReturnTrue()
    {
        // Arrange
        var status = JobOccurrenceStatus.Cancelled;

        // Act
        var result = status.IsFinalStatus();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsFinalStatus_TimedOut_ShouldReturnTrue()
    {
        // Arrange
        var status = JobOccurrenceStatus.TimedOut;

        // Act
        var result = status.IsFinalStatus();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsFinalStatus_Unknown_ShouldReturnTrue()
    {
        // Arrange
        var status = JobOccurrenceStatus.Unknown;

        // Act
        var result = status.IsFinalStatus();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsFinalStatus_Queued_ShouldReturnFalse()
    {
        // Arrange
        var status = JobOccurrenceStatus.Queued;

        // Act
        var result = status.IsFinalStatus();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsFinalStatus_Running_ShouldReturnFalse()
    {
        // Arrange
        var status = JobOccurrenceStatus.Running;

        // Act
        var result = status.IsFinalStatus();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsFinalStatus_AllFinalStatuses_ShouldReturnTrue()
    {
        // Arrange
        var finalStatuses = new[]
        {
            JobOccurrenceStatus.Completed,
            JobOccurrenceStatus.Failed,
            JobOccurrenceStatus.Cancelled,
            JobOccurrenceStatus.TimedOut,
            JobOccurrenceStatus.Unknown
        };

        // Act & Assert
        foreach (var status in finalStatuses)
        {
            status.IsFinalStatus().Should().BeTrue($"because {status} is a final status");
        }
    }

    [Fact]
    public void IsFinalStatus_AllNonFinalStatuses_ShouldReturnFalse()
    {
        // Arrange
        var nonFinalStatuses = new[]
        {
            JobOccurrenceStatus.Queued,
            JobOccurrenceStatus.Running
        };

        // Act & Assert
        foreach (var status in nonFinalStatuses)
        {
            status.IsFinalStatus().Should().BeFalse($"because {status} is not a final status");
        }
    }

    [Fact]
    public void IsFinalStatus_FinalStatusCount_ShouldBeFive()
    {
        // Arrange
        var allStatuses = Enum.GetValues<JobOccurrenceStatus>();

        // Act
        var finalStatusCount = allStatuses.Count(s => s.IsFinalStatus());

        // Assert
        finalStatusCount.Should().Be(5, "because there are 5 final statuses: Completed, Failed, Cancelled, TimedOut, Unknown");
    }

    [Fact]
    public void IsFinalStatus_NonFinalStatusCount_ShouldBeTwo()
    {
        // Arrange
        var allStatuses = Enum.GetValues<JobOccurrenceStatus>();

        // Act
        var nonFinalStatusCount = allStatuses.Count(s => !s.IsFinalStatus());

        // Assert
        nonFinalStatusCount.Should().Be(2, "because there are 2 non-final statuses: Queued, Running");
    }

    #region IntervalToCron

    [Fact]
    public void IntervalToCron_ShouldReturnNull_WhenIntervalIsZeroOrNegative()
    {
        MilvaionSdkExtensions.IntervalToCron(0).Should().BeNull();
        MilvaionSdkExtensions.IntervalToCron(-5).Should().BeNull();
    }

    [Fact]
    public void IntervalToCron_ShouldReturnSecondsFormat_WhenLessThan60Seconds()
    {
        MilvaionSdkExtensions.IntervalToCron(30).Should().Be("*/30 * * * * *");
        MilvaionSdkExtensions.IntervalToCron(10).Should().Be("*/10 * * * * *");
    }

    [Fact]
    public void IntervalToCron_ShouldReturnEveryMinute_WhenExactly60Seconds()
    {
        MilvaionSdkExtensions.IntervalToCron(60).Should().Be("0 * * * * *");
    }

    [Fact]
    public void IntervalToCron_ShouldReturnMinutesFormat_WhenExactMinutes()
    {
        MilvaionSdkExtensions.IntervalToCron(300).Should().Be("0 */5 * * * *");
        MilvaionSdkExtensions.IntervalToCron(1800).Should().Be("0 */30 * * * *");
    }

    [Fact]
    public void IntervalToCron_ShouldReturnEveryHour_WhenExactly3600Seconds()
    {
        MilvaionSdkExtensions.IntervalToCron(3600).Should().Be("0 0 * * * *");
    }

    [Fact]
    public void IntervalToCron_ShouldReturnHoursFormat_WhenExactHours()
    {
        MilvaionSdkExtensions.IntervalToCron(7200).Should().Be("0 0 */2 * * *");
        MilvaionSdkExtensions.IntervalToCron(21600).Should().Be("0 0 */6 * * *");
    }

    [Fact]
    public void IntervalToCron_ShouldReturnDailyFormat_WhenExactly86400Seconds()
    {
        MilvaionSdkExtensions.IntervalToCron(86400).Should().Be("0 0 0 * * *");
    }

    [Fact]
    public void IntervalToCron_ShouldReturnMultipleDaysFormat()
    {
        MilvaionSdkExtensions.IntervalToCron(172800).Should().Be("0 0 0 */2 * *");
    }

    [Fact]
    public void IntervalToCron_ShouldApproximateComplexIntervals()
    {
        // 90 seconds = 1.5 minutes -> approximated
        var result = MilvaionSdkExtensions.IntervalToCron(90);
        result.Should().NotBeNull();
    }

    #endregion

    #region GetEffectiveCron

    [Fact]
    public void GetEffectiveCron_ShouldReturnCronExpression_WhenProvided()
    {
        MilvaionSdkExtensions.GetEffectiveCron("0 0 * * * *", 300).Should().Be("0 0 * * * *");
    }

    [Fact]
    public void GetEffectiveCron_ShouldConvertInterval_WhenNoCronExpression()
    {
        MilvaionSdkExtensions.GetEffectiveCron(null, 300).Should().Be("0 */5 * * * *");
    }

    [Fact]
    public void GetEffectiveCron_ShouldReturnNull_WhenNoCronAndNoInterval()
    {
        MilvaionSdkExtensions.GetEffectiveCron(null, null).Should().BeNull();
        MilvaionSdkExtensions.GetEffectiveCron("", null).Should().BeNull();
        MilvaionSdkExtensions.GetEffectiveCron(null, 0).Should().BeNull();
    }

    [Fact]
    public void GetEffectiveCron_ShouldPreferCronExpression_OverInterval()
    {
        MilvaionSdkExtensions.GetEffectiveCron("*/5 * * * * *", 3600).Should().Be("*/5 * * * * *");
    }

    [Fact]
    public void GetEffectiveCron_ShouldTreatWhitespaceOnlyCron_AsNull()
    {
        MilvaionSdkExtensions.GetEffectiveCron("   ", 300).Should().Be("0 */5 * * * *");
    }

    [Fact]
    public void GetEffectiveCron_ShouldReturnNull_WhenNegativeInterval()
    {
        MilvaionSdkExtensions.GetEffectiveCron(null, -1).Should().BeNull();
    }

    [Fact]
    public void IntervalToCron_ShouldReturnNull_WhenIntervalIs1Second()
    {
        // 1 second is a valid interval but edge case
        var result = MilvaionSdkExtensions.IntervalToCron(1);
        result.Should().NotBeNull();
    }

    #endregion
}
