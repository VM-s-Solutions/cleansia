using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Services;

namespace Cleansia.Core.Domain.EmployeePayroll;

public class EmployeePayConfig : Auditable
{
    public string? ServiceId { get; private set; }
    public Service? Service { get; private set; }

    public string? PackageId { get; private set; }
    public Package? Package { get; private set; }

    [Required]
    public decimal BasePay { get; private set; }

    public decimal ExtraPerRoom { get; private set; } = 0;

    public decimal ExtraPerBathroom { get; private set; } = 0;

    public decimal DistanceRatePerKm { get; private set; } = 0;

    [MaxLength(500)]
    public string? Description { get; private set; }

    [Required]
    public string CurrencyId { get; private set; } = default!;
    public Currency? Currency { get; private set; }

    public decimal MinimumPay { get; private set; } = 0;

    public decimal MaximumPay { get; private set; } = 0;

    public static EmployeePayConfig CreateForService(
        string serviceId,
        decimal basePay,
        string currencyId,
        decimal extraPerRoom = 0,
        decimal extraPerBathroom = 0,
        decimal distanceRatePerKm = 0,
        string? description = null)
    {
        if (basePay < 0)
        {
            throw new ArgumentException("Base pay cannot be negative", nameof(basePay));
        }

        return new EmployeePayConfig
        {
            ServiceId = serviceId,
            BasePay = basePay,
            CurrencyId = currencyId,
            ExtraPerRoom = extraPerRoom,
            ExtraPerBathroom = extraPerBathroom,
            DistanceRatePerKm = distanceRatePerKm,
            Description = description
        };
    }

    public static EmployeePayConfig CreateForPackage(
        string packageId,
        decimal basePay,
        string currencyId,
        decimal extraPerRoom = 0,
        decimal extraPerBathroom = 0,
        decimal distanceRatePerKm = 0,
        string? description = null)
    {
        if (basePay < 0)
        {
            throw new ArgumentException("Base pay cannot be negative", nameof(basePay));
        }

        return new EmployeePayConfig
        {
            PackageId = packageId,
            BasePay = basePay,
            CurrencyId = currencyId,
            ExtraPerRoom = extraPerRoom,
            ExtraPerBathroom = extraPerBathroom,
            DistanceRatePerKm = distanceRatePerKm,
            Description = description
        };
    }

    public EmployeePayConfig UpdatePayRates(
        decimal basePay,
        decimal extraPerRoom,
        decimal extraPerBathroom,
        decimal distanceRatePerKm)
    {
        if (basePay < 0)
        {
            throw new ArgumentException("Base pay cannot be negative", nameof(basePay));
        }

        BasePay = basePay;
        ExtraPerRoom = extraPerRoom;
        ExtraPerBathroom = extraPerBathroom;
        DistanceRatePerKm = distanceRatePerKm;

        return this;
    }

    public EmployeePayConfig SetPayLimits(decimal minimumPay, decimal maximumPay)
    {
        if (minimumPay < 0)
        {
            throw new ArgumentException("Minimum pay cannot be negative", nameof(minimumPay));
        }

        if (maximumPay > 0 && maximumPay < minimumPay)
        {
            throw new ArgumentException("Maximum pay cannot be less than minimum pay", nameof(maximumPay));
        }

        MinimumPay = minimumPay;
        MaximumPay = maximumPay;

        return this;
    }

    public decimal CalculatePay(int rooms, int bathrooms, decimal distance)
    {
        var totalPay = BasePay
            + (ExtraPerRoom * rooms)
            + (ExtraPerBathroom * bathrooms)
            + (DistanceRatePerKm * distance);

        if (MinimumPay > 0 && totalPay < MinimumPay)
        {
            totalPay = MinimumPay;
        }

        if (MaximumPay > 0 && totalPay > MaximumPay)
        {
            totalPay = MaximumPay;
        }

        return totalPay;
    }
}
