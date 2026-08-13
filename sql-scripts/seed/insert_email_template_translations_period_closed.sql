-- Email Template Translations for Pay Period Closed Notification
-- EmailType.PeriodClosed = 4

-- English Translations (language_code: 'en')
INSERT INTO public."EmailTemplateTranslations" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "Key", "Value",
  "EmailType", "LanguageId"
)
VALUES
  -- Subject
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'Subject',
    'Pay Period Closed',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  -- Main Content
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'IntroText',
    'We are writing to inform you that the pay period has been automatically closed by our system.',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'StatusText',
    'Period Closed',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'DetailsText',
    'All work completed during this period has been recorded. Your invoice will be generated and processed according to our standard payroll schedule.',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  -- Period Details Section
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'PeriodDetailsTitle',
    'Period Details',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'PeriodLabelText',
    'Period',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'StartDateText',
    'Start Date',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'EndDateText',
    'End Date',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'ClosedAtText',
    'Closed At',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  -- Next Steps Section
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'NextStepsTitle',
    'What Happens Next?',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'NextStep1',
    'Your invoice will be automatically generated within 24-48 hours',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'NextStep2',
    'You will receive a separate email with your invoice details',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'NextStep3',
    'Payment will be processed according to the agreed payment schedule',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  -- Footer
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'ClosingText',
    'Thank you for your hard work during this period!',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'ContactText',
    'If you have any questions or concerns about this period closure, please don''t hesitate to contact us.',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'SupportText',
    'Need help? Contact us at',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'SupportEmail',
    'it@cleansia.cz',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'TeamSignature',
    'Best regards',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'TeamName',
    'The Cleansia Team',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'FooterText',
    '© 2024 Cleansia. All rights reserved.',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en')
  );

-- Czech Translations (language_code: 'cs')
INSERT INTO public."EmailTemplateTranslations" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "Key", "Value",
  "EmailType", "LanguageId"
)
VALUES
  -- Subject
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'Subject',
    'Platební období uzavřeno',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  -- Main Content
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'IntroText',
    'Píšeme Vám, abychom Vás informovali, že platební období bylo automaticky uzavřeno naším systémem.',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'StatusText',
    'Období uzavřeno',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'DetailsText',
    'Veškerá práce dokončená během tohoto období byla zaznamenána. Vaše faktura bude vygenerována a zpracována podle našeho standardního platebního harmonogramu.',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  -- Period Details Section
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'PeriodDetailsTitle',
    'Detaily období',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'PeriodLabelText',
    'Období',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'StartDateText',
    'Datum začátku',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'EndDateText',
    'Datum konce',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'ClosedAtText',
    'Uzavřeno v',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  -- Next Steps Section
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'NextStepsTitle',
    'Co se stane dál?',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'NextStep1',
    'Vaše faktura bude automaticky vygenerována do 24-48 hodin',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'NextStep2',
    'Obdržíte samostatný email s detaily vaší faktury',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'NextStep3',
    'Platba bude zpracována podle dohodnutého platebního harmonogramu',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  -- Footer
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'ClosingText',
    'Děkujeme za vaši tvrdou práci během tohoto období!',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'ContactText',
    'Pokud máte jakékoliv otázky nebo obavy ohledně uzavření tohoto období, neváhejte nás kontaktovat.',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'SupportText',
    'Potřebujete pomoc? Kontaktujte nás na',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'SupportEmail',
    'it@cleansia.cz',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'TeamSignature',
    'S pozdravem',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'TeamName',
    'Tým Cleansia',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  ),
  (
    generate_ulid()::TEXT,
    true,
    'System',
    CURRENT_TIMESTAMP,
    NULL, NULL, NULL, NULL,
    'FooterText',
    '© 2024 Cleansia. Všechna práva vyhrazena.',
    4,
    (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs')
  );
