using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using MockQueryable.Moq;
using Moq;

namespace Cleansia.Tests.Features.Gdpr;

/// <summary>
/// The incident file targets a customer, never an administrator (the GDPR tools' shared rule), and an
/// order scope is refused as not found unless the order is the subject's — a stranger's order id must
/// not be confirmed to exist under another customer (S3).
/// </summary>
public sealed class ExportCustomerIncidentFileValidatorTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IIncidentFileService> _incidentFileService = new();

    private ExportCustomerIncidentFile.Validator CreateValidator(params User[] users)
    {
        _userRepository.Setup(r => r.GetAll()).Returns(users.AsQueryable().BuildMock());
        foreach (var user in users)
        {
            _userRepository.Setup(r => r.ExistsAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        }

        return new ExportCustomerIncidentFile.Validator(_userRepository.Object, _incidentFileService.Object);
    }

    private static User BuildUser(string id, UserProfile profile)
    {
        var user = User.CreateWithPassword($"{id}@example.com", "Password1", "First", "Last", profile);
        user.Id = id;
        return user;
    }

    [Fact]
    public async Task An_Administrator_Target_Is_Refused()
    {
        var validator = CreateValidator(BuildUser("target-admin", UserProfile.Administrator));

        var result = await validator.ValidateAsync(new ExportCustomerIncidentFile.Command("target-admin", null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.CannotTargetAdminViaGdprTool);
    }

    [Fact]
    public async Task An_Unknown_Subject_Is_Refused()
    {
        var validator = CreateValidator();
        _incidentFileService.Setup(s => s.IsSubjectOrderAsync("nobody", "order-1", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await validator.ValidateAsync(new ExportCustomerIncidentFile.Command("nobody", "order-1"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.NotExistingUserWithId);
    }

    [Fact]
    public async Task A_Strangers_Order_Is_Refused_As_Not_Found()
    {
        var validator = CreateValidator(BuildUser("subject", UserProfile.Customer));
        _incidentFileService.Setup(s => s.IsSubjectOrderAsync("subject", "order-of-someone-else", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await validator.ValidateAsync(new ExportCustomerIncidentFile.Command("subject", "order-of-someone-else"));

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, error.ErrorMessage);
        Assert.Equal(nameof(ExportCustomerIncidentFile.Command.OrderId), error.PropertyName);
    }

    [Fact]
    public async Task The_Subjects_Own_Order_Passes()
    {
        var validator = CreateValidator(BuildUser("subject", UserProfile.Customer));
        _incidentFileService.Setup(s => s.IsSubjectOrderAsync("subject", "order-1", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await validator.ValidateAsync(new ExportCustomerIncidentFile.Command("subject", "order-1"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task No_Order_Scope_Passes_Without_An_Ownership_Read(string? orderId)
    {
        var validator = CreateValidator(BuildUser("subject", UserProfile.Customer));

        var result = await validator.ValidateAsync(new ExportCustomerIncidentFile.Command("subject", orderId));

        Assert.True(result.IsValid);
        _incidentFileService.Verify(s => s.IsSubjectOrderAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
