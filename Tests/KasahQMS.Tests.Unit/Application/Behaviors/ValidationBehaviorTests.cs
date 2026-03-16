using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using KasahQMS.Application.Common.Behaviors;
using MediatR;
using Moq;

namespace KasahQMS.Tests.Unit.Application.Behaviors;

public class ValidationBehaviorTests
{
    public record TestRequest(string Name) : IRequest<string>;

    [Fact]
    public async Task Handle_NoValidators_PassesThroughToNext()
    {
        // Arrange
        var validators = Enumerable.Empty<IValidator<TestRequest>>();
        var behavior = new ValidationBehavior<TestRequest, string>(validators);
        var request = new TestRequest("Valid");
        var nextCalled = false;

        RequestHandlerDelegate<string> next = () =>
        {
            nextCalled = true;
            return Task.FromResult("Success");
        };

        // Act
        var result = await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        nextCalled.Should().BeTrue();
        result.Should().Be("Success");
    }

    [Fact]
    public async Task Handle_ValidRequest_PassesThroughToNext()
    {
        // Arrange
        var validatorMock = new Mock<IValidator<TestRequest>>();
        validatorMock.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var validators = new[] { validatorMock.Object };
        var behavior = new ValidationBehavior<TestRequest, string>(validators);
        var request = new TestRequest("Valid");
        var nextCalled = false;

        RequestHandlerDelegate<string> next = () =>
        {
            nextCalled = true;
            return Task.FromResult("Success");
        };

        // Act
        var result = await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        nextCalled.Should().BeTrue();
        result.Should().Be("Success");
    }

    [Fact]
    public async Task Handle_InvalidRequest_ThrowsWithoutCallingNext()
    {
        // Arrange
        var failures = new List<ValidationFailure>
        {
            new("Name", "Name is required")
        };
        var validatorMock = new Mock<IValidator<TestRequest>>();
        validatorMock.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(failures));

        var validators = new[] { validatorMock.Object };
        var behavior = new ValidationBehavior<TestRequest, string>(validators);
        var request = new TestRequest("");
        var nextCalled = false;

        RequestHandlerDelegate<string> next = () =>
        {
            nextCalled = true;
            return Task.FromResult("Success");
        };

        // Act
        Func<Task> act = () => behavior.Handle(request, next, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Name is required*");
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_MultipleValidationErrors_CombinesMessages()
    {
        // Arrange
        var failures1 = new List<ValidationFailure>
        {
            new("Name", "Name is required")
        };
        var failures2 = new List<ValidationFailure>
        {
            new("Email", "Email is invalid")
        };

        var validator1Mock = new Mock<IValidator<TestRequest>>();
        validator1Mock.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(failures1));

        var validator2Mock = new Mock<IValidator<TestRequest>>();
        validator2Mock.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(failures2));

        var validators = new[] { validator1Mock.Object, validator2Mock.Object };
        var behavior = new ValidationBehavior<TestRequest, string>(validators);
        var request = new TestRequest("");

        RequestHandlerDelegate<string> next = () => Task.FromResult("Success");

        // Act
        Func<Task> act = () => behavior.Handle(request, next, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Message.Should().Contain("Name is required");
        ex.Which.Message.Should().Contain("Email is invalid");
    }
}
