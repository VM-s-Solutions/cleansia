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
    public decimal ExchangeRate { get; private set; } = 1.0m;

    public bool IsDefault { get; private set; }

    public static Currency Create(string code, string symbol, string name, decimal exchangeRate) => new()
    {
        Code = code,
        Symbol = symbol,
        Name = name,
        ExchangeRate = exchangeRate
    };

    public void Update(string code, string symbol, string name, decimal exchangeRate)
    {
        Code = code;
        Symbol = symbol;
        Name = name;
        ExchangeRate = exchangeRate;
    }

    public void SetAsDefault(bool isDefault)
    {
        IsDefault = isDefault;
    }
}