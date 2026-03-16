using FluentAssertions;
using KasahQMS.Domain.ValueObjects;
using Xunit;

namespace KasahQMS.Tests.Unit.Domain.ValueObjects;

public class EmailTests
{
    [Fact]
    public void Create_WithValidEmail_ShouldNormalizeToLowercase()
    {
        var email = Email.Create("John.Doe@Example.COM");

        email.Value.Should().Be("john.doe@example.com");
    }

    [Fact]
    public void Create_WithValidEmail_ShouldTrimWhitespace()
    {
        var email = Email.Create("  user@example.com  ");

        email.Value.Should().Be("user@example.com");
    }

    [Fact]
    public void Create_WithNull_ShouldThrowArgumentException()
    {
        var act = () => Email.Create(null!);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*empty*");
    }

    [Fact]
    public void Create_WithEmptyString_ShouldThrowArgumentException()
    {
        var act = () => Email.Create("");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*empty*");
    }

    [Fact]
    public void Create_WithWhitespace_ShouldThrowArgumentException()
    {
        var act = () => Email.Create("   ");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("missing@")]
    [InlineData("@nodomain.com")]
    [InlineData("no spaces@example.com")]
    [InlineData("missing.domain@")]
    public void Create_WithInvalidFormat_ShouldThrowArgumentException(string invalidEmail)
    {
        var act = () => Email.Create(invalidEmail);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*email*");
    }

    [Fact]
    public void Create_WithTooLongEmail_ShouldThrowArgumentException()
    {
        var longEmail = new string('a', 250) + "@ex.com";

        var act = () => Email.Create(longEmail);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*256*");
    }

    [Fact]
    public void TryCreate_WithValidEmail_ShouldReturnTrue()
    {
        var result = Email.TryCreate("valid@example.com", out var email);

        result.Should().BeTrue();
        email.Should().NotBeNull();
        email!.Value.Should().Be("valid@example.com");
    }

    [Fact]
    public void TryCreate_WithInvalidEmail_ShouldReturnFalse()
    {
        var result = Email.TryCreate("invalid", out var email);

        result.Should().BeFalse();
        email.Should().BeNull();
    }

    [Fact]
    public void TryCreate_WithNull_ShouldReturnFalse()
    {
        var result = Email.TryCreate(null!, out var email);

        result.Should().BeFalse();
        email.Should().BeNull();
    }

    [Fact]
    public void Equality_SameValue_ShouldBeEqual()
    {
        var email1 = Email.Create("user@example.com");
        var email2 = Email.Create("USER@EXAMPLE.COM");

        email1.Should().Be(email2);
        (email1 == email2).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentValue_ShouldNotBeEqual()
    {
        var email1 = Email.Create("user1@example.com");
        var email2 = Email.Create("user2@example.com");

        email1.Should().NotBe(email2);
        (email1 != email2).Should().BeTrue();
    }

    [Fact]
    public void ImplicitStringConversion_ShouldReturnValue()
    {
        var email = Email.Create("user@example.com");

        string result = email;

        result.Should().Be("user@example.com");
    }

    [Fact]
    public void ToString_ShouldReturnValue()
    {
        var email = Email.Create("user@example.com");

        email.ToString().Should().Be("user@example.com");
    }

    [Theory]
    [InlineData("simple@example.com")]
    [InlineData("very.common@example.org")]
    [InlineData("user+tag@example.com")]
    [InlineData("user.name@example.co.uk")]
    public void Create_WithVariousValidFormats_ShouldSucceed(string validEmail)
    {
        var act = () => Email.Create(validEmail);

        act.Should().NotThrow();
    }
}
