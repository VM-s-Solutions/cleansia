-- Email Template Translations for the first-order Promo Code e-mail
-- EmailType.PromoCode = 7
--
-- The body is rendered from email-templates/promo-code.html in this repository
-- (see Cleansia.Core.AppServices/Services/EmailTemplateRenderer.cs); only the
-- copy lives here, exactly as it does for the six hosted templates.
-- Idempotent: re-running inserts nothing that is already present.

INSERT INTO public."EmailTemplateTranslations" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "Key", "Value",
  "EmailType", "LanguageId"
)
SELECT * FROM (VALUES
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'Subject',
    'Your Cleansia discount code',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'Greeting',
    'Hello',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'IntroText',
    'Thanks for your interest. Here is the code for your first clean.',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'DiscountText',
    'Discount on your first order',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'InstructionsText',
    'Enter the code at checkout. It applies to one order.',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'ButtonText',
    'Book a clean',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'ExpiryNotice',
    'The code is valid until {0}.',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'IgnoreText',
    'If you did not ask for this code, you can ignore this message.',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'SupportText',
    'Questions? Write to us at',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'Closing',
    'See you soon,',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'TeamName',
    'The Cleansia team',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'FooterText',
    'You are receiving this because you asked for a discount code on cleansia.cz.',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'Subject',
    'Váš slevový kód Cleansia',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'Greeting',
    'Dobrý den',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'IntroText',
    'Děkujeme za zájem. Tady je kód na váš první úklid.',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'DiscountText',
    'Sleva na první objednávku',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'InstructionsText',
    'Kód zadáte při objednávce. Platí na jednu objednávku.',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'ButtonText',
    'Objednat úklid',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'ExpiryNotice',
    'Kód platí do {0}.',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'IgnoreText',
    'Pokud jste o kód nežádali, tuto zprávu můžete ignorovat.',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'SupportText',
    'Máte dotaz? Napište nám na',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'Closing',
    'Těšíme se,',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'TeamName',
    'tým Cleansia',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'FooterText',
    'Tuto zprávu jste dostali, protože jste si na cleansia.cz vyžádali slevový kód.',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'Subject',
    'Váš zľavový kód Cleansia',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'Greeting',
    'Dobrý deň',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'IntroText',
    'Ďakujeme za záujem. Tu je kód na vaše prvé upratovanie.',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'DiscountText',
    'Zľava na prvú objednávku',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'InstructionsText',
    'Kód zadáte pri objednávke. Platí na jednu objednávku.',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'ButtonText',
    'Objednať upratovanie',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'ExpiryNotice',
    'Kód platí do {0}.',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'IgnoreText',
    'Ak ste o kód nežiadali, túto správu môžete ignorovať.',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'SupportText',
    'Máte otázku? Napíšte nám na',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'Closing',
    'Tešíme sa,',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'TeamName',
    'tím Cleansia',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'FooterText',
    'Túto správu ste dostali, pretože ste si na cleansia.cz vyžiadali zľavový kód.',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'Subject',
    'Ваш код знижки Cleansia',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'Greeting',
    'Доброго дня',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'IntroText',
    'Дякуємо за інтерес. Ось код на ваше перше прибирання.',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'DiscountText',
    'Знижка на перше замовлення',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'InstructionsText',
    'Введіть код під час замовлення. Діє на одне замовлення.',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'ButtonText',
    'Замовити прибирання',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'ExpiryNotice',
    'Код дійсний до {0}.',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'IgnoreText',
    'Якщо ви не просили цей код, просто проігноруйте цей лист.',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'SupportText',
    'Є питання? Напишіть нам на',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'Closing',
    'До зустрічі,',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'TeamName',
    'команда Cleansia',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'FooterText',
    'Ви отримали цей лист, бо запросили код знижки на cleansia.cz.',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'Subject',
    'Ваш код скидки Cleansia',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'Greeting',
    'Добрый день',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'IntroText',
    'Спасибо за интерес. Вот код на вашу первую уборку.',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'DiscountText',
    'Скидка на первый заказ',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'InstructionsText',
    'Введите код при оформлении. Действует на один заказ.',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'ButtonText',
    'Заказать уборку',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'ExpiryNotice',
    'Код действителен до {0}.',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'IgnoreText',
    'Если вы не запрашивали код, просто проигнорируйте это письмо.',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'SupportText',
    'Есть вопрос? Напишите нам на',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'Closing',
    'До встречи,',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'TeamName',
    'команда Cleansia',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru')
  ),
  (
    generate_ulid()::TEXT, true, 'System', CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'FooterText',
    'Вы получили это письмо, потому что запросили код скидки на cleansia.cz.',
    7,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru')
  )
) AS v("Id", "IsActive", "CreatedBy", "CreatedOn", "UpdatedBy", "UpdatedOn", "DeactivatedBy", "DeactivatedOn", "Key", "Value", "EmailType", "LanguageId")
WHERE v."LanguageId" IS NOT NULL
  AND NOT EXISTS (
    SELECT 1 FROM public."EmailTemplateTranslations" e
    WHERE e."EmailType" = 7 AND e."Key" = v."Key" AND e."LanguageId" = v."LanguageId"
  );
