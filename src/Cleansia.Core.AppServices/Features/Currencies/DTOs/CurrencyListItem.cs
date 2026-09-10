namespace Cleansia.Core.AppServices.Features.Currencies.DTOs;

public record CurrencyListItem(
    string Id,
    string Code,
    string Symbol,
    string Name,
    bool IsDefault);

/// <summary>
/// The same row plus <c>IsActive</c> — whether the platform OPERATES in this currency — for the admin
/// host only.
///
/// <para>On a Currency, <c>IsActive</c> is not the soft-delete flag it is on other entities:
/// <c>DeleteCurrency</c> hard-deletes and nothing else writes it false. It is the market switch that
/// <c>SetDefaultCurrency</c> refuses to promote past, and that the catalogue price rule reads to
/// decide which currencies an entry must be priced in.</para>
///
/// <para><b>A separate record rather than a property on <see cref="CurrencyListItem"/>.</b> That one
/// is nested in <c>OrderListItem</c> and travels to every host — both mobile specs included — so
/// adding to it would put a live market switch onto every historical order row, on five contracts,
/// so that one admin screen family could read a boolean. An order's currency is a fact about the
/// order; whether the platform still sells in it is not.</para>
/// </summary>
public record AdminCurrencyListItem(
    string Id,
    string Code,
    string Symbol,
    string Name,
    bool IsDefault,
    bool IsActive);