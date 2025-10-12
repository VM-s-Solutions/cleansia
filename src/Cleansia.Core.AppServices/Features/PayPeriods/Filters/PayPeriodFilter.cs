using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.PayPeriods.Filters;

public record PayPeriodFilter(
    PayPeriodStatus? Status,
    int? Year);
