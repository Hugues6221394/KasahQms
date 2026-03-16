using FluentAssertions;
using KasahQMS.Domain.Common;
using Xunit;

namespace KasahQMS.Tests.Unit.Domain.Common;

public class ResultTests
{
    [Fact]
    public void Success_ShouldSetIsSuccessTrue()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public void Failure_ShouldSetIsSuccessFalseWithMessage()
    {
        var result = Result.Failure("Something went wrong");

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Be("Something went wrong");
    }

    [Fact]
    public void GenericSuccess_ShouldHaveValue()
    {
        var result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        result.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public void GenericSuccess_WithStringValue_ShouldHaveValue()
    {
        var result = Result.Success("hello");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void GenericFailure_AccessingValue_ShouldThrow()
    {
        var result = Result.Failure<int>("Not found");

        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Be("Not found");

        var act = () => { var _ = result.Value; };
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*failed result*");
    }

    [Fact]
    public void GenericFailure_ShouldNotThrowOnErrorMessage()
    {
        var result = Result.Failure<string>("Error occurred");

        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Be("Error occurred");
    }

    [Fact]
    public void FirstFailureOrSuccess_AllSuccess_ShouldReturnSuccess()
    {
        var r1 = Result.Success();
        var r2 = Result.Success();
        var r3 = Result.Success();

        var result = Result.FirstFailureOrSuccess(r1, r2, r3);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void FirstFailureOrSuccess_WithFailure_ShouldReturnFirstFailure()
    {
        var r1 = Result.Success();
        var r2 = Result.Failure("First error");
        var r3 = Result.Failure("Second error");

        var result = Result.FirstFailureOrSuccess(r1, r2, r3);

        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Be("First error");
    }

    [Fact]
    public void FirstFailureOrSuccess_OnlyFailures_ShouldReturnFirst()
    {
        var r1 = Result.Failure("Error A");
        var r2 = Result.Failure("Error B");

        var result = Result.FirstFailureOrSuccess(r1, r2);

        result.ErrorMessage.Should().Be("Error A");
    }

    [Fact]
    public void FirstFailureOrSuccess_EmptyArray_ShouldReturnSuccess()
    {
        var result = Result.FirstFailureOrSuccess();

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ImplicitConversion_FromValue_ShouldCreateSuccessResult()
    {
        Result<int> result = 42;

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void ImplicitConversion_FromString_ShouldCreateSuccessResult()
    {
        Result<string> result = "test value";

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("test value");
    }

    [Fact]
    public void GenericSuccess_WithComplexType_ShouldWork()
    {
        var dto = new { Name = "Test", Id = 1 };
        var result = Result.Success(dto);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Test");
        result.Value.Id.Should().Be(1);
    }

    [Fact]
    public void IsFailure_OnSuccess_ShouldBeFalse()
    {
        var result = Result.Success();
        result.IsFailure.Should().BeFalse();
    }

    [Fact]
    public void IsFailure_OnFailure_ShouldBeTrue()
    {
        var result = Result.Failure("error");
        result.IsFailure.Should().BeTrue();
    }
}
