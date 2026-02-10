using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Milvaion.Application.Behaviours;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Interception.Interceptors.Response;
using Moq;
using System.Net;

namespace Milvaion.UnitTests.BehavioursTests;

[Trait("Behaviours Unit Tests", "ValidationBehaviour unit tests.")]
public class ValidationBehaviourTests
{
    #region Test Types

    public record TestRequest : IRequest<Response>
    {
        public string Name { get; init; }
    }

    public record TestGenericRequest : IRequest<Response<string>>
    {
        public string Name { get; init; }
    }

    public record TestListRequest : IRequest<ListResponse<string>>
    {
        public string Name { get; init; }
    }

    #endregion

    #region ValidationBehaviorForResponse - No Validators

    [Fact]
    public async Task Handle_NoValidators_ShouldCallNextDelegate()
    {
        // Arrange
        var validators = Enumerable.Empty<IValidator<TestRequest>>();
        var serviceProvider = new Mock<IServiceProvider>();
        var behavior = new ValidationBehaviorForResponse<TestRequest, Response>(validators, serviceProvider.Object);
        var request = new TestRequest { Name = "Test" };
        var expectedResponse = new Response { IsSuccess = true };
        var next = new RequestHandlerDelegate<Response>((ct) => Task.FromResult(expectedResponse));

        // Act
        var result = await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(expectedResponse);
    }

    #endregion

    #region ValidationBehaviorForResponse - Validation Passes

    [Fact]
    public async Task Handle_ValidationPasses_ShouldCallNextDelegate()
    {
        // Arrange
        var mockValidator = new Mock<IValidator<TestRequest>>();
        mockValidator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), default))
                     .ReturnsAsync(new ValidationResult());
        var validators = new[] { mockValidator.Object };
        var serviceProvider = new Mock<IServiceProvider>();
        var behavior = new ValidationBehaviorForResponse<TestRequest, Response>(validators, serviceProvider.Object);
        var request = new TestRequest { Name = "Test" };
        var expectedResponse = new Response { IsSuccess = true };
        var next = new RequestHandlerDelegate<Response>((ct) => Task.FromResult(expectedResponse));

        // Act
        var result = await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(expectedResponse);
    }

    #endregion

    #region ValidationBehaviorForResponse - Validation Fails (Response)

    [Fact]
    public async Task Handle_ValidationFails_ForResponse_ShouldReturnFailureResponse()
    {
        // Arrange
        var validationFailures = new List<ValidationFailure>
        {
            new("Name", "Name is required.")
        };
        var mockValidator = new Mock<IValidator<TestRequest>>();
        mockValidator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), default))
                     .ReturnsAsync(new ValidationResult(validationFailures));
        var validators = new[] { mockValidator.Object };
        var serviceProvider = new Mock<IServiceProvider>();
        var behavior = new ValidationBehaviorForResponse<TestRequest, Response>(validators, serviceProvider.Object);
        var request = new TestRequest { Name = "" };
        var next = new RequestHandlerDelegate<Response>((ct) => Task.FromResult(new Response()));

        // Act
        var result = await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
    }

    #endregion

    #region ValidationBehaviorForResponse - Validation Fails (Response<T>)

    [Fact]
    public async Task Handle_ValidationFails_ForGenericResponse_ShouldReturnFailureResponse()
    {
        // Arrange
        var validationFailures = new List<ValidationFailure>
        {
            new("Name", "Name is required.")
        };
        var mockValidator = new Mock<IValidator<TestGenericRequest>>();
        mockValidator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestGenericRequest>>(), default))
                     .ReturnsAsync(new ValidationResult(validationFailures));
        var validators = new[] { mockValidator.Object };
        var mockResponseInterceptionOptions = new Mock<IResponseInterceptionOptions>();
        mockResponseInterceptionOptions.Setup(o => o.TranslateResultMessages).Returns(false);
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(s => s.GetService(typeof(IResponseInterceptionOptions)))
                       .Returns(mockResponseInterceptionOptions.Object);
        var behavior = new ValidationBehaviorForResponse<TestGenericRequest, Response<string>>(validators, serviceProvider.Object);
        var request = new TestGenericRequest { Name = "" };
        var next = new RequestHandlerDelegate<Response<string>>((ct) => Task.FromResult(new Response<string>()));

        // Act
        var result = await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
    }

    #endregion

    #region ValidationBehaviorForResponse - Validation Fails (ListResponse<T>)

    [Fact]
    public async Task Handle_ValidationFails_ForListResponse_ShouldReturnFailureResponse()
    {
        // Arrange
        var validationFailures = new List<ValidationFailure>
        {
            new("Name", "Name is required.")
        };
        var mockValidator = new Mock<IValidator<TestListRequest>>();
        mockValidator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestListRequest>>(), default))
                     .ReturnsAsync(new ValidationResult(validationFailures));
        var validators = new[] { mockValidator.Object };
        var mockResponseInterceptionOptions = new Mock<IResponseInterceptionOptions>();
        mockResponseInterceptionOptions.Setup(o => o.TranslateResultMessages).Returns(false);
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(s => s.GetService(typeof(IResponseInterceptionOptions)))
                       .Returns(mockResponseInterceptionOptions.Object);
        var behavior = new ValidationBehaviorForResponse<TestListRequest, ListResponse<string>>(validators, serviceProvider.Object);
        var request = new TestListRequest { Name = "" };
        var next = new RequestHandlerDelegate<ListResponse<string>>((ct) => Task.FromResult(new ListResponse<string>()));

        // Act
        var result = await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
    }

    #endregion

    #region ValidationBehaviorForResponse - Multiple Validators

    [Fact]
    public async Task Handle_MultipleValidatorsAllPass_ShouldCallNextDelegate()
    {
        // Arrange
        var mockValidator1 = new Mock<IValidator<TestRequest>>();
        mockValidator1.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), default))
                      .ReturnsAsync(new ValidationResult());
        var mockValidator2 = new Mock<IValidator<TestRequest>>();
        mockValidator2.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), default))
                      .ReturnsAsync(new ValidationResult());
        var validators = new[] { mockValidator1.Object, mockValidator2.Object };
        var serviceProvider = new Mock<IServiceProvider>();
        var behavior = new ValidationBehaviorForResponse<TestRequest, Response>(validators, serviceProvider.Object);
        var request = new TestRequest { Name = "Test" };
        var expectedResponse = new Response { IsSuccess = true };
        var next = new RequestHandlerDelegate<Response>((ct) => Task.FromResult(expectedResponse));

        // Act
        var result = await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(expectedResponse);
    }

    [Fact]
    public async Task Handle_MultipleValidatorsOneFails_ShouldReturnFailureResponse()
    {
        // Arrange
        var mockValidator1 = new Mock<IValidator<TestRequest>>();
        mockValidator1.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), default))
                      .ReturnsAsync(new ValidationResult());
        var mockValidator2 = new Mock<IValidator<TestRequest>>();
        mockValidator2.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), default))
                      .ReturnsAsync(new ValidationResult(new List<ValidationFailure>
                      {
                          new("Name", "Name is required.")
                      }));
        var validators = new[] { mockValidator1.Object, mockValidator2.Object };
        var serviceProvider = new Mock<IServiceProvider>();
        var behavior = new ValidationBehaviorForResponse<TestRequest, Response>(validators, serviceProvider.Object);
        var request = new TestRequest { Name = "" };
        var next = new RequestHandlerDelegate<Response>((ct) => Task.FromResult(new Response()));

        // Act
        var result = await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
    }

    #endregion
}
