using FluentValidation.TestHelper;
using Relativa.Authentication.Application.DTOs;
using Relativa.Authentication.Application.Validators;
using Xunit;

namespace Relativa.Authentication.Application.Tests;

public sealed class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _sut = new();

    [Fact]
    public void Valid_ReturnsNoErrors() =>
        _sut.TestValidate(new LoginRequestDto("user@example.com", "password123"))
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceEmail_FailsValidation(string email) =>
        _sut.TestValidate(new LoginRequestDto(email, "password"))
            .ShouldHaveValidationErrorFor(x => x.Email);

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    [InlineData("@nodomain.com")]
    [InlineData("double@@domain.com")]
    [InlineData("plainstring")]
    public void InvalidEmailFormat_FailsValidation(string email) =>
        _sut.TestValidate(new LoginRequestDto(email, "password"))
            .ShouldHaveValidationErrorFor(x => x.Email);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespacePassword_FailsValidation(string password) =>
        _sut.TestValidate(new LoginRequestDto("user@example.com", password))
            .ShouldHaveValidationErrorFor(x => x.Password);

    [Fact]
    public void BothFieldsEmpty_ReturnsBothErrors()
    {
        var result = _sut.TestValidate(new LoginRequestDto("", ""));
        result.ShouldHaveValidationErrorFor(x => x.Email);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }
}

public sealed class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _sut = new();

    private static RegisterRequestDto Valid() =>
        new("Alice", "Smith", "alice@example.com", "Secure123", "+380501234567", new DateOnly(1990, 1, 1));

    [Fact]
    public void Valid_ReturnsNoErrors() =>
        _sut.TestValidate(Valid())
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceFirstName_FailsValidation(string firstName) =>
        _sut.TestValidate(Valid() with { FirstName = firstName })
            .ShouldHaveValidationErrorFor(x => x.FirstName);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceLastName_FailsValidation(string lastName) =>
        _sut.TestValidate(Valid() with { LastName = lastName })
            .ShouldHaveValidationErrorFor(x => x.LastName);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceEmail_FailsValidation(string email) =>
        _sut.TestValidate(Valid() with { Email = email })
            .ShouldHaveValidationErrorFor(x => x.Email);

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    [InlineData("@nodomain.com")]
    [InlineData("two@@signs.com")]
    public void InvalidEmailFormat_FailsValidation(string email) =>
        _sut.TestValidate(Valid() with { Email = email })
            .ShouldHaveValidationErrorFor(x => x.Email);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespacePassword_FailsValidation(string password) =>
        _sut.TestValidate(Valid() with { Password = password })
            .ShouldHaveValidationErrorFor(x => x.Password);

    [Fact]
    public void PasswordTooShort_FailsValidation() =>
        _sut.TestValidate(Valid() with { Password = "Short1" })
            .ShouldHaveValidationErrorFor(x => x.Password);

    [Fact]
    public void PasswordAtMinLength_ReturnsNoErrors() =>
        _sut.TestValidate(Valid() with { Password = "Exactly8" })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void FirstNameAtMaxLength_ReturnsNoErrors() =>
        _sut.TestValidate(Valid() with { FirstName = new string('A', 100) })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void FirstNameExceedsMaxLength_FailsValidation() =>
        _sut.TestValidate(Valid() with { FirstName = new string('A', 101) })
            .ShouldHaveValidationErrorFor(x => x.FirstName);

    [Fact]
    public void LastNameExceedsMaxLength_FailsValidation() =>
        _sut.TestValidate(Valid() with { LastName = new string('B', 101) })
            .ShouldHaveValidationErrorFor(x => x.LastName);
}

public sealed class UpdateMyProfileRequestValidatorTests
{
    private readonly UpdateMyProfileRequestValidator _sut = new();

    [Fact]
    public void Valid_ReturnsNoErrors() =>
        _sut.TestValidate(new UpdateMyProfileRequest("Bob", "Jones"))
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceFirstName_FailsValidation(string firstName) =>
        _sut.TestValidate(new UpdateMyProfileRequest(firstName, "Jones"))
            .ShouldHaveValidationErrorFor(x => x.FirstName);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceLastName_FailsValidation(string lastName) =>
        _sut.TestValidate(new UpdateMyProfileRequest("Bob", lastName))
            .ShouldHaveValidationErrorFor(x => x.LastName);

    [Fact]
    public void FirstNameAtMaxLength_ReturnsNoErrors() =>
        _sut.TestValidate(new UpdateMyProfileRequest(new string('C', 100), "Jones"))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void FirstNameExceedsMaxLength_FailsValidation() =>
        _sut.TestValidate(new UpdateMyProfileRequest(new string('C', 101), "Jones"))
            .ShouldHaveValidationErrorFor(x => x.FirstName);

    [Fact]
    public void LastNameAtMaxLength_ReturnsNoErrors() =>
        _sut.TestValidate(new UpdateMyProfileRequest("Bob", new string('D', 100)))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void LastNameExceedsMaxLength_FailsValidation() =>
        _sut.TestValidate(new UpdateMyProfileRequest("Bob", new string('D', 101)))
            .ShouldHaveValidationErrorFor(x => x.LastName);

    [Fact]
    public void BothFieldsEmpty_ReturnsBothErrors()
    {
        var result = _sut.TestValidate(new UpdateMyProfileRequest("", ""));
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankPhone_SkipsPhoneRule(string? phone) =>
        _sut.TestValidate(new UpdateMyProfileRequest("Bob", "Jones", phone))
            .ShouldNotHaveValidationErrorFor(x => x.Phone);

    [Fact]
    public void ValidPhone_ReturnsNoErrors() =>
        _sut.TestValidate(new UpdateMyProfileRequest("Bob", "Jones", "+15551234567"))
            .ShouldNotHaveValidationErrorFor(x => x.Phone);

    [Theory]
    [InlineData("12345")]
    [InlineData("+0123456")]
    [InlineData("not-a-phone")]
    [InlineData("+1")]
    public void InvalidPhone_FailsWithPhoneInvalidCode(string phone) =>
        _sut.TestValidate(new UpdateMyProfileRequest("Bob", "Jones", phone))
            .ShouldHaveValidationErrorFor(x => x.Phone).WithErrorCode("phone_invalid");

    [Fact]
    public void NullDateOfBirth_SkipsDateRule() =>
        _sut.TestValidate(new UpdateMyProfileRequest("Bob", "Jones", DateOfBirth: null))
            .ShouldNotHaveValidationErrorFor(x => x.DateOfBirth);

    [Fact]
    public void ValidDateOfBirth_ReturnsNoErrors() =>
        _sut.TestValidate(new UpdateMyProfileRequest("Bob", "Jones", DateOfBirth: new DateOnly(1990, 5, 20)))
            .ShouldNotHaveValidationErrorFor(x => x.DateOfBirth);

    [Fact]
    public void FutureDateOfBirth_FailsWithBirthdateInvalidCode() =>
        _sut.TestValidate(new UpdateMyProfileRequest("Bob", "Jones",
                DateOfBirth: DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1))))
            .ShouldHaveValidationErrorFor(x => x.DateOfBirth).WithErrorCode("birthdate_invalid");

    [Fact]
    public void DateOfBirthBefore1901_FailsWithBirthdateInvalidCode() =>
        _sut.TestValidate(new UpdateMyProfileRequest("Bob", "Jones", DateOfBirth: new DateOnly(1900, 1, 1)))
            .ShouldHaveValidationErrorFor(x => x.DateOfBirth).WithErrorCode("birthdate_invalid");
}

public sealed class ForgotPasswordRequestValidatorTests
{
    private readonly ForgotPasswordRequestValidator _sut = new();

    [Fact]
    public void Valid_ReturnsNoErrors() =>
        _sut.TestValidate(new ForgotPasswordRequest("user@example.com"))
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceEmail_FailsValidation(string email) =>
        _sut.TestValidate(new ForgotPasswordRequest(email))
            .ShouldHaveValidationErrorFor(x => x.Email);

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    [InlineData("@nodomain.com")]
    [InlineData("plainstring")]
    public void InvalidEmailFormat_FailsValidation(string email) =>
        _sut.TestValidate(new ForgotPasswordRequest(email))
            .ShouldHaveValidationErrorFor(x => x.Email);
}

public sealed class ResetPasswordRequestValidatorTests
{
    private readonly ResetPasswordRequestValidator _sut = new();

    private static ResetPasswordRequest Valid() =>
        new("valid-token", "Secure123");

    [Fact]
    public void Valid_ReturnsNoErrors() =>
        _sut.TestValidate(Valid())
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceToken_FailsValidation(string token) =>
        _sut.TestValidate(Valid() with { Token = token })
            .ShouldHaveValidationErrorFor(x => x.Token);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespacePassword_FailsValidation(string password) =>
        _sut.TestValidate(Valid() with { NewPassword = password })
            .ShouldHaveValidationErrorFor(x => x.NewPassword);

    [Fact]
    public void PasswordTooShort_FailsValidation() =>
        _sut.TestValidate(Valid() with { NewPassword = "short1" })
            .ShouldHaveValidationErrorFor(x => x.NewPassword);

    [Fact]
    public void PasswordAtMinLength_ReturnsNoErrors() =>
        _sut.TestValidate(Valid() with { NewPassword = "Exactly8" })
            .ShouldNotHaveAnyValidationErrors();
}
