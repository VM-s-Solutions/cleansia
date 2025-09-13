using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internalization;

namespace Cleansia.Core.Domain.Emails;

public class EmailTranslation : Auditable
{
    [MaxLength(100)]
    public string Subject { get; private set; }

    [MaxLength(255)]
    public string Title { get; private set; }

    [MaxLength(255)]
    public string Header { get; private set; }

    [MaxLength(255)]
    public string SubHeader { get; private set; }

    [MaxLength(100)]
    public string GreetingWord { get; private set; }

    [MaxLength(2000)]
    public string Instruction { get; private set; }

    [MaxLength(2000)]
    public string CodeNote { get; private set; }

    [MaxLength(1000)]
    public string Footer { get; private set; }

    public EmailType EmailType { get; private set; }

    public string LanguageId { get; private set; }
    public Language? Language { get; private set; }
}