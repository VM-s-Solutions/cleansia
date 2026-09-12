using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Internationalization;

public class Currency : Auditable
{
    [Required]
    [MaxLength(3)]
    public string Code { get; private set; }

    [MaxLength(5)]
    public string Symbol { get; private set; }

    [MaxLength(50)]
    public string Name { get; private set; }

    [Required]

    public bool IsDefault { get; private set; }

    public static Currency Create(string code, string symbol, string name) => new()
    {
        Code = Canonical(code),
        Symbol = symbol,
        Name = name,
        // BORN SWITCHED OFF. On a Currency, IsActive is the market switch, not the soft-delete flag it
        // is elsewhere: the catalogue price rule reads it to decide which currencies every entry must
        // be priced in, SetDefaultCurrency refuses to promote past it, and the booking path refuses a
        // caller-named currency without it. A currency an admin has just created has no prices, so it
        // is not operated until someone says so -- ActivateCurrency is the only writer of true.
        IsActive = false,
    };

    public void Update(string code, string symbol, string name)
    {
        Code = Canonical(code);
        Symbol = symbol;
        Name = name;
    }

    public void SetAsDefault(bool isDefault)
    {
        IsDefault = isDefault;
    }

    /// <summary>
    /// A currency code NAMES a currency, and ISO 4217 names them in upper case.
    ///
    /// <para>The column is <c>citext</c>, so the unique index already refuses a second row spelled
    /// differently — but citext folds for COMPARISON only and stores whatever was typed. Without this,
    /// an admin who types "czk" gets a row whose code is "czk" forever, and that string is not
    /// cosmetic: it is passed straight through as the receipt's currency code and onto the
    /// FiscalReceiptRequest sent to the tax authority. Unique-up-to-case is not the same as canonical.
    /// </para>
    /// </summary>
    private static string Canonical(string code) => code.Trim().ToUpperInvariant();

}