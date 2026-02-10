using FluentAssertions;
using Milvaion.Application.Dtos.NotificationDtos;
using Milvaion.Domain.Enums;

namespace Milvaion.UnitTests.ApplicationTests;

[Trait("Application Unit Tests", "InternalNotificationRequest unit tests.")]
public class InternalNotificationRequestTests
{
    [Fact]
    public void DefaultConstructor_ShouldSetDefaults()
    {
        // Act
        var request = new InternalNotificationRequest();

        // Assert
        request.Type.Should().Be(default);
        request.Data.Should().BeNull();
        request.Text.Should().BeNull();
        request.ActionLink.Should().BeNull();
        request.RelatedEntity.Should().Be(NotificationEntity.None);
        request.RelatedEntityId.Should().BeNull();
        request.Recipients.Should().BeNull();
        request.FindRecipientsFromType.Should().BeTrue();
    }

    [Fact]
    public void ParameterizedConstructor_ShouldSetTypeAndData()
    {
        // Arrange
        var data = new { JobId = "abc-123", Reason = "failed" };

        // Act
        var request = new InternalNotificationRequest(AlertType.JobAutoDisabled, data);

        // Assert
        request.Type.Should().Be(AlertType.JobAutoDisabled);
        request.Data.Should().Be(data);
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        // Arrange & Act
        var request = new InternalNotificationRequest
        {
            Type = AlertType.ZombieOccurrenceDetected,
            Data = new { OccurrenceId = 42 },
            Text = "Zombie detected",
            ActionLink = "/occurrences/42",
            RelatedEntity = NotificationEntity.None,
            RelatedEntityId = "42",
            Recipients = ["admin", "ops"],
            FindRecipientsFromType = false
        };

        // Assert
        request.Type.Should().Be(AlertType.ZombieOccurrenceDetected);
        request.Text.Should().Be("Zombie detected");
        request.ActionLink.Should().Be("/occurrences/42");
        request.RelatedEntityId.Should().Be("42");
        request.Recipients.Should().HaveCount(2);
        request.FindRecipientsFromType.Should().BeFalse();
    }
}
