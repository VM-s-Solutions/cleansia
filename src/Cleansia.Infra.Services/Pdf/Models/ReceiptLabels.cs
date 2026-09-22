using System.Globalization;
using Cleansia.Core.Domain.Enums;
using PaymentStatusValue = Cleansia.Core.Domain.Enums.PaymentStatus;

namespace Cleansia.Infra.Services.Pdf.Models;

/// <summary>
/// Every word the receipt layout prints that is not a fact about the sale, in the five locales the
/// platform renders.
///
/// <para><b>Keyed by LANGUAGE, unlike <see cref="InvoiceLabels"/>, which is keyed by country.</b> A
/// payout invoice is addressed to a tax authority in the cleaner's own jurisdiction, so it prints in
/// that jurisdiction's language. A receipt is addressed to the customer, so it prints in the customer's
/// language — the one their e-mails arrive in.</para>
///
/// <para><b>The payment method and status are looked up here, never <c>ToString()</c>d.</b> An enum
/// name is an identifier, not copy; <c>ReceiptPdfData</c> carries the enum values and the word is
/// chosen at render time. <c>ReceiptLanguageTests</c> fails when a locale or an enum member has no
/// word.</para>
///
/// <para>The statutory identifiers are labelled by CONCEPT rather than by the issuing registry's own
/// name — "Registration number", not "IČO", once the reader is outside CZ/SK. The number belongs to the
/// issuer's jurisdiction and translating its registry would assert a registration that does not
/// exist.</para>
/// </summary>
public record ReceiptLabels
{
    /// <summary>How the document's language writes a number: its decimal and group separators.</summary>
    public CultureInfo NumberCulture { get; init; } = CultureInfo.InvariantCulture;

    /// <summary>Whether the currency follows the amount ("2 000,00 Kč") rather than leading it.</summary>
    public bool CurrencyAfterAmount { get; init; }

    public string DocumentTitle { get; init; } = "Receipt";
    public string ReceiptNumber { get; init; } = "Receipt #";
    public string OrderNumber { get; init; } = "Order #";
    public string IssuedDate { get; init; } = "Date";

    public string CustomerInformation { get; init; } = "CUSTOMER INFORMATION";
    public string CompanyInformation { get; init; } = "COMPANY INFORMATION";
    public string Name { get; init; } = "Name";
    public string Email { get; init; } = "E-mail";
    public string Phone { get; init; } = "Telephone";
    public string Address { get; init; } = "Address";
    public string Company { get; init; } = "Company";
    public string RegistrationNumber { get; init; } = "Reg. No.";
    public string VatNumber { get; init; } = "VAT No.";
    public string Contact { get; init; } = "Contact";
    public string Iban { get; init; } = "IBAN";

    public string OrderDetails { get; init; } = "Order details";
    public string CleaningDate { get; init; } = "Cleaning date";
    public string Rooms { get; init; } = "Rooms";
    public string Bathrooms { get; init; } = "Bathrooms";
    public string EstimatedDuration { get; init; } = "Est. duration";
    public string HoursUnit { get; init; } = "h";
    public string MinutesUnit { get; init; } = "min";

    public string Items { get; init; } = "Items";
    public string Description { get; init; } = "Description";
    public string Amount { get; init; } = "Amount";
    public string NoItems { get; init; } = "No items";
    public string PackageSuffix { get; init; } = "package";
    public string ExpressSurcharge { get; init; } = "Express surcharge";
    public string TierDiscount { get; init; } = "Loyalty discount";
    public string MembershipDiscount { get; init; } = "Cleansia Plus discount";
    public string PromoDiscount { get; init; } = "Promo code discount";

    public string SubtotalExcludingVat { get; init; } = "Subtotal (excl. VAT)";
    public string Vat { get; init; } = "VAT";
    public string Total { get; init; } = "Total";
    public string PaidWithCredit { get; init; } = "Paid with credit";
    public string PaidByCard { get; init; } = "Paid by card";
    public string NotVatRegistered { get; init; } = "We are not registered for VAT";

    public string PaymentStatus { get; init; } = "Payment status";
    public string PaymentMethod { get; init; } = "Payment method";

    public string FiscalRegistration { get; init; } = "Fiscal registration";
    public string FiscalCode { get; init; } = "Fiscal code";
    public string FiscalProvider { get; init; } = "Provider";
    public string FiscalRegisteredAt { get; init; } = "Registered";

    public string ThanksBeforeName { get; init; } = "Thank you for choosing ";
    public string ThanksAfterName { get; init; } = " for your cleaning needs!";
    public string GeneratedOn { get; init; } = "Generated";

    public IReadOnlyDictionary<PaymentStatusValue, string> PaymentStatuses { get; init; } =
        new Dictionary<PaymentStatusValue, string>
        {
            [PaymentStatusValue.Pending] = "Awaiting payment",
            [PaymentStatusValue.Paid] = "Paid",
            [PaymentStatusValue.Failed] = "Payment failed",
            [PaymentStatusValue.Refunded] = "Refunded",
            [PaymentStatusValue.Disputed] = "Disputed",
            [PaymentStatusValue.PartiallyRefunded] = "Partially refunded",
        };

    public IReadOnlyDictionary<PaymentType, string> PaymentTypes { get; init; } =
        new Dictionary<PaymentType, string>
        {
            [PaymentType.Cash] = "Cash",
            [PaymentType.Card] = "Card",
        };

    /// <summary>
    /// The label set for a language code, English for anything else. The five are the locales
    /// <c>EmailLocale.Supported</c> names; a sixth stored on a user row is a preference nothing can
    /// render yet, and a receipt in a language nobody reviewed is worse than one in English.
    /// </summary>
    public static ReceiptLabels For(string? languageCode) =>
        languageCode is not null && ByLanguage.TryGetValue(languageCode, out var labels)
            ? labels
            : English;

    public static IReadOnlyCollection<string> SupportedLanguages => ByLanguage.Keys.ToList();

    public static ReceiptLabels English { get; } = new();

    public static ReceiptLabels Czech { get; } = new()
    {
        NumberCulture = CultureInfo.GetCultureInfo("cs-CZ"),
        CurrencyAfterAmount = true,

        DocumentTitle = "Účtenka",
        ReceiptNumber = "Účtenka č.",
        OrderNumber = "Objednávka č.",
        IssuedDate = "Datum",

        CustomerInformation = "ÚDAJE ZÁKAZNÍKA",
        CompanyInformation = "ÚDAJE DODAVATELE",
        Name = "Jméno",
        Email = "E-mail",
        Phone = "Telefon",
        Address = "Adresa",
        Company = "Název",
        RegistrationNumber = "IČO",
        VatNumber = "DIČ",
        Contact = "Kontakt",
        Iban = "IBAN",

        OrderDetails = "Detaily objednávky",
        CleaningDate = "Termín úklidu",
        Rooms = "Pokoje",
        Bathrooms = "Koupelny",
        EstimatedDuration = "Odhadovaná doba",
        HoursUnit = "h",
        MinutesUnit = "min",

        Items = "Položky",
        Description = "Popis",
        Amount = "Částka",
        NoItems = "Žádné položky",
        PackageSuffix = "balíček",
        ExpressSurcharge = "Expresní příplatek",
        TierDiscount = "Věrnostní sleva",
        MembershipDiscount = "Sleva Cleansia Plus",
        PromoDiscount = "Sleva na kód",

        SubtotalExcludingVat = "Základ daně",
        Vat = "DPH",
        Total = "Celkem",
        PaidWithCredit = "Uhrazeno kreditem",
        PaidByCard = "Uhrazeno kartou",
        NotVatRegistered = "Nejsme plátci DPH",

        PaymentStatus = "Stav platby",
        PaymentMethod = "Způsob platby",

        FiscalRegistration = "Evidence tržeb",
        FiscalCode = "Fiskální kód",
        FiscalProvider = "Poskytovatel",
        FiscalRegisteredAt = "Zaevidováno",

        ThanksBeforeName = "Děkujeme, že jste si pro úklid vybrali ",
        ThanksAfterName = "!",
        GeneratedOn = "Vygenerováno",

        PaymentStatuses = new Dictionary<PaymentStatusValue, string>
        {
            [PaymentStatusValue.Pending] = "Čeká na úhradu",
            [PaymentStatusValue.Paid] = "Zaplaceno",
            [PaymentStatusValue.Failed] = "Platba selhala",
            [PaymentStatusValue.Refunded] = "Vráceno",
            [PaymentStatusValue.Disputed] = "Reklamováno",
            [PaymentStatusValue.PartiallyRefunded] = "Částečně vráceno",
        },
        PaymentTypes = new Dictionary<PaymentType, string>
        {
            [PaymentType.Cash] = "Hotově",
            [PaymentType.Card] = "Kartou",
        },
    };

    public static ReceiptLabels Slovak { get; } = new()
    {
        NumberCulture = CultureInfo.GetCultureInfo("sk-SK"),
        CurrencyAfterAmount = true,

        DocumentTitle = "Účtenka",
        ReceiptNumber = "Účtenka č.",
        OrderNumber = "Objednávka č.",
        IssuedDate = "Dátum",

        CustomerInformation = "ÚDAJE ZÁKAZNÍKA",
        CompanyInformation = "ÚDAJE DODÁVATEĽA",
        Name = "Meno",
        Email = "E-mail",
        Phone = "Telefón",
        Address = "Adresa",
        Company = "Názov",
        RegistrationNumber = "IČO",
        VatNumber = "IČ DPH",
        Contact = "Kontakt",
        Iban = "IBAN",

        OrderDetails = "Detaily objednávky",
        CleaningDate = "Termín upratovania",
        Rooms = "Izby",
        Bathrooms = "Kúpeľne",
        EstimatedDuration = "Odhadovaný čas",
        HoursUnit = "h",
        MinutesUnit = "min",

        Items = "Položky",
        Description = "Popis",
        Amount = "Suma",
        NoItems = "Žiadne položky",
        PackageSuffix = "balík",
        ExpressSurcharge = "Expresný príplatok",
        TierDiscount = "Vernostná zľava",
        MembershipDiscount = "Zľava Cleansia Plus",
        PromoDiscount = "Zľava na kód",

        SubtotalExcludingVat = "Základ dane",
        Vat = "DPH",
        Total = "Spolu",
        PaidWithCredit = "Uhradené kreditom",
        PaidByCard = "Uhradené kartou",
        NotVatRegistered = "Nie sme platiteľmi DPH",

        PaymentStatus = "Stav platby",
        PaymentMethod = "Spôsob platby",

        FiscalRegistration = "Evidencia tržieb",
        FiscalCode = "Fiškálny kód",
        FiscalProvider = "Poskytovateľ",
        FiscalRegisteredAt = "Zaevidované",

        ThanksBeforeName = "Ďakujeme, že ste si na upratovanie vybrali ",
        ThanksAfterName = "!",
        GeneratedOn = "Vygenerované",

        PaymentStatuses = new Dictionary<PaymentStatusValue, string>
        {
            [PaymentStatusValue.Pending] = "Čaká na úhradu",
            [PaymentStatusValue.Paid] = "Zaplatené",
            [PaymentStatusValue.Failed] = "Platba zlyhala",
            [PaymentStatusValue.Refunded] = "Vrátené",
            [PaymentStatusValue.Disputed] = "Reklamované",
            [PaymentStatusValue.PartiallyRefunded] = "Čiastočne vrátené",
        },
        PaymentTypes = new Dictionary<PaymentType, string>
        {
            [PaymentType.Cash] = "Hotovosťou",
            [PaymentType.Card] = "Kartou",
        },
    };

    public static ReceiptLabels Ukrainian { get; } = new()
    {
        NumberCulture = CultureInfo.GetCultureInfo("uk-UA"),
        CurrencyAfterAmount = true,

        DocumentTitle = "Квитанція",
        ReceiptNumber = "Квитанція №",
        OrderNumber = "Замовлення №",
        IssuedDate = "Дата",

        CustomerInformation = "ДАНІ ЗАМОВНИКА",
        CompanyInformation = "ДАНІ ПОСТАЧАЛЬНИКА",
        Name = "Ім'я",
        Email = "E-mail",
        Phone = "Телефон",
        Address = "Адреса",
        Company = "Назва",
        RegistrationNumber = "Реєстраційний номер",
        VatNumber = "Номер платника ПДВ",
        Contact = "Контакти",
        Iban = "IBAN",

        OrderDetails = "Деталі замовлення",
        CleaningDate = "Дата прибирання",
        Rooms = "Кімнати",
        Bathrooms = "Ванні кімнати",
        EstimatedDuration = "Орієнтовна тривалість",
        HoursUnit = "год",
        MinutesUnit = "хв",

        Items = "Позиції",
        Description = "Опис",
        Amount = "Сума",
        NoItems = "Немає позицій",
        PackageSuffix = "пакет",
        ExpressSurcharge = "Доплата за терміновість",
        TierDiscount = "Знижка за лояльність",
        MembershipDiscount = "Знижка Cleansia Plus",
        PromoDiscount = "Знижка за промокодом",

        SubtotalExcludingVat = "Сума без ПДВ",
        Vat = "ПДВ",
        Total = "Разом",
        PaidWithCredit = "Сплачено з балансу",
        PaidByCard = "Сплачено карткою",
        NotVatRegistered = "Не є платником ПДВ",

        PaymentStatus = "Статус оплати",
        PaymentMethod = "Спосіб оплати",

        FiscalRegistration = "Фіскальна реєстрація",
        FiscalCode = "Фіскальний код",
        FiscalProvider = "Постачальник",
        FiscalRegisteredAt = "Зареєстровано",

        ThanksBeforeName = "Дякуємо, що обрали ",
        ThanksAfterName = " для прибирання!",
        GeneratedOn = "Згенеровано",

        PaymentStatuses = new Dictionary<PaymentStatusValue, string>
        {
            [PaymentStatusValue.Pending] = "Очікує оплати",
            [PaymentStatusValue.Paid] = "Сплачено",
            [PaymentStatusValue.Failed] = "Оплата не пройшла",
            [PaymentStatusValue.Refunded] = "Повернено",
            [PaymentStatusValue.Disputed] = "Оскаржено",
            [PaymentStatusValue.PartiallyRefunded] = "Частково повернено",
        },
        PaymentTypes = new Dictionary<PaymentType, string>
        {
            [PaymentType.Cash] = "Готівкою",
            [PaymentType.Card] = "Карткою",
        },
    };

    public static ReceiptLabels Russian { get; } = new()
    {
        NumberCulture = CultureInfo.GetCultureInfo("ru-RU"),
        CurrencyAfterAmount = true,

        DocumentTitle = "Квитанция",
        ReceiptNumber = "Квитанция №",
        OrderNumber = "Заказ №",
        IssuedDate = "Дата",

        CustomerInformation = "ДАННЫЕ ЗАКАЗЧИКА",
        CompanyInformation = "ДАННЫЕ ПОСТАВЩИКА",
        Name = "Имя",
        Email = "E-mail",
        Phone = "Телефон",
        Address = "Адрес",
        Company = "Название",
        RegistrationNumber = "Регистрационный номер",
        VatNumber = "Номер плательщика НДС",
        Contact = "Контакты",
        Iban = "IBAN",

        OrderDetails = "Детали заказа",
        CleaningDate = "Дата уборки",
        Rooms = "Комнаты",
        Bathrooms = "Ванные комнаты",
        EstimatedDuration = "Примерная длительность",
        HoursUnit = "ч",
        MinutesUnit = "мин",

        Items = "Позиции",
        Description = "Описание",
        Amount = "Сумма",
        NoItems = "Нет позиций",
        PackageSuffix = "пакет",
        ExpressSurcharge = "Доплата за срочность",
        TierDiscount = "Скидка за лояльность",
        MembershipDiscount = "Скидка Cleansia Plus",
        PromoDiscount = "Скидка по промокоду",

        SubtotalExcludingVat = "Сумма без НДС",
        Vat = "НДС",
        Total = "Итого",
        PaidWithCredit = "Оплачено с баланса",
        PaidByCard = "Оплачено картой",
        NotVatRegistered = "Не является плательщиком НДС",

        PaymentStatus = "Статус оплаты",
        PaymentMethod = "Способ оплаты",

        FiscalRegistration = "Фискальная регистрация",
        FiscalCode = "Фискальный код",
        FiscalProvider = "Поставщик",
        FiscalRegisteredAt = "Зарегистрировано",

        ThanksBeforeName = "Спасибо, что выбрали ",
        ThanksAfterName = " для уборки!",
        GeneratedOn = "Сформировано",

        PaymentStatuses = new Dictionary<PaymentStatusValue, string>
        {
            [PaymentStatusValue.Pending] = "Ожидает оплаты",
            [PaymentStatusValue.Paid] = "Оплачено",
            [PaymentStatusValue.Failed] = "Оплата не прошла",
            [PaymentStatusValue.Refunded] = "Возвращено",
            [PaymentStatusValue.Disputed] = "Оспорено",
            [PaymentStatusValue.PartiallyRefunded] = "Частично возвращено",
        },
        PaymentTypes = new Dictionary<PaymentType, string>
        {
            [PaymentType.Cash] = "Наличными",
            [PaymentType.Card] = "Картой",
        },
    };

    private static readonly IReadOnlyDictionary<string, ReceiptLabels> ByLanguage =
        new Dictionary<string, ReceiptLabels>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = English,
            ["cs"] = Czech,
            ["sk"] = Slovak,
            ["uk"] = Ukrainian,
            ["ru"] = Russian,
        };
}
