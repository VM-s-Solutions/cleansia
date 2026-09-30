using System.Globalization;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Legal;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Contracts;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Models;

namespace Cleansia.Core.AppServices.Services;

public sealed class ContractConfirmationService(
    IPdfService pdfService,
    ICompanyInfoRepository companyInfoRepository,
    ILegalDocumentRepository legalDocumentRepository,
    ICountryConfigurationRepository countryConfigurationRepository) : IContractConfirmationService
{
    private const string Brand = "Cleansia";

    public async Task<(byte[] Bytes, string FileName)> ForBookingAsync(
        Order order, DateTimeOffset contractConcludedOn, string languageCode, CancellationToken cancellationToken)
    {
        var language = EmailLocale.Resolve(languageCode);
        var labels = Labels.For(language);
        var culture = CultureInfo.GetCultureInfo(language);
        var countryId = order.CustomerAddress?.CountryId;
        var zone = await MarketZoneAsync(countryId, cancellationToken);

        // The seller the receipts name: the company of the order's market under the order's company.
        var company = countryId is null ? null : await companyInfoRepository.GetActiveByCountryAsync(countryId, cancellationToken);
        company ??= await companyInfoRepository.GetActiveCompanyInfoAsync(cancellationToken);

        // The terms a booking is refused without accepting are those in force for its market on the day it was made.
        var terms = await legalDocumentRepository.GetInForceAsync(
            LegalDocumentAudience.Customer, LegalDocumentType.TermsOfService, countryId,
            DateOnly.FromDateTime(order.CreatedOn.UtcDateTime), cancellationToken);
        var termsText = terms?.TextForOrFallback(language);
        var currencyCode = order.Currency?.Code ?? string.Empty;

        var data = new ConfirmationPdfData(
            Issuer: company?.LegalName ?? Brand,
            Title: labels.BookingTitle,
            ReferenceLabel: labels.OrderNumber,
            Reference: order.DisplayOrderNumber,
            Sections:
            [
                new ConfirmationPdfSection(labels.Seller, CompanyFields(company, labels, withContact: true)),
                new ConfirmationPdfSection(labels.Customer, Fields(
                    (labels.Name, order.CustomerName),
                    (labels.Email, order.CustomerEmail))),
                new ConfirmationPdfSection(labels.Booking, Fields(
                    (labels.OrderNumber, order.DisplayOrderNumber),
                    (labels.CleaningDate, Local(order.CleaningDateTime, zone, culture)),
                    (labels.Address, order.CustomerAddress is { } address ? $"{address.Street}, {address.ZipCode} {address.City}" : null),
                    (labels.Rooms, order.Rooms.ToString(culture)),
                    (labels.Bathrooms, order.Bathrooms.ToString(culture)))),
                new ConfirmationPdfSection(labels.Price, Fields(
                    (labels.TotalPrice, Money(order.TotalPrice, currencyCode, culture)),
                    (labels.CreditApplied, order.CreditAppliedAmount > 0m ? Money(order.CreditAppliedAmount, currencyCode, culture) : null),
                    (labels.Payment, order.PaymentType == PaymentType.Cash ? labels.PaymentCash : labels.PaymentCard))),
                new ConfirmationPdfSection(labels.Contract, Fields(
                    (labels.ContractConcluded, Instant(contractConcludedOn, zone, culture)),
                    (labels.TermsVersion, terms?.Version ?? labels.NotRecorded),
                    (labels.TextFingerprint, termsText?.ContentHash))),
                new ConfirmationPdfSection(labels.Withdrawal, order.EarlyPerformanceConsentedOn is { } requestedOn
                    ? Fields(
                        (labels.EarlyPerformanceRequest, labels.EarlyPerformanceStatement),
                        (labels.RequestMade, Instant(requestedOn, zone, culture)),
                        (labels.WordingVersion, order.EarlyPerformanceConsentTextVersion))
                    : Fields((labels.EarlyPerformanceRequest, labels.NotRecorded))),
            ],
            Text: []);

        return (pdfService.GenerateConfirmationPdf(data), $"booking-confirmation-{FileSafe(order.DisplayOrderNumber)}.pdf");
    }

    public async Task<(byte[] Bytes, string FileName)> ForWorkContractAsync(
        WorkContractAcceptance acceptance, Employee cleaner, string languageCode, CancellationToken cancellationToken)
    {
        var language = EmailLocale.Resolve(languageCode);
        var labels = Labels.For(language);
        var culture = CultureInfo.GetCultureInfo(language);
        var facts = WorkContractFacts.FromJson(acceptance.FactsJson);
        var zone = await MarketZoneAsync(facts.CountryId, cancellationToken);

        // The text row is FK-guaranteed (Restrict) by the acceptance that names it.
        var document = (await legalDocumentRepository.GetByTextIdWithTextsAsync(acceptance.LegalDocumentTextId, cancellationToken))!;
        var text = document.Texts.First(t => t.Id == acceptance.LegalDocumentTextId);
        var company = acceptance.TenantId is { } operatorTenantId
            ? await companyInfoRepository.GetActiveForOperatorAsync(operatorTenantId, facts.CountryId, cancellationToken)
            : null;

        var placeholders = LegalMarkdownRenderer.MarketPlaceholders(market: null, company);
        placeholders[LegalMarkdownRenderer.CurrencyPlaceholder] = facts.CurrencyCode;

        var data = new ConfirmationPdfData(
            Issuer: company?.LegalName ?? Brand,
            Title: text.Title,
            ReferenceLabel: labels.JobNumber,
            Reference: facts.OrderNumber,
            Sections:
            [
                new ConfirmationPdfSection(labels.Client, CompanyFields(company, labels, withContact: false)),
                new ConfirmationPdfSection(labels.Contractor, Fields(
                    (labels.Name, cleaner.User is { } user ? $"{user.FirstName} {user.LastName}".Trim() : null),
                    (labels.RegistrationNumber, cleaner.RegistrationNumber))),
                new ConfirmationPdfSection(labels.Job, Fields(
                    (labels.JobNumber, facts.OrderNumber),
                    (labels.CleaningDate, Local(facts.CleaningDateTimeUtc, zone, culture)),
                    (labels.EstimatedDuration, string.Format(culture, labels.Minutes, facts.EstimatedMinutes)),
                    (labels.Location, facts.LocationApproximate),
                    (labels.Rooms, facts.Rooms.ToString(culture)),
                    (labels.Bathrooms, facts.Bathrooms.ToString(culture)))),
                new ConfirmationPdfSection(labels.Price, Fields(
                    (labels.Reward, Money(facts.TotalPrice, facts.CurrencyCode, culture)))),
                new ConfirmationPdfSection(labels.Contract, Fields(
                    (labels.Accepted, Instant(acceptance.AcceptedOn, zone, culture)),
                    (labels.ContractVersion, acceptance.DocumentVersion),
                    (labels.TextFingerprint, text.ContentHash))),
            ],
            Text: LegalMarkdownRenderer.RenderPlainBlocks(text.ContentMarkdown, placeholders));

        return (pdfService.GenerateConfirmationPdf(data), $"work-contract-{FileSafe(facts.OrderNumber)}.pdf");
    }

    private async Task<TimeZoneInfo> MarketZoneAsync(string? countryId, CancellationToken cancellationToken) =>
        countryId is null
            ? TimeZoneInfo.Utc
            : TimeZoneResolution.Resolve(
                (await countryConfigurationRepository.GetByCountryIdAsync(countryId, cancellationToken))?.TimeZoneId);

    private static IReadOnlyList<(string Label, string Value)> CompanyFields(CompanyInfo? company, Labels labels, bool withContact) =>
        company is null
            ? []
            : Fields(
                (labels.Company, company.LegalName),
                (labels.RegistrationNumber, company.RegistrationNumber),
                (labels.VatNumber, company.VatNumber),
                (labels.Seat, company.GetFullAddress()),
                (labels.Email, withContact ? company.Email : null),
                (labels.Phone, withContact ? company.Phone : null));

    private static IReadOnlyList<(string Label, string Value)> Fields(params (string Label, string? Value)[] fields) =>
        fields
            .Where(f => !string.IsNullOrWhiteSpace(f.Value))
            .Select(f => (f.Label, f.Value!))
            .ToList();

    private static string Money(decimal amount, string currencyCode, CultureInfo culture) =>
        $"{amount.ToString("N2", culture)} {currencyCode}".Trim();

    private static string Local(DateTime utc, TimeZoneInfo zone, CultureInfo culture) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone).ToString("g", culture);

    private static string Instant(DateTimeOffset instant, TimeZoneInfo zone, CultureInfo culture)
    {
        var local = TimeZoneInfo.ConvertTime(instant, zone);
        return $"{local.ToString("G", culture)} (UTC{local.ToString("zzz", CultureInfo.InvariantCulture)})";
    }

    private static string FileSafe(string reference) =>
        new(reference.Where(c => char.IsAsciiLetterOrDigit(c) || c == '-').ToArray());

    private sealed record Labels
    {
        public string BookingTitle { get; init; } = "Booking confirmation";
        public string OrderNumber { get; init; } = "Order number";
        public string JobNumber { get; init; } = "Job number";
        public string Seller { get; init; } = "Seller";
        public string Client { get; init; } = "Client";
        public string Contractor { get; init; } = "Contractor";
        public string Company { get; init; } = "Company";
        public string RegistrationNumber { get; init; } = "Company ID (IČO)";
        public string VatNumber { get; init; } = "VAT ID (DIČ)";
        public string Seat { get; init; } = "Registered office";
        public string Email { get; init; } = "E-mail";
        public string Phone { get; init; } = "Phone";
        public string Customer { get; init; } = "Customer";
        public string Name { get; init; } = "Name";
        public string Booking { get; init; } = "Booking";
        public string Job { get; init; } = "Job";
        public string CleaningDate { get; init; } = "Cleaning date and time";
        public string Address { get; init; } = "Address";
        public string Location { get; init; } = "Location";
        public string EstimatedDuration { get; init; } = "Estimated duration";
        public string Minutes { get; init; } = "{0} min";
        public string Rooms { get; init; } = "Rooms";
        public string Bathrooms { get; init; } = "Bathrooms";
        public string Price { get; init; } = "Price";
        public string TotalPrice { get; init; } = "Total price";
        public string CreditApplied { get; init; } = "Paid with credit";
        public string Payment { get; init; } = "Payment";
        public string PaymentCard { get; init; } = "By card";
        public string PaymentCash { get; init; } = "In cash to the cleaner on the day";
        public string Reward { get; init; } = "Reward for your place on the job";
        public string Contract { get; init; } = "Contract";
        public string ContractConcluded { get; init; } = "Contract concluded";
        public string Accepted { get; init; } = "Accepted";
        public string TermsVersion { get; init; } = "Terms of service, version";
        public string ContractVersion { get; init; } = "Contract version";
        public string TextFingerprint { get; init; } = "Text fingerprint (SHA-256)";
        public string Withdrawal { get; init; } = "Withdrawal from the contract";
        public string EarlyPerformanceRequest { get; init; } = "Your request";
        public string EarlyPerformanceStatement { get; init; } =
            "You expressly asked for the cleaning to start within the 14-day withdrawal period and acknowledged that you lose the right to withdraw once the service has been fully performed.";
        public string RequestMade { get; init; } = "Request made";
        public string WordingVersion { get; init; } = "Wording version";
        public string NotRecorded { get; init; } = "Not recorded";

        public static Labels For(string language) => ByLanguage.GetValueOrDefault(language) ?? English;

        private static readonly Labels English = new();

        private static readonly IReadOnlyDictionary<string, Labels> ByLanguage =
            new Dictionary<string, Labels>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = English,
                ["cs"] = new()
                {
                    BookingTitle = "Potvrzení rezervace",
                    OrderNumber = "Číslo rezervace",
                    JobNumber = "Číslo zakázky",
                    Seller = "Prodávající",
                    Client = "Objednatel",
                    Contractor = "Zhotovitel",
                    Company = "Společnost",
                    RegistrationNumber = "IČO",
                    VatNumber = "DIČ",
                    Seat = "Sídlo",
                    Email = "E-mail",
                    Phone = "Telefon",
                    Customer = "Zákazník",
                    Name = "Jméno",
                    Booking = "Rezervace",
                    Job = "Zakázka",
                    CleaningDate = "Datum a čas úklidu",
                    Address = "Adresa",
                    Location = "Lokalita",
                    EstimatedDuration = "Předpokládaná doba",
                    Minutes = "{0} min",
                    Rooms = "Pokoje",
                    Bathrooms = "Koupelny",
                    Price = "Cena",
                    TotalPrice = "Celková cena",
                    CreditApplied = "Uhrazeno kreditem",
                    Payment = "Platba",
                    PaymentCard = "Kartou",
                    PaymentCash = "V hotovosti uklízeči v den úklidu",
                    Reward = "Odměna za vaše místo na zakázce",
                    Contract = "Smlouva",
                    ContractConcluded = "Smlouva uzavřena",
                    Accepted = "Přijato",
                    TermsVersion = "Obchodní podmínky, verze",
                    ContractVersion = "Verze smlouvy",
                    TextFingerprint = "Otisk textu (SHA-256)",
                    Withdrawal = "Odstoupení od smlouvy",
                    EarlyPerformanceRequest = "Vaše žádost",
                    EarlyPerformanceStatement =
                        "Výslovně jste požádali, aby úklid začal ve 14denní lhůtě pro odstoupení od smlouvy, a vzali jste na vědomí, že po úplném poskytnutí služby právo na odstoupení ztrácíte.",
                    RequestMade = "Žádost učiněna",
                    WordingVersion = "Verze znění",
                    NotRecorded = "Nezaznamenáno",
                },
                ["sk"] = new()
                {
                    BookingTitle = "Potvrdenie rezervácie",
                    OrderNumber = "Číslo rezervácie",
                    JobNumber = "Číslo zákazky",
                    Seller = "Predávajúci",
                    Client = "Objednávateľ",
                    Contractor = "Zhotoviteľ",
                    Company = "Spoločnosť",
                    RegistrationNumber = "IČO",
                    VatNumber = "DIČ",
                    Seat = "Sídlo",
                    Email = "E-mail",
                    Phone = "Telefón",
                    Customer = "Zákazník",
                    Name = "Meno",
                    Booking = "Rezervácia",
                    Job = "Zákazka",
                    CleaningDate = "Dátum a čas upratovania",
                    Address = "Adresa",
                    Location = "Lokalita",
                    EstimatedDuration = "Predpokladaný čas",
                    Minutes = "{0} min",
                    Rooms = "Izby",
                    Bathrooms = "Kúpeľne",
                    Price = "Cena",
                    TotalPrice = "Celková cena",
                    CreditApplied = "Uhradené kreditom",
                    Payment = "Platba",
                    PaymentCard = "Kartou",
                    PaymentCash = "V hotovosti upratovačovi v deň upratovania",
                    Reward = "Odmena za vaše miesto na zákazke",
                    Contract = "Zmluva",
                    ContractConcluded = "Zmluva uzavretá",
                    Accepted = "Prijaté",
                    TermsVersion = "Obchodné podmienky, verzia",
                    ContractVersion = "Verzia zmluvy",
                    TextFingerprint = "Odtlačok textu (SHA-256)",
                    Withdrawal = "Odstúpenie od zmluvy",
                    EarlyPerformanceRequest = "Vaša žiadosť",
                    EarlyPerformanceStatement =
                        "Výslovne ste požiadali, aby sa upratovanie začalo v 14-dňovej lehote na odstúpenie od zmluvy, a vzali ste na vedomie, že po úplnom poskytnutí služby právo na odstúpenie strácate.",
                    RequestMade = "Žiadosť podaná",
                    WordingVersion = "Verzia znenia",
                    NotRecorded = "Nezaznamenané",
                },
                ["uk"] = new()
                {
                    BookingTitle = "Підтвердження бронювання",
                    OrderNumber = "Номер бронювання",
                    JobNumber = "Номер замовлення",
                    Seller = "Продавець",
                    Client = "Замовник",
                    Contractor = "Виконавець",
                    Company = "Компанія",
                    RegistrationNumber = "Ідентифікаційний номер (IČO)",
                    VatNumber = "Податковий номер (DIČ)",
                    Seat = "Юридична адреса",
                    Email = "Електронна пошта",
                    Phone = "Телефон",
                    Customer = "Клієнт",
                    Name = "Ім’я",
                    Booking = "Бронювання",
                    Job = "Замовлення",
                    CleaningDate = "Дата й час прибирання",
                    Address = "Адреса",
                    Location = "Місце",
                    EstimatedDuration = "Орієнтовна тривалість",
                    Minutes = "{0} хв",
                    Rooms = "Кімнати",
                    Bathrooms = "Ванні кімнати",
                    Price = "Ціна",
                    TotalPrice = "Загальна ціна",
                    CreditApplied = "Сплачено кредитом",
                    Payment = "Оплата",
                    PaymentCard = "Карткою",
                    PaymentCash = "Готівкою прибиральнику в день прибирання",
                    Reward = "Винагорода за ваше місце на замовленні",
                    Contract = "Договір",
                    ContractConcluded = "Договір укладено",
                    Accepted = "Прийнято",
                    TermsVersion = "Умови надання послуг, версія",
                    ContractVersion = "Версія договору",
                    TextFingerprint = "Відбиток тексту (SHA-256)",
                    Withdrawal = "Відмова від договору",
                    EarlyPerformanceRequest = "Ваше прохання",
                    EarlyPerformanceStatement =
                        "Ви прямо попросили розпочати прибирання протягом 14-денного строку для відмови від договору та підтвердили, що втрачаєте право на відмову після повного надання послуги.",
                    RequestMade = "Прохання подано",
                    WordingVersion = "Версія формулювання",
                    NotRecorded = "Не зафіксовано",
                },
                ["ru"] = new()
                {
                    BookingTitle = "Подтверждение бронирования",
                    OrderNumber = "Номер бронирования",
                    JobNumber = "Номер заказа",
                    Seller = "Продавец",
                    Client = "Заказчик",
                    Contractor = "Исполнитель",
                    Company = "Компания",
                    RegistrationNumber = "Идентификационный номер (IČO)",
                    VatNumber = "Налоговый номер (DIČ)",
                    Seat = "Юридический адрес",
                    Email = "Электронная почта",
                    Phone = "Телефон",
                    Customer = "Клиент",
                    Name = "Имя",
                    Booking = "Бронирование",
                    Job = "Заказ",
                    CleaningDate = "Дата и время уборки",
                    Address = "Адрес",
                    Location = "Место",
                    EstimatedDuration = "Ориентировочная продолжительность",
                    Minutes = "{0} мин",
                    Rooms = "Комнаты",
                    Bathrooms = "Ванные комнаты",
                    Price = "Цена",
                    TotalPrice = "Общая цена",
                    CreditApplied = "Оплачено кредитом",
                    Payment = "Оплата",
                    PaymentCard = "Картой",
                    PaymentCash = "Наличными уборщику в день уборки",
                    Reward = "Вознаграждение за ваше место в заказе",
                    Contract = "Договор",
                    ContractConcluded = "Договор заключён",
                    Accepted = "Принято",
                    TermsVersion = "Условия оказания услуг, версия",
                    ContractVersion = "Версия договора",
                    TextFingerprint = "Отпечаток текста (SHA-256)",
                    Withdrawal = "Отказ от договора",
                    EarlyPerformanceRequest = "Ваша просьба",
                    EarlyPerformanceStatement =
                        "Вы прямо попросили начать уборку в течение 14-дневного срока для отказа от договора и подтвердили, что теряете право на отказ после полного оказания услуги.",
                    RequestMade = "Просьба подана",
                    WordingVersion = "Версия формулировки",
                    NotRecorded = "Не зафиксировано",
                },
            };
    }
}
