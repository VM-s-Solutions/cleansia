using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.Domain.Services;

/// <summary>
/// A service's price in ONE currency, authored by a human.
///
/// <para><b>Authored, never converted.</b> Owner ruling 2026-09-08: a price in another currency is a
/// number somebody chose for that market, not the crown price multiplied by a rate. The rate that used
/// to do that was a single hand-typed column with no feed, no history and no per-order snapshot, so
/// editing it silently restated every order that referenced it — and it produced prices like €45.10
/// that no cleaning company would quote.</para>
///
/// <para>Two money columns because a service has two: a flat base plus a per-unit amount charged for
/// each room and bathroom. They travel together — a currency that has one has both.</para>
///
/// <para><b>A missing row means the service is not offerable in that currency.</b> Fail closed, never
/// zero and never converted: adding a market means authoring its prices, and an empty catalogue is the
/// correct answer until somebody does.</para>
/// </summary>
public class ServicePrice : Auditable
{
    public string ServiceId { get; private set; }
    public Service? Service { get; private set; }

    public string CurrencyId { get; private set; }
    public Currency? Currency { get; private set; }

    public decimal BasePrice { get; private set; }

    public decimal PerRoomPrice { get; private set; }

    public static ServicePrice Create(string serviceId, string currencyId, decimal basePrice, decimal perRoomPrice) => new()
    {
        ServiceId = serviceId,
        CurrencyId = currencyId,
        BasePrice = basePrice,
        PerRoomPrice = perRoomPrice
    };

    public ServicePrice Update(decimal basePrice, decimal perRoomPrice)
    {
        BasePrice = basePrice;
        PerRoomPrice = perRoomPrice;
        return this;
    }
}
