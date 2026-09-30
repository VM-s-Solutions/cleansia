namespace Cleansia.Infra.Services.Pdf.Models;

/// <summary>
/// The set-off statement a payout invoice carries when the company's cash the cleaner held was set off
/// against it (owner ruling 2026-09-28, decision 23). Keyed by the cleaner's LANGUAGE, like
/// <see cref="ReceiptLabels"/>: the statement is addressed to the cleaner, while the invoice around it is
/// addressed to their jurisdiction and stays in its language (<see cref="InvoiceLabels"/>).
/// </summary>
public record CashSetOffStatementLabels
{
    public string Title { get; init; } = "Cash set-off statement";
    public string InvoiceTotal { get; init; } = "Invoice total";
    public string CashSetOff { get; init; } = "Customer cash you hold, set off";
    public string Transfer { get; init; } = "Bank transfer to you";
    public string Note { get; init; } =
        "The invoice amount is unchanged. Any customer cash you still hold is carried forward to your next invoice.";

    public static CashSetOffStatementLabels For(string? languageCode) =>
        languageCode is not null && ByLanguage.TryGetValue(languageCode, out var labels)
            ? labels
            : English;

    public static IReadOnlyCollection<string> SupportedLanguages => ByLanguage.Keys.ToList();

    public static CashSetOffStatementLabels English { get; } = new();

    public static CashSetOffStatementLabels Czech { get; } = new()
    {
        Title = "Vyúčtování hotovosti",
        InvoiceTotal = "Částka faktury",
        CashSetOff = "Započtená hotovost od zákazníků, kterou máte u sebe",
        Transfer = "Převodem na váš účet",
        Note = "Částka faktury se nemění. Hotovost od zákazníků, kterou máte stále u sebe, se převádí do vaší další faktury.",
    };

    public static CashSetOffStatementLabels Slovak { get; } = new()
    {
        Title = "Vyúčtovanie hotovosti",
        InvoiceTotal = "Suma faktúry",
        CashSetOff = "Započítaná hotovosť od zákazníkov, ktorú máte pri sebe",
        Transfer = "Prevodom na váš účet",
        Note = "Suma faktúry sa nemení. Hotovosť od zákazníkov, ktorú máte stále pri sebe, sa prenáša do vašej ďalšej faktúry.",
    };

    public static CashSetOffStatementLabels Ukrainian { get; } = new()
    {
        Title = "Залік готівки",
        InvoiceTotal = "Сума рахунку",
        CashSetOff = "Зарахована готівка від клієнтів, яка є у вас",
        Transfer = "Переказ на ваш рахунок",
        Note = "Сума рахунку не змінюється. Готівка від клієнтів, яка ще залишається у вас, переноситься до вашого наступного рахунку.",
    };

    public static CashSetOffStatementLabels Russian { get; } = new()
    {
        Title = "Зачёт наличных",
        InvoiceTotal = "Сумма счёта",
        CashSetOff = "Зачтённые наличные от клиентов, которые у вас",
        Transfer = "Перевод на ваш счёт",
        Note = "Сумма счёта не меняется. Наличные от клиентов, которые ещё остаются у вас, переносятся в ваш следующий счёт.",
    };

    private static readonly IReadOnlyDictionary<string, CashSetOffStatementLabels> ByLanguage =
        new Dictionary<string, CashSetOffStatementLabels>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = English,
            ["cs"] = Czech,
            ["sk"] = Slovak,
            ["uk"] = Ukrainian,
            ["ru"] = Russian,
        };
}
