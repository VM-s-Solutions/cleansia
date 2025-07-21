using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Internalization;

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
}