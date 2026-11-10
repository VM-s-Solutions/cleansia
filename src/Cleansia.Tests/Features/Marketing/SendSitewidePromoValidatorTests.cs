using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Marketing;
using Cleansia.Core.Domain.Repositories;
using FluentValidation.TestHelper;
using Moq;

namespace Cleansia.Tests.Features.Marketing;

public class SendSitewidePromoValidatorTests
{
    private static SendSitewidePromo.Command ValidCommand() => new(
        TitleEn: "Spring sale", TitleCs: "Jaro", TitleSk: "Jar", TitleUk: "Vesna", TitleRu: "Vesna",
        BodyEn: "20% off", BodyCs: "Sleva", BodySk: "Zlava", BodyUk: "Znyzhka", BodyRu: "Skidka");

    private static SendSitewidePromo.Validator ValidatorWith(params (string, string?)[] recipients)
    {
        var preferences = new Mock<IUserNotificationPreferencesRepository>();
        preferences
            .Setup(r => r.GetPromoRecipientsAsync("TENANT-A", null, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recipients.ToList());
        var tenant = new Mock<ITenantProvider>();
        tenant.Setup(t => t.GetCurrentTenantId()).Returns("TENANT-A");
        return new SendSitewidePromo.Validator(preferences.Object, tenant.Object);
    }

    [Fact]
    public async Task A_Company_With_No_Opted_In_Customer_Is_Refused()
    {
        var result = await ValidatorWith().TestValidateAsync(ValidCommand());

        Assert.Equal(BusinessErrorMessage.MarketingNoOptedInCustomers, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task One_Opted_In_Customer_Is_Enough()
    {
        var result = await ValidatorWith(("USER-1", "cs")).TestValidateAsync(ValidCommand());

        result.ShouldNotHaveAnyValidationErrors();
    }
}
