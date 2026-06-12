using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.Domain.Repositories;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cleansia.Tests.Features.Auth;

public class ConfirmUserEmailValidatorTests
{
    [Fact]
    public async Task When_Code_Is_Null_Then_Validation_Fails_With_Required_And_InvalidCode_Errors()
    {
        // Arrange
        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.ExistsWithConfirmationCodeAsync(null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        var validator = new ConfirmUserEmail.Validator(mockRepo.Object, Mock.Of<ILogger<ConfirmUserEmail.Validator>>());
        var command = new ConfirmUserEmail.Command(null);

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(1, result.Errors.Count);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.Required && e.ErrorCode == nameof(ConfirmUserEmail.Command.Code));
    }

    [Fact]
    public async Task When_Code_Is_Empty_Then_Validation_Fails_With_Required_And_InvalidCode_Errors()
    {
        // Arrange
        var mockRepo = new Mock<IUserRepository>();
        mockRepo.Setup(r => r.ExistsWithConfirmationCodeAsync(string.Empty, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        var validator = new ConfirmUserEmail.Validator(mockRepo.Object, Mock.Of<ILogger<ConfirmUserEmail.Validator>>());
        var command = new ConfirmUserEmail.Command(string.Empty);

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(1, result.Errors.Count);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.Required && e.ErrorCode == nameof(ConfirmUserEmail.Command.Code));
    }

    [Fact]
    public async Task When_Code_Does_Not_Exist_Then_Validation_Fails_With_InvalidCode_Error()
    {
        // Arrange
        var mockRepo = new Mock<IUserRepository>();
        const string invalidCode = "invalidCode";
        var validator = new ConfirmUserEmail.Validator(mockRepo.Object, Mock.Of<ILogger<ConfirmUserEmail.Validator>>());
        var command = new ConfirmUserEmail.Command(invalidCode);

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.InvalidConfirmationCode, result.Errors[0].ErrorMessage);
        Assert.Equal(nameof(ConfirmUserEmail.Command.Code), result.Errors[0].ErrorCode);
    }

    [Fact]
    public async Task When_Code_Is_Valid_But_Expired_Then_Validation_Fails_With_InvalidCode_Error()
    {
        // Arrange
        var mockRepo = new Mock<IUserRepository>();
        const string validCode = "validCode";
        mockRepo.Setup(r => r.GetByConfirmationCodeAsync(validCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserMockFactory.Generate(new UserMockFactory.UserPartial { ConfirmationCode = validCode, ConfirmationCodeExpiresAt = DateTimeOffset.UtcNow }));
        mockRepo.Setup(r => r.TryChargeConfirmationCodeAttemptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var validator = new ConfirmUserEmail.Validator(mockRepo.Object, Mock.Of<ILogger<ConfirmUserEmail.Validator>>());
        var command = new ConfirmUserEmail.Command(validCode);

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.InvalidConfirmationCode, result.Errors[0].ErrorMessage);
        Assert.Equal(nameof(ConfirmUserEmail.Command.Code), result.Errors[0].ErrorCode);
    }

    [Fact]
    public async Task When_Code_Is_Valid_Then_Validation_Passes()
    {
        // Arrange
        var mockRepo = new Mock<IUserRepository>();
        const string validCode = "validCode";
        mockRepo.Setup(r => r.GetByConfirmationCodeAsync(validCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserMockFactory.Generate(new UserMockFactory.UserPartial { ConfirmationCode = validCode, ConfirmationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15) }));
        mockRepo.Setup(r => r.TryChargeConfirmationCodeAttemptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var validator = new ConfirmUserEmail.Validator(mockRepo.Object, Mock.Of<ILogger<ConfirmUserEmail.Validator>>());
        var command = new ConfirmUserEmail.Command(validCode);

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}