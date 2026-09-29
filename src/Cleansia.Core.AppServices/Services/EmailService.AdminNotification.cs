using System.Globalization;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.AdminNotifications;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;

namespace Cleansia.Core.AppServices.Services;

public sealed partial class EmailService
{
    private const string SubjectSuffix = ".Subject";
    private const string BodySuffix = ".Body";
    private const string BodyUnderWaySuffix = ".BodyUnderWay";
    private const string CausePrefix = ".Cause.";
    private const string InstantDisplayFormat = "dd.MM.yyyy HH:mm 'UTC'";
    private const string DayDisplayFormat = "d. M. yyyy";

    // The same three layers as the wind-down notices: in-code copy per locale, an admin translation
    // row over it, and the per-send facts over both. The copy is keyed by event so one EmailType and
    // one template carry every admin event; the args are substituted positionally in the order the
    // event's catalogue entry declares, so the copy and the site can never disagree on what {n} is.
    public async Task<string> SendAdminNotificationEmailAsync(
        string email,
        string eventKey,
        IReadOnlyDictionary<string, string> args,
        string languageCode = Constants.Language.English,
        CancellationToken ct = default)
    {
        var entry = AdminEventCatalog.Find(eventKey);
        var locale = EmailLocale.Resolve(languageCode);

        var translations = await emailTemplateTranslationRepository
            .GetTranslationsByTypeAndLanguageAsync(EmailType.AdminNotification, locale, ct);

        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (key, value) in AdminNotificationDefaults[locale])
        {
            values[key] = value;
        }

        foreach (var (key, value) in translations)
        {
            values[key] = value;
        }

        var display = entry.EmailArgOrder
            .Select(name => DisplayValue(eventKey, name, args.GetValueOrDefault(name) ?? string.Empty, values))
            .ToArray<object?>();

        var bodyKey = eventKey + (IsUnderWay(eventKey, args) ? BodyUnderWaySuffix : BodySuffix);
        var subject = string.Format(CultureInfo.InvariantCulture, values[eventKey + SubjectSuffix]!, display);
        var body = string.Format(CultureInfo.InvariantCulture, values[bodyKey]!, display);

        values["lang"] = locale;
        values["Subject"] = subject;
        values["Body"] = body;
        values["SupportEmail"] = sendGridConfig.AddressFrom;
        values["FooterText"] = await FooterTextAsync(locale, countryId: null, ct);

        return await SendRenderedAsync(
            email,
            templateRenderer.Render(TemplateFileFor(EmailType.AdminNotification), values),
            subject,
            $"Admin notification ({eventKey}) to {email}",
            ct);
    }

    // A clean that was under way when its crew left is not back on the board: the copy says so.
    private static bool IsUnderWay(string eventKey, IReadOnlyDictionary<string, string> args) =>
        eventKey == AdminNotificationEventCatalog.OrderCrewLost
        && args.TryGetValue("statusAtLoss", out var status)
        && Enum.TryParse<OrderStatus>(status, out var parsed)
        && parsed is OrderStatus.OnTheWay or OrderStatus.InProgress;

    private static string DisplayValue(string eventKey, string name, string value, IReadOnlyDictionary<string, string?> values)
    {
        if (name == "cause")
        {
            return values.GetValueOrDefault(eventKey + CausePrefix + value) ?? value;
        }

        if (DateTime.TryParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var instant))
        {
            return instant.ToUniversalTime().ToString(InstantDisplayFormat, CultureInfo.InvariantCulture);
        }

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            return day.ToString(DayDisplayFormat, CultureInfo.InvariantCulture);
        }

        return value;
    }

    /// <summary>
    /// Default admin-notification copy per locale. The chrome keys are shared; the per-event keys are
    /// <c>{eventKey}.Subject</c> and <c>{eventKey}.Body</c> with <c>{n}</c> the n-th arg of the
    /// event's <see cref="AdminEventCatalog.Entry.EmailArgOrder"/>; the crew-lost event adds
    /// <c>.BodyUnderWay</c> and its two <c>.Cause.*</c> phrases.
    /// </summary>
    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> AdminNotificationDefaults =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Greeting"] = "Hello",
                ["HintText"] = "Sign in to the admin console to act on it.",
                ["SupportText"] = "Questions? Write to us at",
                ["Closing"] = "Kind regards,",
                ["TeamName"] = "the Cleansia team",
                ["admin.order.new.Subject"] = "New order {0} to serve",
                ["admin.order.new.Body"] = "Order {0} for {1} is ready for a cleaner.",
                ["admin.order.crew_lost.Subject"] = "Order {0} lost its last cleaner",
                ["admin.order.crew_lost.Body"] = "Order {0} on {3} has nobody on it — {1}. It is back on the board.",
                ["admin.order.crew_lost.BodyUnderWay"] = "Order {0} on {3} has nobody on it — {1}. The clean was already under way.",
                ["admin.order.crew_lost.Cause.dropped"] = "the cleaner dropped it",
                ["admin.order.crew_lost.Cause.rejected"] = "the cleaner's account was rejected",
                ["admin.order.cleaner_not_started.Subject"] = "The cleaner has not started order {0}",
                ["admin.order.cleaner_not_started.Body"] = "Order {0} was booked for {1} and nobody has started it. Check with the cleaner and the customer; if the cleaner did not arrive, cancel it as a no-show in the console.",
                ["admin.order.lockout_reported.Subject"] = "The cleaner cannot get in for order {0}",
                ["admin.order.lockout_reported.Body"] = "The cleaner on order {0}, booked for {1}, reports that they cannot get in. Check the entrance photo and the calls they made; if the customer did not let them in, cancel it as a customer lockout in the console.",
                ["admin.dispute.filed.Subject"] = "A dispute was filed on order {0}",
                ["admin.dispute.filed.Body"] = "A customer filed a dispute on order {0}. Open the console to read it and answer.",
                ["admin.dispute.chargeback.Subject"] = "A chargeback came in on order {0}",
                ["admin.dispute.chargeback.Body"] = "The bank reversed {1} on order {0}. The dispute in the console holds the details.",
                ["admin.dispute.chargeback_unmatched.Subject"] = "A chargeback of {0} matches no order",
                ["admin.dispute.chargeback_unmatched.Body"] = "The bank reversed {0} on a card payment no order carries. Find Stripe dispute {1} in the Stripe dashboard and answer it there.",
                ["admin.payment.failed.Subject"] = "A card payment failed on order {0}",
                ["admin.payment.failed.Body"] = "The card payment for order {0} was declined. The order is cancelled automatically if it stays unpaid.",
                ["admin.payment.refund_stuck.Subject"] = "A refund on order {0} is not through",
                ["admin.payment.refund_stuck.Body"] = "The refund of {1} on order {0} has not gone through for a day. It is retried every hour; check it in Stripe and in the console.",
                ["admin.payment.refund_needs_retry.Subject"] = "A refund on order {0} needs a retry",
                ["admin.payment.refund_needs_retry.Body"] = "The refund of {1} on order {0} has not gone through for a day, and it is not retried automatically. Check it in Stripe, then retry it from the dispute or the order in the console.",
                ["admin.erasure.failed.Subject"] = "An erasure could not be completed",
                ["admin.erasure.failed.Body"] = "An account erasure request failed on {0} and is retried tomorrow. The data-protection page lists it.",
                ["admin.company.wind_down_requested.Subject"] = "Wind-down requested",
                ["admin.company.wind_down_requested.Body"] = "Your company is winding down from {0}.",
                ["admin.company.wind_down_run.Subject"] = "A wind-down run settled the books",
                ["admin.company.wind_down_run.Body"] = "Cancelled {0}, refunded {1}, refund failures {2}, pay periods closed {3}.",
                ["admin.company.archived.Subject"] = "Company archived",
                ["admin.company.archived.Body"] = "The books were sealed on {0}.",
            },
            ["cs"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Greeting"] = "Dobrý den",
                ["HintText"] = "Přihlaste se do administrátorské konzole a vyřiďte to.",
                ["SupportText"] = "Máte otázky? Napište nám na",
                ["Closing"] = "S pozdravem,",
                ["TeamName"] = "tým Cleansia",
                ["admin.order.new.Subject"] = "Nová objednávka {0} k obsloužení",
                ["admin.order.new.Body"] = "Objednávka {0} za {1} čeká na uklízeče.",
                ["admin.order.crew_lost.Subject"] = "Objednávka {0} přišla o posledního uklízeče",
                ["admin.order.crew_lost.Body"] = "Na objednávce {0} dne {3} už nikdo není — {1}. Je zpět na nástěnce.",
                ["admin.order.crew_lost.BodyUnderWay"] = "Na objednávce {0} dne {3} už nikdo není — {1}. Úklid už probíhal.",
                ["admin.order.crew_lost.Cause.dropped"] = "uklízeč ji opustil",
                ["admin.order.crew_lost.Cause.rejected"] = "účet uklízeče byl zamítnut",
                ["admin.order.cleaner_not_started.Subject"] = "Uklízeč nezačal objednávku {0}",
                ["admin.order.cleaner_not_started.Body"] = "Objednávka {0} byla objednána na {1} a nikdo ji nezačal. Ověřte to u uklízeče a zákazníka; pokud uklízeč nedorazil, zrušte ji v konzoli jako nedostavení.",
                ["admin.order.lockout_reported.Subject"] = "Uklízeč se nemůže dostat dovnitř u objednávky {0}",
                ["admin.order.lockout_reported.Body"] = "Uklízeč u objednávky {0}, objednané na {1}, hlásí, že se nemůže dostat dovnitř. Zkontrolujte fotografii vchodu a jeho pokusy o volání; pokud ho zákazník nepustil dovnitř, zrušte ji v konzoli jako nezpřístupnění zákazníkem.",
                ["admin.dispute.filed.Subject"] = "K objednávce {0} byla podána reklamace",
                ["admin.dispute.filed.Body"] = "Zákazník podal reklamaci k objednávce {0}. Otevřete konzoli, přečtěte si ji a odpovězte.",
                ["admin.dispute.chargeback.Subject"] = "K objednávce {0} přišel chargeback",
                ["admin.dispute.chargeback.Body"] = "Banka stornovala {1} u objednávky {0}. Podrobnosti najdete v reklamaci v konzoli.",
                ["admin.dispute.chargeback_unmatched.Subject"] = "Chargeback na {0} nepatří k žádné objednávce",
                ["admin.dispute.chargeback_unmatched.Body"] = "Banka stornovala {0} u platby kartou, kterou nemá žádná objednávka. Najděte spor {1} ve Stripe a vyřiďte ho tam.",
                ["admin.payment.failed.Subject"] = "Platba kartou u objednávky {0} selhala",
                ["admin.payment.failed.Body"] = "Platba kartou za objednávku {0} byla zamítnuta. Pokud zůstane nezaplacená, objednávka se automaticky zruší.",
                ["admin.payment.refund_stuck.Subject"] = "Vrácení peněz u objednávky {0} neprošlo",
                ["admin.payment.refund_stuck.Body"] = "Vrácení {1} u objednávky {0} už den neprošlo. Zkouší se znovu každou hodinu; zkontrolujte ho ve Stripe a v konzoli.",
                ["admin.payment.refund_needs_retry.Subject"] = "Vrácení peněz u objednávky {0} je potřeba zopakovat",
                ["admin.payment.refund_needs_retry.Body"] = "Vrácení {1} u objednávky {0} už den neprošlo a automaticky se neopakuje. Zkontrolujte ho ve Stripe a pak ho zopakujte z reklamace nebo z objednávky v konzoli.",
                ["admin.erasure.failed.Subject"] = "Výmaz se nepodařilo dokončit",
                ["admin.erasure.failed.Body"] = "Žádost o výmaz účtu dne {0} selhala a zítra se zopakuje. Najdete ji na stránce ochrany osobních údajů.",
                ["admin.company.wind_down_requested.Subject"] = "Bylo požádáno o ukončení činnosti",
                ["admin.company.wind_down_requested.Body"] = "Vaše společnost ukončuje činnost od {0}.",
                ["admin.company.wind_down_run.Subject"] = "Běh ukončení činnosti vypořádal účetnictví",
                ["admin.company.wind_down_run.Body"] = "Zrušeno {0}, vráceno {1}, neúspěšných vrácení {2}, uzavřených výplatních období {3}.",
                ["admin.company.archived.Subject"] = "Společnost byla archivována",
                ["admin.company.archived.Body"] = "Účetnictví bylo uzavřeno dne {0}.",
            },
            ["sk"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Greeting"] = "Dobrý deň",
                ["HintText"] = "Prihláste sa do administrátorskej konzoly a vybavte to.",
                ["SupportText"] = "Máte otázky? Napíšte nám na",
                ["Closing"] = "S pozdravom,",
                ["TeamName"] = "tím Cleansia",
                ["admin.order.new.Subject"] = "Nová objednávka {0} na obslúženie",
                ["admin.order.new.Body"] = "Objednávka {0} za {1} čaká na upratovača.",
                ["admin.order.crew_lost.Subject"] = "Objednávka {0} prišla o posledného upratovača",
                ["admin.order.crew_lost.Body"] = "Na objednávke {0} dňa {3} už nikto nie je — {1}. Je späť na nástenke.",
                ["admin.order.crew_lost.BodyUnderWay"] = "Na objednávke {0} dňa {3} už nikto nie je — {1}. Upratovanie už prebiehalo.",
                ["admin.order.crew_lost.Cause.dropped"] = "upratovač ju opustil",
                ["admin.order.crew_lost.Cause.rejected"] = "účet upratovača bol zamietnutý",
                ["admin.order.cleaner_not_started.Subject"] = "Upratovač nezačal objednávku {0}",
                ["admin.order.cleaner_not_started.Body"] = "Objednávka {0} bola objednaná na {1} a nikto ju nezačal. Overte to u upratovača a zákazníka; ak upratovač neprišiel, zrušte ju v konzole ako nedostavenie sa.",
                ["admin.order.lockout_reported.Subject"] = "Upratovač sa nemôže dostať dnu pri objednávke {0}",
                ["admin.order.lockout_reported.Body"] = "Upratovač pri objednávke {0}, objednanej na {1}, hlási, že sa nemôže dostať dnu. Skontrolujte fotografiu vchodu a jeho pokusy o volanie; ak ho zákazník nepustil dnu, zrušte ju v konzole ako nesprístupnenie zákazníkom.",
                ["admin.dispute.filed.Subject"] = "K objednávke {0} bola podaná reklamácia",
                ["admin.dispute.filed.Body"] = "Zákazník podal reklamáciu k objednávke {0}. Otvorte konzolu, prečítajte si ju a odpovedzte.",
                ["admin.dispute.chargeback.Subject"] = "K objednávke {0} prišiel chargeback",
                ["admin.dispute.chargeback.Body"] = "Banka stornovala {1} pri objednávke {0}. Podrobnosti nájdete v reklamácii v konzole.",
                ["admin.dispute.chargeback_unmatched.Subject"] = "Chargeback na {0} nepatrí k žiadnej objednávke",
                ["admin.dispute.chargeback_unmatched.Body"] = "Banka stornovala {0} pri platbe kartou, ktorú nemá žiadna objednávka. Nájdite spor {1} v službe Stripe a vybavte ho tam.",
                ["admin.payment.failed.Subject"] = "Platba kartou pri objednávke {0} zlyhala",
                ["admin.payment.failed.Body"] = "Platba kartou za objednávku {0} bola zamietnutá. Ak zostane nezaplatená, objednávka sa automaticky zruší.",
                ["admin.payment.refund_stuck.Subject"] = "Vrátenie peňazí pri objednávke {0} neprešlo",
                ["admin.payment.refund_stuck.Body"] = "Vrátenie {1} pri objednávke {0} už deň neprešlo. Skúša sa znova každú hodinu; skontrolujte ho v Stripe a v konzole.",
                ["admin.payment.refund_needs_retry.Subject"] = "Vrátenie peňazí pri objednávke {0} treba zopakovať",
                ["admin.payment.refund_needs_retry.Body"] = "Vrátenie {1} pri objednávke {0} už deň neprešlo a automaticky sa neopakuje. Skontrolujte ho v Stripe a potom ho zopakujte z reklamácie alebo z objednávky v konzole.",
                ["admin.erasure.failed.Subject"] = "Výmaz sa nepodarilo dokončiť",
                ["admin.erasure.failed.Body"] = "Žiadosť o výmaz účtu dňa {0} zlyhala a zajtra sa zopakuje. Nájdete ju na stránke ochrany osobných údajov.",
                ["admin.company.wind_down_requested.Subject"] = "Bolo požiadané o ukončenie činnosti",
                ["admin.company.wind_down_requested.Body"] = "Vaša spoločnosť ukončuje činnosť od {0}.",
                ["admin.company.wind_down_run.Subject"] = "Beh ukončenia činnosti vyrovnal účtovníctvo",
                ["admin.company.wind_down_run.Body"] = "Zrušené {0}, vrátené {1}, neúspešné vrátenia {2}, uzavreté výplatné obdobia {3}.",
                ["admin.company.archived.Subject"] = "Spoločnosť bola archivovaná",
                ["admin.company.archived.Body"] = "Účtovníctvo bolo uzavreté dňa {0}.",
            },
            ["uk"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Greeting"] = "Доброго дня",
                ["HintText"] = "Увійдіть до адміністративної консолі, щоб опрацювати це.",
                ["SupportText"] = "Є запитання? Напишіть нам на",
                ["Closing"] = "З повагою,",
                ["TeamName"] = "команда Cleansia",
                ["admin.order.new.Subject"] = "Нове замовлення {0} до виконання",
                ["admin.order.new.Body"] = "Замовлення {0} на {1} чекає на прибиральника.",
                ["admin.order.crew_lost.Subject"] = "Замовлення {0} втратило останнього прибиральника",
                ["admin.order.crew_lost.Body"] = "На замовленні {0} на {3} нікого немає — {1}. Воно знову на дошці.",
                ["admin.order.crew_lost.BodyUnderWay"] = "На замовленні {0} на {3} нікого немає — {1}. Прибирання вже тривало.",
                ["admin.order.crew_lost.Cause.dropped"] = "прибиральник відмовився від нього",
                ["admin.order.crew_lost.Cause.rejected"] = "обліковий запис прибиральника було відхилено",
                ["admin.order.cleaner_not_started.Subject"] = "Прибиральник не розпочав замовлення {0}",
                ["admin.order.cleaner_not_started.Body"] = "Замовлення {0} було заплановано на {1}, і ніхто його не розпочав. Перевірте це з прибиральником і клієнтом; якщо прибиральник не прийшов, скасуйте його в консолі як неявку.",
                ["admin.order.lockout_reported.Subject"] = "Прибиральник не може потрапити всередину за замовленням {0}",
                ["admin.order.lockout_reported.Body"] = "Прибиральник за замовленням {0}, запланованим на {1}, повідомляє, що не може потрапити всередину. Перевірте фото входу та його спроби дзвінків; якщо клієнт його не впустив, скасуйте замовлення в консолі як недопуск з вини клієнта.",
                ["admin.dispute.filed.Subject"] = "За замовленням {0} подано скаргу",
                ["admin.dispute.filed.Body"] = "Клієнт подав скаргу за замовленням {0}. Відкрийте консоль, щоб прочитати її та відповісти.",
                ["admin.dispute.chargeback.Subject"] = "За замовленням {0} надійшов чарджбек",
                ["admin.dispute.chargeback.Body"] = "Банк скасував {1} за замовленням {0}. Подробиці — у скарзі в консолі.",
                ["admin.dispute.chargeback_unmatched.Subject"] = "Чарджбек на {0} не належить жодному замовленню",
                ["admin.dispute.chargeback_unmatched.Body"] = "Банк скасував {0} за платежем карткою, якого немає в жодному замовленні. Знайдіть спір {1} у Stripe і дайте відповідь там.",
                ["admin.payment.failed.Subject"] = "Оплата карткою за замовленням {0} не пройшла",
                ["admin.payment.failed.Body"] = "Оплату карткою за замовлення {0} відхилено. Якщо воно залишиться неоплаченим, замовлення буде скасовано автоматично.",
                ["admin.payment.refund_stuck.Subject"] = "Повернення коштів за замовленням {0} не пройшло",
                ["admin.payment.refund_stuck.Body"] = "Повернення {1} за замовленням {0} не проходить уже добу. Спроба повторюється щогодини; перевірте його у Stripe та в консолі.",
                ["admin.payment.refund_needs_retry.Subject"] = "Повернення коштів за замовленням {0} потрібно повторити",
                ["admin.payment.refund_needs_retry.Body"] = "Повернення {1} за замовленням {0} не проходить уже добу й автоматично не повторюється. Перевірте його у Stripe, а потім повторіть зі скарги або із замовлення в консолі.",
                ["admin.erasure.failed.Subject"] = "Видалення не вдалося завершити",
                ["admin.erasure.failed.Body"] = "Запит на видалення облікового запису {0} не вдався і буде повторений завтра. Він є на сторінці захисту даних.",
                ["admin.company.wind_down_requested.Subject"] = "Запитано припинення діяльності",
                ["admin.company.wind_down_requested.Body"] = "Ваша компанія припиняє діяльність з {0}.",
                ["admin.company.wind_down_run.Subject"] = "Запуск припинення діяльності закрив рахунки",
                ["admin.company.wind_down_run.Body"] = "Скасовано {0}, повернуто {1}, невдалих повернень {2}, закритих розрахункових періодів {3}.",
                ["admin.company.archived.Subject"] = "Компанію заархівовано",
                ["admin.company.archived.Body"] = "Облік було закрито {0}.",
            },
            ["ru"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Greeting"] = "Здравствуйте",
                ["HintText"] = "Войдите в административную консоль, чтобы разобраться с этим.",
                ["SupportText"] = "Есть вопросы? Напишите нам на",
                ["Closing"] = "С уважением,",
                ["TeamName"] = "команда Cleansia",
                ["admin.order.new.Subject"] = "Новый заказ {0} к выполнению",
                ["admin.order.new.Body"] = "Заказ {0} на {1} ждёт уборщика.",
                ["admin.order.crew_lost.Subject"] = "Заказ {0} остался без последнего уборщика",
                ["admin.order.crew_lost.Body"] = "На заказе {0} на {3} никого нет — {1}. Он снова на доске.",
                ["admin.order.crew_lost.BodyUnderWay"] = "На заказе {0} на {3} никого нет — {1}. Уборка уже шла.",
                ["admin.order.crew_lost.Cause.dropped"] = "уборщик отказался от него",
                ["admin.order.crew_lost.Cause.rejected"] = "аккаунт уборщика был отклонён",
                ["admin.order.cleaner_not_started.Subject"] = "Уборщик не начал заказ {0}",
                ["admin.order.cleaner_not_started.Body"] = "Заказ {0} был назначен на {1}, и никто его не начал. Уточните у уборщика и клиента; если уборщик не пришёл, отмените его в консоли как неявку.",
                ["admin.order.lockout_reported.Subject"] = "Уборщик не может попасть внутрь по заказу {0}",
                ["admin.order.lockout_reported.Body"] = "Уборщик по заказу {0}, назначенному на {1}, сообщает, что не может попасть внутрь. Проверьте фото входа и его попытки дозвониться; если клиент его не впустил, отмените заказ в консоли как недопуск по вине клиента.",
                ["admin.dispute.filed.Subject"] = "По заказу {0} подана претензия",
                ["admin.dispute.filed.Body"] = "Клиент подал претензию по заказу {0}. Откройте консоль, чтобы прочитать её и ответить.",
                ["admin.dispute.chargeback.Subject"] = "По заказу {0} пришёл чарджбэк",
                ["admin.dispute.chargeback.Body"] = "Банк отменил {1} по заказу {0}. Подробности — в претензии в консоли.",
                ["admin.dispute.chargeback_unmatched.Subject"] = "Чарджбэк на {0} не относится ни к одному заказу",
                ["admin.dispute.chargeback_unmatched.Body"] = "Банк отменил {0} по платежу картой, которого нет ни в одном заказе. Найдите спор {1} в Stripe и ответьте на него там.",
                ["admin.payment.failed.Subject"] = "Оплата картой по заказу {0} не прошла",
                ["admin.payment.failed.Body"] = "Оплата картой за заказ {0} отклонена. Если он останется неоплаченным, заказ будет отменён автоматически.",
                ["admin.payment.refund_stuck.Subject"] = "Возврат средств по заказу {0} не прошёл",
                ["admin.payment.refund_stuck.Body"] = "Возврат {1} по заказу {0} не проходит уже сутки. Попытка повторяется каждый час; проверьте его в Stripe и в консоли.",
                ["admin.payment.refund_needs_retry.Subject"] = "Возврат средств по заказу {0} нужно повторить",
                ["admin.payment.refund_needs_retry.Body"] = "Возврат {1} по заказу {0} не проходит уже сутки и автоматически не повторяется. Проверьте его в Stripe, а затем повторите из претензии или из заказа в консоли.",
                ["admin.erasure.failed.Subject"] = "Удаление не удалось завершить",
                ["admin.erasure.failed.Body"] = "Запрос на удаление аккаунта {0} не удался и будет повторён завтра. Он есть на странице защиты данных.",
                ["admin.company.wind_down_requested.Subject"] = "Запрошено прекращение деятельности",
                ["admin.company.wind_down_requested.Body"] = "Ваша компания прекращает деятельность с {0}.",
                ["admin.company.wind_down_run.Subject"] = "Запуск прекращения деятельности закрыл счета",
                ["admin.company.wind_down_run.Body"] = "Отменено {0}, возвращено {1}, неудачных возвратов {2}, закрытых расчётных периодов {3}.",
                ["admin.company.archived.Subject"] = "Компания заархивирована",
                ["admin.company.archived.Body"] = "Учёт был закрыт {0}.",
            },
        };
}
