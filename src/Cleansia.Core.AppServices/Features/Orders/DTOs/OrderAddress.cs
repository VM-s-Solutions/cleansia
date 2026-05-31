namespace Cleansia.Core.AppServices.Features.Orders.DTOs;

public record OrderAddress(
    string Street,
    string City,
    string ZipCode,
    string Country,
    double? Latitude,
    double? Longitude);