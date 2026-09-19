namespace Cleansia.Core.AppServices.Features.Memberships.Admin.DTOs;

public record MembershipPlanPriceDto(decimal Price, decimal MonthlyEquivalentPrice, string StripePriceId);
