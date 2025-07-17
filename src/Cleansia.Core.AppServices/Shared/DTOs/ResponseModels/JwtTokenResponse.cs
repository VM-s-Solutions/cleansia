namespace Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;

public record JwtTokenResponse(
    string Token,
    bool IsEmailConfirmed);
