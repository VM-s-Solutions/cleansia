using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Internalization;

public class Language : BaseEntity
{
    [Required]
    [MaxLength(5)]
    public string Code { get; private set; }

    [MaxLength(50)]
    public string Name { get; private set; }
}