using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Internationalization;

public class Language : BaseEntity
{
    [Required]
    [MaxLength(5)]
    public string Code { get; private set; }

    [MaxLength(50)]
    public string Name { get; private set; }

    public static Language Create(string code, string name) => new()
    {
        Code = code,
        Name = name
    };
}