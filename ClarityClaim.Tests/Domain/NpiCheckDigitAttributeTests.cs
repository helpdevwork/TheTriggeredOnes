using ClarityClaim.Domain.Validation;
using FluentAssertions;
using Xunit;

namespace ClarityClaim.Tests.Domain;

public class NpiCheckDigitAttributeTests
{
    private readonly NpiCheckDigitAttribute _attr = new();

    [Fact]
    public void IsValid_NullOrEmpty_ReturnsTrue()
    {
        _attr.IsValid(null).Should().BeTrue();
        _attr.IsValid("").Should().BeTrue();
    }

    [Theory]
    [InlineData("1234567893")] // CMS-published example of a valid NPI checksum
    [InlineData("1013042399")]
    public void IsValid_KnownValidNpi_ReturnsTrue(string npi)
    {
        _attr.IsValid(npi).Should().BeTrue();
    }

    [Fact]
    public void IsValid_WrongLength_ReturnsFalse()
    {
        _attr.IsValid("123456789").Should().BeFalse();
        _attr.IsValid("12345678901").Should().BeFalse();
    }

    [Fact]
    public void IsValid_NonDigits_ReturnsFalse()
    {
        _attr.IsValid("12345abc90").Should().BeFalse();
    }

    [Fact]
    public void IsValid_BadChecksum_ReturnsFalse()
    {
        _attr.IsValid("1234567890").Should().BeFalse();
    }
}
