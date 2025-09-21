using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.TestUtilities.MockDataFactories.Languages;

public class LanguageMockFactory
{
    public class LanguagePartial
    {
        [Required]
        [MaxLength(5)]
        public string Code { get; set; }

        [MaxLength(50)]
        public string Name { get; set; }
    }

    public static Language Generate(LanguagePartial? mergeFrom = null)
    {
        var language = Language.Create("CZ", "Czech");

        return language.Merge(mergeFrom);
    }
}