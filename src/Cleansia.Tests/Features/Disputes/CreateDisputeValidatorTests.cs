using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Disputes;

/// <summary>
/// Pins the dispute-description length rule to the named <see cref="DisputeLimits"/> constants.
/// The boundary values are exercised through the constants so a change to the cap moves both the
/// rule and the test in one place.
/// </summary>
public class CreateDisputeValidatorTests
{
    private const string ExistingOrderId = "order-1";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly CreateDispute.Validator _validator;

    public CreateDisputeValidatorTests()
    {
        _orderRepository
            .Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _validator = new CreateDispute.Validator(_orderRepository.Object);
    }

    private static CreateDispute.Command CommandWithDescription(string description) =>
        new(ExistingOrderId, DisputeReason.QualityIssue, description);

    [Fact]
    public async Task When_Description_Below_Min_Then_MinLength_Error()
    {
        var description = new string('x', DisputeLimits.DescriptionMin - 1);

        var result = await _validator.ValidateAsync(CommandWithDescription(description));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.MinLength);
    }

    [Fact]
    public async Task When_Description_At_Min_Boundary_Then_No_Length_Error()
    {
        var description = new string('x', DisputeLimits.DescriptionMin);

        var result = await _validator.ValidateAsync(CommandWithDescription(description));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task When_Description_At_Max_Boundary_Then_No_Length_Error()
    {
        var description = new string('x', DisputeLimits.DescriptionMax);

        var result = await _validator.ValidateAsync(CommandWithDescription(description));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task When_Description_Over_Cap_Then_MaxLength_Error()
    {
        var description = new string('x', DisputeLimits.DescriptionMax + 1);

        var result = await _validator.ValidateAsync(CommandWithDescription(description));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.MaxLengthExceeded);
    }
}
