-- ============================================================
-- DEV FIXTURES — runs after prod-bootstrap.sql, never on its own
-- ============================================================
-- The sample catalogue, its prices and pay rates, the Plus plans, the promo codes, the company record,
-- and the markets DEV configures but does not operate. Every row points at reference data
-- prod-bootstrap.sql inserts, and generate_ulid() is defined there. A Development boot runs both
-- (CleansiaStartupBase.DevelopmentSeedScripts); the shared DEV database takes both through
-- execute-sql.yml, the bootstrap first. execute-sql.yml refuses this file against PRO.

BEGIN TRANSACTION;

-- Temporarily defer foreign key constraint checks (Azure-compatible)
SET CONSTRAINTS ALL DEFERRED;

-- 5. CURRENCIES BEYOND CZK
-- CZK, the one operated currency, is in prod-bootstrap.sql. Twelve currencies were once seeded active
-- with hand-typed rates nobody had reviewed, and until Wave A every one of them was nameable by any
-- authenticated caller on the quote and create paths -- HUF at 16.2 meant a ~16x mispricing on demand.
--
-- EUR is seeded but INACTIVE, and the distinction earns its keep. The owner asked for EUR present with
-- zero prices so that "the machinery is built, only CZK is reachable" is provable rather than asserted
-- -- but that proof is a Wave B property: it needs fail-closed pricing to exist. In Wave A a second
-- ACTIVE currency is a live footgun, because the pricing calculator no longer scales anything and the
-- shipped admin "set default" star would charge the CZK catalogue under EUR (~25x). Caught by the Wave A
-- adversarial review, not by 4410 passing tests.
--
-- So: EUR exists here from day one, and Wave B flips IsActive = true in the same commit that gives it
-- price rows and the fail-closed rule. SetDefaultCurrency refuses an inactive currency in the meantime.
-- Its LoyaltyPointsDivisor stays NULL (earns nothing) until the owner rules its rate at activation.
--
-- The EUR/PLN/GBP/USD NoShowCredit figures are DEV placeholders; author the real figure on the admin
-- currency form before activation.
--
-- PLN, GBP and USD exist because the country configurations below name them: a named country resolves
-- to its currency or THROWS, so flagging Poland, the UK or the US serviced with no Currency row behind
-- its code would 500 every quote there. They are INACTIVE with no divisor -- the activation gate keeps
-- them off the market until the owner prices a catalogue in them.
INSERT INTO public."Currencies" (
  "Id", "IsActive", "IsDefault", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "Code", "Symbol", "Name", "LoyaltyPointsDivisor", "NoShowCredit"
)
VALUES
  (generate_ulid()::TEXT, false, false, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'EUR', '€', 'Euro', NULL, 10.00),
  (generate_ulid()::TEXT, false, false, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'PLN', 'zł', 'Polish Zloty', NULL, 40.00),
  (generate_ulid()::TEXT, false, false, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'GBP', '£', 'Pound Sterling', NULL, 9.00),
  (generate_ulid()::TEXT, false, false, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'USD', '$', 'US Dollar', NULL, 10.00);

-- 6. SERVICE CATEGORIES
-- Slugs are the client-facing stable identifier (mobile maps them to icons/colors).
-- Keep slugs immutable once seeded; rename Name freely via admin.
INSERT INTO public."ServiceCategories" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "Slug", "Name", "Description", "DisplayOrder", "Translations"
)
VALUES
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'home', 'Home', 'Everyday home cleaning services', 10,
   '{"en": {"Name": "Home", "Description": "Everyday home cleaning services"}, "cs": {"Name": "Domácnost", "Description": "Každodenní úklid domácnosti"}, "sk": {"Name": "Domácnosť", "Description": "Každodenné upratovanie domácnosti"}, "uk": {"Name": "Дім", "Description": "Щоденне прибирання будинку"}, "ru": {"Name": "Дом", "Description": "Ежедневная уборка дома"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'deep', 'Deep clean', 'Thorough and specialized cleaning', 20,
   '{"en": {"Name": "Deep clean", "Description": "Thorough and specialized cleaning"}, "cs": {"Name": "Hloubkový úklid", "Description": "Důkladné a specializované čištění"}, "sk": {"Name": "Hĺbkové čistenie", "Description": "Dôkladné a špecializované čistenie"}, "uk": {"Name": "Глибоке прибирання", "Description": "Ретельне та спеціалізоване прибирання"}, "ru": {"Name": "Глубокая уборка", "Description": "Тщательная и специализированная уборка"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'laundry', 'Laundry', 'Washing, ironing, and linen care', 30,
   '{"en": {"Name": "Laundry", "Description": "Washing, ironing, and linen care"}, "cs": {"Name": "Praní", "Description": "Praní, žehlení a péče o prádlo"}, "sk": {"Name": "Pranie", "Description": "Pranie, žehlenie a starostlivosť o bielizeň"}, "uk": {"Name": "Прання", "Description": "Прання, прасування та догляд за білизною"}, "ru": {"Name": "Стирка", "Description": "Стирка, глажка и уход за бельём"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'pet', 'Pet', 'Services tailored for pet owners', 40,
   '{"en": {"Name": "Pet", "Description": "Services tailored for pet owners"}, "cs": {"Name": "Mazlíčci", "Description": "Služby pro majitele domácích mazlíčků"}, "sk": {"Name": "Domáce zvieratá", "Description": "Služby pre majiteľov domácich miláčikov"}, "uk": {"Name": "Тварини", "Description": "Послуги для власників домашніх тварин"}, "ru": {"Name": "Питомцы", "Description": "Услуги для владельцев домашних животных"}}');

-- 7. SERVICES
INSERT INTO public."Services" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "Name", "Description",
  "EstimatedTime", "CategoryId", "Translations"
)
VALUES
  -- Basic Cleaning Services
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'General Cleaning', 'Standard cleaning of all rooms including dusting, vacuuming, and sanitizing',
   120,
   (SELECT "Id" FROM public."ServiceCategories" WHERE "Slug" = 'home'),
   '{"en":{"Name":"General Cleaning","Description":"Standard cleaning of all rooms including dusting, vacuuming, and sanitizing"},"cs":{"Name":"Obecný úklid","Description":"Standardní úklid všech místností včetně otírání prachu, vysávání a dezinfekce"},"sk":{"Name":"Všeobecné upratovanie","Description":"Štandardné upratovanie všetkých miestností vrátane utierania prachu, vysávania a dezinfekcie"},"uk":{"Name":"Загальне прибирання","Description":"Стандартне прибирання всіх кімнат, включно з витиранням пилу, пилососом та дезінфекцією"},"ru":{"Name":"Общая уборка","Description":"Стандартная уборка всех комнат включая протирание пыли, пылесос и дезинфекцию"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Deep Cleaning', 'Thorough cleaning including baseboards, inside appliances, and detailed sanitization',
   180,
   (SELECT "Id" FROM public."ServiceCategories" WHERE "Slug" = 'deep'),
   '{"en":{"Name":"Deep Cleaning","Description":"Thorough cleaning including baseboards, inside appliances, and detailed sanitization"},"cs":{"Name":"Hloubkový úklid","Description":"Důkladný úklid včetně lišt, vnitřků spotřebičů a detailní dezinfekce"},"sk":{"Name":"Hĺbkové upratovanie","Description":"Dôkladné upratovanie vrátane líšt, vnútra spotrebičov a detailnej dezinfekcie"},"uk":{"Name":"Глибоке прибирання","Description":"Ретельне прибирання, включно з плінтусами, всередині побутової техніки та детальною дезінфекцією"},"ru":{"Name":"Глубокая уборка","Description":"Тщательная уборка включая плинтуса, внутри бытовой техники и детальная дезинфекция"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Bathroom Cleaning', 'Specialized bathroom cleaning with tile scrubbing and grout cleaning',
   45,
   (SELECT "Id" FROM public."ServiceCategories" WHERE "Slug" = 'home'),
   '{"en":{"Name":"Bathroom Cleaning","Description":"Specialized bathroom cleaning with tile scrubbing and grout cleaning"},"cs":{"Name":"Úklid koupelny","Description":"Specializovaný úklid koupelny s drhnáním dlaždic a čištěním spár"},"sk":{"Name":"Upratovanie kúpeľne","Description":"Špecializované upratovanie kúpeľne s drhnutím dlaždíc a čistením škár"},"uk":{"Name":"Прибирання ванної","Description":"Спеціалізоване прибирання ванної з чищенням плитки та швів"},"ru":{"Name":"Уборка ванной","Description":"Специализированная уборка ванной с чисткой плитки и швов"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Kitchen Deep Clean', 'Comprehensive kitchen cleaning including oven, refrigerator, and cabinets',
   90,
   (SELECT "Id" FROM public."ServiceCategories" WHERE "Slug" = 'deep'),
   '{"en":{"Name":"Kitchen Deep Clean","Description":"Comprehensive kitchen cleaning including oven, refrigerator, and cabinets"},"cs":{"Name":"Hloubkový úklid kuchyně","Description":"Komplexní úklid kuchyně včetně trouby, lednice a skříněk"},"sk":{"Name":"Hĺbkové upratovanie kuchyne","Description":"Komplexné upratovanie kuchyne vrátane rúry, chladničky a skriniek"},"uk":{"Name":"Глибоке прибирання кухні","Description":"Комплексне прибирання кухні, включно з духовкою, холодильником та шафами"},"ru":{"Name":"Глубокая уборка кухни","Description":"Комплексная уборка кухни включая духовку, холодильник и шкафы"}}'),

  -- Specialized Services
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Window Cleaning', 'Interior and exterior window cleaning with streak-free finish',
   60,
   (SELECT "Id" FROM public."ServiceCategories" WHERE "Slug" = 'home'),
   '{"en":{"Name":"Window Cleaning","Description":"Interior and exterior window cleaning with streak-free finish"},"cs":{"Name":"Mytí oken","Description":"Mytí oken zevnitř i zvenčí bez šmouh"},"sk":{"Name":"Umývanie okien","Description":"Umývanie okien zvnútra aj zvonku bez šmúh"},"uk":{"Name":"Миття вікон","Description":"Миття вікон зсередини та зовні без розводів"},"ru":{"Name":"Мытье окон","Description":"Мытье окон изнутри и снаружи без разводов"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Carpet Cleaning', 'Professional carpet steam cleaning and stain removal',
   90,
   (SELECT "Id" FROM public."ServiceCategories" WHERE "Slug" = 'home'),
   '{"en":{"Name":"Carpet Cleaning","Description":"Professional carpet steam cleaning and stain removal"},"cs":{"Name":"Čištění koberců","Description":"Profesionální parní čištění koberců a odstraňování skvrn"},"sk":{"Name":"Čistenie kobercov","Description":"Profesionálne parné čistenie kobercov a odstraňovanie škvŕn"},"uk":{"Name":"Чищення килимів","Description":"Професійне парове чищення килимів та видалення плям"},"ru":{"Name":"Чистка ковров","Description":"Профессиональная паровая чистка ковров и удаление пятен"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Upholstery Cleaning', 'Deep cleaning of sofas, chairs, and fabric furniture',
   75,
   (SELECT "Id" FROM public."ServiceCategories" WHERE "Slug" = 'home'),
   '{"en":{"Name":"Upholstery Cleaning","Description":"Deep cleaning of sofas, chairs, and fabric furniture"},"cs":{"Name":"Čištění čalounění","Description":"Hloubkové čištění sedaček, židlí a látkového nábytku"},"sk":{"Name":"Čistenie čalúnenia","Description":"Hĺbkové čistenie sedačiek, stoličiek a látkového nábytku"},"uk":{"Name":"Чищення оббивки","Description":"Глибоке чищення диванів, крісел та тканинних меблів"},"ru":{"Name":"Чистка обивки","Description":"Глубокая чистка диванов, кресел и тканевой мебели"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Post-Construction Cleanup', 'Specialized cleaning after renovation or construction work',
   240,
   (SELECT "Id" FROM public."ServiceCategories" WHERE "Slug" = 'deep'),
   '{"en":{"Name":"Post-Construction Cleanup","Description":"Specialized cleaning after renovation or construction work"},"cs":{"Name":"Úklid po rekonstrukci","Description":"Specializovaný úklid po rekonstrukci nebo stavebních pracích"},"sk":{"Name":"Upratovanie po rekonštrukcii","Description":"Špecializované upratovanie po rekonštrukcii alebo stavebných prácach"},"uk":{"Name":"Прибирання після ремонту","Description":"Спеціалізоване прибирання після ремонту або будівельних робіт"},"ru":{"Name":"Уборка после ремонта","Description":"Специализированная уборка после ремонта или строительных работ"}}'),

  -- Premium Services
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Move-in/Move-out Cleaning', 'Complete cleaning for moving in or out of property',
   180,
   (SELECT "Id" FROM public."ServiceCategories" WHERE "Slug" = 'deep'),
   '{"en":{"Name":"Move-in/Move-out Cleaning","Description":"Complete cleaning for moving in or out of property"},"cs":{"Name":"Úklid při stěhování","Description":"Kompletní úklid při nastěhování nebo vystěhování z nemovitosti"},"sk":{"Name":"Upratovanie pri sťahovaní","Description":"Kompletné upratovanie pri nasťahovaní alebo vysťahovaní z nehnuteľnosti"},"uk":{"Name":"Прибирання при переїзді","Description":"Повне прибирання при в''їзді або виїзді з нерухомості"},"ru":{"Name":"Уборка при переезде","Description":"Полная уборка при въезде или выезде из недвижимости"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Eco-Friendly Cleaning', 'Green cleaning using only eco-friendly and non-toxic products',
   135,
   (SELECT "Id" FROM public."ServiceCategories" WHERE "Slug" = 'home'),
   '{"en":{"Name":"Eco-Friendly Cleaning","Description":"Green cleaning using only eco-friendly and non-toxic products"},"cs":{"Name":"Ekologický úklid","Description":"Zelený úklid používající pouze ekologické a netoxické produkty"},"sk":{"Name":"Ekologické upratovanie","Description":"Zelené upratovanie používajúce iba ekologické a netoxické produkty"},"uk":{"Name":"Екологічне прибирання","Description":"Зелене прибирання з використанням лише екологічних та нетоксичних продуктів"},"ru":{"Name":"Экологическая уборка","Description":"Зеленая уборка с использованием только экологически чистых и нетоксичных продуктов"}}');

-- 7b. EXTRAS — booking add-ons (inside-oven, inside-fridge, etc.)
-- Prices are placeholders per the spec (booking-extras-and-surcharge.md §1a);
-- PM should sanity-check before production seed. All 5 locales translated
-- in-line so the GetExtraOverview endpoint serves localized strings out of
-- the box.
INSERT INTO public."Extras" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "Slug", "Name", "Description",
  "DisplayOrder", "Translations"
)
VALUES
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'inside-oven', 'Inside oven cleaning',
   'Degrease, scrub, wipe down — bring the oven back to factory clean.',
   10,
   '{"en": {"Name": "Inside oven cleaning", "Description": "Degrease, scrub, wipe down — bring the oven back to factory clean."}, "cs": {"Name": "Čištění vnitřku trouby", "Description": "Odmaštění, vydrhnutí, otření — trouba bude jako nová."}, "sk": {"Name": "Čistenie vnútra rúry", "Description": "Odmastenie, vydrhnutie, utretie — rúra bude ako nová."}, "uk": {"Name": "Чистка духовки зсередини", "Description": "Знежирення, миття, протирання — духовка як нова."}, "ru": {"Name": "Чистка духовки изнутри", "Description": "Обезжиривание, оттирание, протирка — духовка как новая."}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'inside-fridge', 'Inside fridge cleaning',
   'Empty, clean, wipe down, reassemble. Assumes the fridge has been emptied beforehand.',
   20,
   '{"en": {"Name": "Inside fridge cleaning", "Description": "Empty, clean, wipe down, reassemble. Assumes the fridge has been emptied beforehand."}, "cs": {"Name": "Čištění vnitřku ledničky", "Description": "Vyprázdnit, vyčistit, otřít, složit zpět. Předpokládá vyprázdněnou ledničku."}, "sk": {"Name": "Čistenie vnútra chladničky", "Description": "Vyprázdniť, vyčistiť, utrieť, zložiť. Predpokladá vyprázdnenú chladničku."}, "uk": {"Name": "Чистка холодильника зсередини", "Description": "Випорожнити, помити, протерти, зібрати назад. Холодильник має бути порожнім."}, "ru": {"Name": "Чистка холодильника изнутри", "Description": "Освободить, помыть, протереть, собрать. Холодильник должен быть опустошён."}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'interior-windows', 'Interior windows',
   'Streak-free clean on the inside of the windows (exterior is a separate service).',
   30,
   '{"en": {"Name": "Interior windows", "Description": "Streak-free clean on the inside of the windows (exterior is a separate service)."}, "cs": {"Name": "Vnitřní okna", "Description": "Mytí oken zevnitř bez šmouh (vnější strana je samostatná služba)."}, "sk": {"Name": "Vnútorné okná", "Description": "Umytie okien zvnútra bez šmúh (vonkajšia strana je samostatná služba)."}, "uk": {"Name": "Внутрішні вікна", "Description": "Прозоре миття вікон зсередини (зовнішня сторона — окрема послуга)."}, "ru": {"Name": "Окна изнутри", "Description": "Прозрачное мытьё окон изнутри (внешняя сторона — отдельная услуга)."}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'laundry-ironing', 'Laundry & ironing',
   'Up to one hour of laundry and ironing as part of the visit.',
   40,
   '{"en": {"Name": "Laundry & ironing", "Description": "Up to one hour of laundry and ironing as part of the visit."}, "cs": {"Name": "Praní a žehlení", "Description": "Až jedna hodina praní a žehlení v rámci úklidu."}, "sk": {"Name": "Pranie a žehlenie", "Description": "Až jedna hodina prania a žehlenia v rámci upratovania."}, "uk": {"Name": "Прання та прасування", "Description": "До однієї години прання та прасування під час візиту."}, "ru": {"Name": "Стирка и глажка", "Description": "До часа стирки и глажки во время уборки."}}'),

  -- Retired inactive: the Increased dirtiness level covers homes with pets, so the extra would charge
  -- a pet owner twice for the same effort (owner ruling 2026-09-28).
  (generate_ulid()::TEXT, false, 'system', CURRENT_TIMESTAMP, NULL, NULL, 'system', CURRENT_TIMESTAMP,
   'pet-hair-supplement', 'Pet hair deep-clean',
   'Extra effort on pet hair removal for homes with shedding pets.',
   50,
   '{"en": {"Name": "Pet hair deep-clean", "Description": "Extra effort on pet hair removal for homes with shedding pets."}, "cs": {"Name": "Důkladné odstranění zvířecích chlupů", "Description": "Extra péče o odstranění chlupů v domech s línajícími mazlíčky."}, "sk": {"Name": "Dôkladné odstránenie srsti", "Description": "Extra starostlivosť o odstránenie srsti v domoch s línajúcimi zvieratami."}, "uk": {"Name": "Глибоке прибирання шерсті тварин", "Description": "Додаткові зусилля для прибирання шерсті в домах з тваринами."}, "ru": {"Name": "Глубокая уборка шерсти животных", "Description": "Дополнительные усилия по уборке шерсти в домах с линяющими питомцами."}}');

-- 8. PACKAGES
INSERT INTO public."Packages" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "Name", "Description", "Tagline", "IsPopular", "Translations"
)
VALUES
  -- Basic Packages
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Essential Clean', 'Perfect for regular maintenance cleaning of your home',
   'For a well-kept home', false,
   '{"en":{"Name":"Essential Clean","Description":"Perfect for regular maintenance cleaning of your home","Tagline":"For a well-kept home"},"cs":{"Name":"Základní úklid","Description":"Ideální pro pravidelný udržovací úklid vašeho domova","Tagline":"Pro udržovaný byt"},"sk":{"Name":"Základné upratovanie","Description":"Ideálne pre pravidelné udržiavacie upratovanie vášho domova","Tagline":"Pre udržiavaný byt"},"uk":{"Name":"Основне прибирання","Description":"Ідеально для регулярного підтримуючого прибирання вашого дому","Tagline":"Для доглянутої оселі"},"ru":{"Name":"Основная уборка","Description":"Идеально для регулярной поддерживающей уборки вашего дома","Tagline":"Для ухоженного дома"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Complete Home Clean', 'Comprehensive cleaning package for the entire home',
   'Most common choice', true,
   '{"en":{"Name":"Complete Home Clean","Description":"Comprehensive cleaning package for the entire home","Tagline":"Most common choice"},"cs":{"Name":"Kompletní úklid domova","Description":"Komplexní úklidový balíček pro celý domov","Tagline":"Nejčastější volba"},"sk":{"Name":"Kompletné upratovanie domova","Description":"Komplexný upratovací balík pre celý domov","Tagline":"Najčastejšia voľba"},"uk":{"Name":"Повне прибирання дому","Description":"Комплексний пакет прибирання для всього дому","Tagline":"Найчастіший вибір"},"ru":{"Name":"Полная уборка дома","Description":"Комплексный пакет уборки для всего дома","Tagline":"Самый частый выбор"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Deep Clean Premium', 'Intensive deep cleaning for thoroughly clean spaces',
   'When it must be spotless', false,
   '{"en":{"Name":"Deep Clean Premium","Description":"Intensive deep cleaning for thoroughly clean spaces","Tagline":"When it must be spotless"},"cs":{"Name":"Prémiový hloubkový úklid","Description":"Intenzivní hloubkový úklid pro dokonale čisté prostory","Tagline":"Když musí být dokonale"},"sk":{"Name":"Prémiové hĺbkové upratovanie","Description":"Intenzívne hĺbkové upratovanie pre dokonale čisté priestory","Tagline":"Keď musí byť dokonale"},"uk":{"Name":"Преміум глибоке прибирання","Description":"Інтенсивне глибоке прибирання для ідеально чистих приміщень","Tagline":"Коли має бути бездоганно"},"ru":{"Name":"Премиум глубокая уборка","Description":"Интенсивная глубокая уборка для идеально чистых помещений","Tagline":"Когда должно быть безупречно"}}'),

  -- Specialized Packages
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Kitchen & Bathroom Focus', 'Specialized package focusing on kitchen and bathroom deep cleaning',
   'The two hardest rooms', false,
   '{"en":{"Name":"Kitchen & Bathroom Focus","Description":"Specialized package focusing on kitchen and bathroom deep cleaning","Tagline":"The two hardest rooms"},"cs":{"Name":"Zaměření na kuchyň a koupelnu","Description":"Specializovaný balíček zaměřený na hloubkový úklid kuchyně a koupelny","Tagline":"Dvě nejnáročnější místnosti"},"sk":{"Name":"Zameranie na kuchyňu a kúpeľňu","Description":"Špecializovaný balík zameraný na hĺbkové upratovanie kuchyne a kúpeľne","Tagline":"Dve najnáročnejšie miestnosti"},"uk":{"Name":"Фокус на кухню та ванну","Description":"Спеціалізований пакет з акцентом на глибоке прибирання кухні та ванної","Tagline":"Дві найскладніші кімнати"},"ru":{"Name":"Фокус на кухню и ванную","Description":"Специализированный пакет с акцентом на глубокую уборку кухни и ванной","Tagline":"Две самые сложные комнаты"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Eco-Green Package', 'Complete eco-friendly cleaning using only green products',
   'Green products only', false,
   '{"en":{"Name":"Eco-Green Package","Description":"Complete eco-friendly cleaning using only green products","Tagline":"Green products only"},"cs":{"Name":"Eko-zelený balíček","Description":"Kompletní ekologický úklid používající pouze zelené produkty","Tagline":"Jen zelené prostředky"},"sk":{"Name":"Eko-zelený balík","Description":"Kompletné ekologické upratovanie používajúce iba zelené produkty","Tagline":"Len zelené prostriedky"},"uk":{"Name":"Еко-зелений пакет","Description":"Повне екологічне прибирання з використанням лише зелених продуктів","Tagline":"Лише зелені засоби"},"ru":{"Name":"Эко-зеленый пакет","Description":"Полная экологическая уборка с использованием только зеленых продуктов","Tagline":"Только зелёные средства"}}'),

  -- Premium Packages
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Moving Day Special', 'Perfect for move-in or move-out situations',
   'Handover day', false,
   '{"en":{"Name":"Moving Day Special","Description":"Perfect for move-in or move-out situations","Tagline":"Handover day"},"cs":{"Name":"Speciál pro den stěhování","Description":"Ideální pro situace nastěhování nebo vystěhování","Tagline":"Předání bytu"},"sk":{"Name":"Špeciál pre deň sťahovania","Description":"Ideálne pre situácie nasťahovania alebo vysťahovania","Tagline":"Odovzdanie bytu"},"uk":{"Name":"Спеціальний пакет для переїзду","Description":"Ідеально для ситуацій в''''їзду або виїзду","Tagline":"Передача квартири"},"ru":{"Name":"Специальный пакет для переезда","Description":"Идеально для ситуаций въезда или выезда","Tagline":"Передача квартиры"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Post-Renovation Clean', 'Specialized cleaning after construction or renovation work',
   'After the builders', false,
   '{"en":{"Name":"Post-Renovation Clean","Description":"Specialized cleaning after construction or renovation work","Tagline":"After the builders"},"cs":{"Name":"Úklid po rekonstrukci","Description":"Specializovaný úklid po stavebních nebo rekonstrukčních pracích","Tagline":"Po řemeslnících"},"sk":{"Name":"Upratovanie po rekonštrukcii","Description":"Špecializované upratovanie po stavebných alebo rekonštrukčných prácach","Tagline":"Po remeselníkoch"},"uk":{"Name":"Прибирання після ремонту","Description":"Спеціалізоване прибирання після будівельних або ремонтних робіт","Tagline":"Після будівельників"},"ru":{"Name":"Уборка после ремонта","Description":"Специализированная уборка после строительных или ремонтных работ","Tagline":"После строителей"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Luxury Full Service', 'Premium package with all services included',
   'Everything, included', false,
   '{"en":{"Name":"Luxury Full Service","Description":"Premium package with all services included","Tagline":"Everything, included"},"cs":{"Name":"Luxusní kompletní služba","Description":"Prémiový balíček se všemi zahrnutými službami","Tagline":"Vše v jednom"},"sk":{"Name":"Luxusná kompletná služba","Description":"Prémiový balík so všetkými zahrnutými službami","Tagline":"Všetko v jednom"},"uk":{"Name":"Розкішний повний сервіс","Description":"Преміум пакет з усіма включеними послугами","Tagline":"Все в одному"},"ru":{"Name":"Роскошный полный сервис","Description":"Премиум пакет со всеми включенными услугами","Tagline":"Всё в одному"}}');

-- 8b. CATALOGUE PRICES, PER CURRENCY
-- Owner ruling 2026-09-08: a price is AUTHORED per currency, never converted. The exchange rate that
-- used to do the converting was one hand-typed column with no feed, no history and no per-order
-- snapshot, so editing it silently restated every order that referenced it.
--
-- Authored is why these are typed out and joined by name rather than derived: there is nothing left to
-- derive them FROM. A catalogue entry has no price of its own; the number below IS the price, and a
-- second market means a second block like this one with numbers somebody chose for it, not this block
-- multiplied by a rate.
--
-- CZK ONLY. EUR is seeded inactive with no prices, which is what makes "the machinery is built, only
-- CZK is reachable" provable rather than asserted: the customer catalogue withholds an entry with no
-- row in the currency being quoted, so an EUR catalogue today is correctly empty.
--
-- A NAME HERE THAT MATCHES NOTHING ABOVE SEEDS NOTHING, SILENTLY. That is the cost of authoring, and
-- it is covered rather than commented away: SeededCataloguePricingTests fails on any active catalogue
-- entry with no price row in the default currency.
INSERT INTO public."ServicePrices" (
  "Id", "IsActive", "CreatedBy", "CreatedOn", "UpdatedBy", "UpdatedOn",
  "DeactivatedBy", "DeactivatedOn", "ServiceId", "CurrencyId", "BasePrice", "PerRoomPrice"
)
SELECT generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
       s."Id", c."Id", v."BasePrice", v."PerRoomPrice"
FROM (VALUES
  ('General Cleaning',          500.00, 150.00),
  ('Deep Cleaning',             800.00, 250.00),
  ('Bathroom Cleaning',         300.00,   0.00),
  ('Kitchen Deep Clean',        400.00,   0.00),
  ('Window Cleaning',           200.00,  50.00),
  ('Carpet Cleaning',           350.00, 100.00),
  ('Upholstery Cleaning',       450.00,   0.00),
  ('Post-Construction Cleanup', 1200.00, 300.00),
  ('Move-in/Move-out Cleaning', 1000.00, 200.00),
  ('Eco-Friendly Cleaning',     600.00, 180.00)
) AS v("Name", "BasePrice", "PerRoomPrice")
JOIN public."Services" s ON s."Name" = v."Name"
CROSS JOIN (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1) c;

INSERT INTO public."PackagePrices" (
  "Id", "IsActive", "CreatedBy", "CreatedOn", "UpdatedBy", "UpdatedOn",
  "DeactivatedBy", "DeactivatedOn", "PackageId", "CurrencyId", "Price"
)
SELECT generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
       pk."Id", c."Id", v."Price"
FROM (VALUES
  ('Essential Clean',          799.00),
  ('Complete Home Clean',     1299.00),
  ('Deep Clean Premium',      1799.00),
  ('Kitchen & Bathroom Focus', 999.00),
  ('Eco-Green Package',       1499.00),
  ('Moving Day Special',      2299.00),
  ('Post-Renovation Clean',   2799.00),
  ('Luxury Full Service',     3499.00)
) AS v("Name", "Price")
JOIN public."Packages" pk ON pk."Name" = v."Name"
CROSS JOIN (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1) c;

-- Placeholders per booking-extras-and-surcharge.md 1a; PM sanity-checks before a production seed.
INSERT INTO public."ExtraPrices" (
  "Id", "IsActive", "CreatedBy", "CreatedOn", "UpdatedBy", "UpdatedOn",
  "DeactivatedBy", "DeactivatedOn", "ExtraId", "CurrencyId", "Price"
)
SELECT generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
       e."Id", c."Id", v."Price"
FROM (VALUES
  ('inside-oven',         200.00),
  ('inside-fridge',       150.00),
  ('interior-windows',    100.00),
  ('laundry-ironing',     250.00),
  ('pet-hair-supplement', 150.00)
) AS v("Slug", "Price")
JOIN public."Extras" e ON e."Slug" = v."Slug"
CROSS JOIN (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1) c;

-- 9. EMPLOYEE PAY CONFIGS
-- Every catalogue entry gets the PLATFORM-WIDE row (EmployeeId NULL). This is not optional data: an
-- entry with no platform-wide config quotes NOTHING on every cleaner's board at once, so the booking
-- wizard withholds it, no order may carry it, and no cleaner may be approved while one exists. A
-- per-employee override is not a substitute — it answers for one cleaner and leaves the rest blank.
--
-- Derived from the PRICE ROWS rather than from the catalogue, so the pay and the price it is a share
-- of are in the same currency by construction. That is not a tidiness point: pay is a multiple of a
-- price, and taking the multiple from one currency while stamping the row with another is roughly a
-- 24x error in what a cleaner is paid. BulkCreateEmployeePayConfigs joins the same way for the same
-- reason. The rate is the STANDARD rate template's multiplier (0.5), the same number that command uses
-- for that template — a starting point the admin tunes per entry or per cleaner, not a pricing decision
-- made in a seed file. Pay rates are the operator's money, so the rows are stamped with the operating
-- company (ADR-0061 D7); an admin of another company reads and writes its own defaults.
INSERT INTO public."EmployeePayConfigs" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy", "DeactivatedOn",
  "TenantId", "EmployeeId", "ServiceId", "PackageId",
  "BasePay", "ExtraPerRoom", "ExtraPerBathroom",
  "Description", "CurrencyId", "MinimumPay", "MaximumPay"
)
SELECT generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
       'cleansia-cz', NULL, sp."ServiceId", NULL,
       ROUND(sp."BasePrice" * 0.5, 2), ROUND(sp."PerRoomPrice" * 0.5, 2), 0,
       'Platform-wide default (standard rate template)',
       sp."CurrencyId",
       0, 0
FROM public."ServicePrices" sp
WHERE sp."CurrencyId" = (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1);

INSERT INTO public."EmployeePayConfigs" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy", "DeactivatedOn",
  "TenantId", "EmployeeId", "ServiceId", "PackageId",
  "BasePay", "ExtraPerRoom", "ExtraPerBathroom",
  "Description", "CurrencyId", "MinimumPay", "MaximumPay"
)
SELECT generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
       'cleansia-cz', NULL, NULL, pp."PackageId",
       ROUND(pp."Price" * 0.5, 2), 0, 0,
       'Platform-wide default (standard rate template)',
       pp."CurrencyId",
       0, 0
FROM public."PackagePrices" pp
WHERE pp."CurrencyId" = (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1);

-- 12. ORDERS AND RELATED DATA
-- First insert package services relationships
INSERT INTO public."PackageServices" (
  "Id", "IsActive", "PackageId", "ServiceId"
)
VALUES
  -- Essential Clean Package Services
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Essential Clean' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'General Cleaning' LIMIT 1)),
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Essential Clean' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Bathroom Cleaning' LIMIT 1)),

  -- Complete Home Clean Package Services
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Complete Home Clean' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'General Cleaning' LIMIT 1)),
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Complete Home Clean' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Bathroom Cleaning' LIMIT 1)),
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Complete Home Clean' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Window Cleaning' LIMIT 1)),

  -- Deep Clean Premium Package Services
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Deep Clean Premium' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Deep Cleaning' LIMIT 1)),
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Deep Clean Premium' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Kitchen Deep Clean' LIMIT 1)),
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Deep Clean Premium' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Bathroom Cleaning' LIMIT 1)),

  -- Kitchen & Bathroom Focus Package Services
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Kitchen & Bathroom Focus' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Kitchen Deep Clean' LIMIT 1)),
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Kitchen & Bathroom Focus' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Bathroom Cleaning' LIMIT 1)),

  -- Eco-Green Package Services
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Eco-Green Package' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Eco-Friendly Cleaning' LIMIT 1)),
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Eco-Green Package' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'General Cleaning' LIMIT 1)),

  -- Moving Day Special Package Services
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Moving Day Special' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Move-in/Move-out Cleaning' LIMIT 1)),
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Moving Day Special' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Deep Cleaning' LIMIT 1)),

  -- Post-Renovation Clean Package Services
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Post-Renovation Clean' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Post-Construction Cleanup' LIMIT 1)),
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Post-Renovation Clean' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Deep Cleaning' LIMIT 1)),

  -- Luxury Full Service Package Services
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Luxury Full Service' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Deep Cleaning' LIMIT 1)),
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Luxury Full Service' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Window Cleaning' LIMIT 1)),
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Luxury Full Service' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Carpet Cleaning' LIMIT 1)),
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Luxury Full Service' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Upholstery Cleaning' LIMIT 1));

-- ============================================================
-- COUNTRY INVOICE CONFIGS — the markets DEV configures but does not operate
-- ============================================================
-- Czechia's row, and the one legal notice, are in prod-bootstrap.sql. "LegalDisclaimerTemplate" is
-- deliberately absent from this INSERT: every row here starts with NO legal notice and
-- LegalDisclaimerReviewStatus = 0 (NotReviewed), so the invoice prints the platform's generic English
-- fallback. The review status is written EXPLICITLY: the column is NOT NULL and carries no database
-- default, so omitting it aborts the whole seed transaction with 23502 and a fresh database comes up
-- empty.
INSERT INTO public."CountryInvoiceConfigs" (
  "Id", "IsActive", "CountryId", "VatRequired", "VatRate",
  "DigitalSignatureRequired", "EInvoiceFormat",
  "AdditionalFieldsJson", "LegalDisclaimerReviewStatus"
)
VALUES
  -- Germany - VAT 19%
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'DEU' LIMIT 1),
   true, 0.19, false, 'PDF',
   '{"TaxNumber": "required", "UStIdNr": "optional"}', 0),

  -- Austria - VAT 20%
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'AUT' LIMIT 1),
   true, 0.20, false, 'PDF', NULL, 0),

  -- Poland - VAT 23%
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'POL' LIMIT 1),
   true, 0.23, false, 'PDF',
   '{"NIP": "required"}', 0),

  -- Slovakia - VAT 20%
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'SVK' LIMIT 1),
   true, 0.20, false, 'PDF', NULL, 0),

  -- United States - No VAT
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'USA' LIMIT 1),
   false, 0.00, false, 'PDF',
   '{"EIN": "optional", "StateTaxId": "optional"}', 0),

  -- United Kingdom - VAT 20%
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'GBR' LIMIT 1),
   true, 0.20, false, 'PDF',
   '{"VATNumber": "required"}', 0),

  -- France - VAT 20%
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'FRA' LIMIT 1),
   true, 0.20, false, 'PDF',
   '{"SIRET": "required"}', 0),

  -- Italy - VAT 22%
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'ITA' LIMIT 1),
   true, 0.22, false, 'PDF+XML',
   '{"CodiceFiscale": "required", "PartitaIVA": "required"}', 0),

  -- Spain - VAT 21%
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'ESP' LIMIT 1),
   true, 0.21, false, 'PDF',
   '{"NIF": "required"}', 0);

-- ============================================================
-- COUNTRY CONFIGURATIONS — the markets DEV configures but does not operate
-- ============================================================
-- Czechia's row, the default market with its operating company, is in prod-bootstrap.sql. None of
-- these has an operator, so GetMarkets lists none of them and an anonymous write to one is refused
-- with tenant.not_found until a company is assigned to it.
INSERT INTO public."CountryConfigurations" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy", "DeactivatedOn",
  "CountryId", "DefaultCurrencyCode", "DefaultLanguageCode",
  "DateFormat", "TimeZoneId", "PhonePrefix",
  "StandardVatRate",
  "TaxIdLabel", "TaxIdFormat",
  "RegistrationNumberLabel", "RegistrationNumberFormat", "RegistrationNumberRequired",
  "VatNumberLabel", "VatNumberFormat", "VatNumberRequired",
  "DefaultPaymentGateway", "PayoutScheme",
  "InsuranceCoverageAmount", "IsDefaultMarket", "OperatorTenantId"
)
VALUES
  -- Slovakia — IČO mandatory, IČ DPH (VAT) optional
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'SVK' LIMIT 1),
   'EUR', 'sk', 'dd.MM.yyyy', 'Europe/Bratislava', '+421',
   0.20, 'IČO', '^\d{8}$',
   'IČO', '^\d{8}$', true,
   'IČ DPH', '^SK\d{10}$', false,
   'Stripe', 1,
   NULL, false, NULL),

  -- Poland — NIP mandatory, EU VAT optional
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'POL' LIMIT 1),
   'PLN', 'pl', 'dd.MM.yyyy', 'Europe/Warsaw', '+48',
   0.23, 'NIP', '^\d{10}$',
   'NIP', '^\d{10}$', true,
   'VAT UE', '^PL\d{10}$', false,
   'Stripe', NULL,
   NULL, false, NULL),

  -- Germany — Steuernummer mandatory, USt-IdNr optional
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'DEU' LIMIT 1),
   'EUR', 'de', 'dd.MM.yyyy', 'Europe/Berlin', '+49',
   0.19, 'Steuernummer', '^\d{10,13}$',
   'Steuernummer', '^\d{10,13}$', true,
   'USt-IdNr', '^DE\d{9}$', false,
   'Stripe', NULL,
   NULL, false, NULL),

  -- Austria — Firmenbuchnummer mandatory, UID (VAT) optional
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'AUT' LIMIT 1),
   'EUR', 'de', 'dd.MM.yyyy', 'Europe/Vienna', '+43',
   0.20, 'UID-Nummer', '^ATU\d{8}$',
   'Firmenbuchnummer', '^[A-Z]?\d{1,6}[a-z]?$', true,
   'UID-Nummer', '^ATU\d{8}$', false,
   'Stripe', NULL,
   NULL, false, NULL),

  -- United Kingdom — UTR mandatory, VAT number optional
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'GBR' LIMIT 1),
   'GBP', 'en', 'dd/MM/yyyy', 'Europe/London', '+44',
   0.20, 'UTR', '^\d{10}$',
   'UTR', '^\d{10}$', true,
   'VAT Number', '^GB\d{9}$', false,
   'Stripe', NULL,
   NULL, false, NULL),

  -- France — SIRET mandatory, TVA intracommunautaire optional
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'FRA' LIMIT 1),
   'EUR', 'fr', 'dd/MM/yyyy', 'Europe/Paris', '+33',
   0.20, 'SIRET', '^\d{14}$',
   'SIRET', '^\d{14}$', true,
   'TVA', '^FR[A-Z0-9]{2}\d{9}$', false,
   'Stripe', NULL,
   NULL, false, NULL),

  -- Italy — Codice Fiscale mandatory, Partita IVA optional
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'ITA' LIMIT 1),
   'EUR', 'it', 'dd/MM/yyyy', 'Europe/Rome', '+39',
   0.22, 'Codice Fiscale', '^[A-Z]{6}\d{2}[A-Z]\d{2}[A-Z]\d{3}[A-Z]$',
   'Codice Fiscale', '^[A-Z]{6}\d{2}[A-Z]\d{2}[A-Z]\d{3}[A-Z]$', true,
   'Partita IVA', '^IT\d{11}$', false,
   'Stripe', NULL,
   NULL, false, NULL),

  -- Spain — NIF mandatory, NIF-IVA optional
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'ESP' LIMIT 1),
   'EUR', 'es', 'dd/MM/yyyy', 'Europe/Madrid', '+34',
   0.21, 'NIF', '^[A-Z]\d{7}[A-Z0-9]$',
   'NIF', '^[A-Z]\d{7}[A-Z0-9]$', true,
   'NIF-IVA', '^ES[A-Z0-9]\d{7}[A-Z0-9]$', false,
   'Stripe', NULL,
   NULL, false, NULL),

  -- United States — EIN mandatory, no separate VAT
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'USA' LIMIT 1),
   'USD', 'en', 'MM/dd/yyyy', 'America/New_York', '+1',
   0.00, 'EIN', '^\d{2}-\d{7}$',
   'EIN', '^\d{2}-\d{7}$', true,
   NULL, NULL, false,
   'Stripe', NULL,
   NULL, false, NULL);

-- ============================================================
-- EMPLOYEE DOCUMENT REQUIREMENTS — Slovakia takes Czechia's
-- ============================================================
INSERT INTO public."EmployeeDocumentRequirements" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy", "DeactivatedOn",
  "CountryId", "DocumentType", "IsRequired", "SortOrder"
)
SELECT
  generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
  sk."Id", r."DocumentType", r."IsRequired", r."SortOrder"
FROM public."EmployeeDocumentRequirements" r
JOIN public."Countries" cz ON cz."Id" = r."CountryId"
CROSS JOIN public."Countries" sk
WHERE cz."IsoCode" = 'CZE' AND sk."IsoCode" = 'SVK'
ON CONFLICT ("CountryId", "DocumentType") DO NOTHING;

-- ============================================================
-- COMPANY INFO
-- ============================================================
-- The legal issuer on every receipt and invoice — literally the operating company, so the row is
-- stamped with it (ADR-0061 D7). A second company seeds its own row under its own tenant id.
INSERT INTO public."CompanyInfo" (
    "Id", "TenantId", "LegalName", "TradingName", "Tagline",
    "RegistrationNumber", "VatNumber", "IsVatPayer",
    "Street", "City", "ZipCode", "CountryId",
    "Phone", "Email", "Website",
    "BankName", "BankAccountNumber", "Iban", "Swift",
    "IsActive", "CreatedBy", "CreatedOn", "UpdatedBy", "UpdatedOn"
)
VALUES (
    generate_ulid()::TEXT,
    'cleansia-cz',
    'Cleansia s.r.o.',
    'CLEANSIA',
    'Professional Cleaning Services',
    '12345678',  -- IČO (Registration Number) - REPLACE WITH ACTUAL
    'CZ12345678',  -- DIČ (VAT Number) - REPLACE WITH ACTUAL
    false,       -- IsVatPayer — seed company is not a VAT payer ("Nejsme plátci DPH")
    'Václavské náměstí 1',
    'Prague',
    '11000',
    (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE' LIMIT 1),
    '+420 123 456 789',
    'info@cleansia.cz',
    'https://www.cleansia.cz',
    'Česká spořitelna',
    '123456789/0800',
    'CZ65 0800 0000 1234 5678 9012',
    'GIBACZPX',
    true,
    'system',
    CURRENT_TIMESTAMP,
    'system',
    CURRENT_TIMESTAMP
);

-- ============================================================
-- DISPUTES
-- ============================================================
-- Nothing seeds disputes. The old seed/insert_disputes.sql selected orders this file never
-- creates and was deleted on 2026-09-12 (owner ruling; in git history).

-- ============================================================
-- PROMO CODES (Phase B seed)
-- ============================================================
-- Idempotent inserts keyed on Code (which is unique per tenant).
-- Type: 1 = PercentDiscount (uses DiscountPercent), 2 = FixedDiscount (uses DiscountAmount + CurrencyId).
-- DiscountPercent stored as fraction in [0, 1] — backend renders as percentage.
-- A code gives away the operating company's money, so each is stamped with it (ADR-0061 D7).

-- WELCOME15 — 15% off first booking, no minimum, single-use per user.
INSERT INTO public."PromoCodes" (
    "Id", "IsActive", "CreatedBy", "CreatedOn", "UpdatedBy", "UpdatedOn",
    "DeactivatedBy", "DeactivatedOn", "TenantId",
    "Code", "Type", "DiscountPercent", "DiscountAmount", "CurrencyId",
    "MinimumOrderAmount", "MaxRedemptionsPerUser", "GlobalMaxRedemptions",
    "CurrentRedemptionsCount", "ValidFrom", "ValidUntil", "Description"
)
SELECT '01PROMOWELCOME150000000000', true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'cleansia-cz',
    'WELCOME15', 1, 0.1500, NULL, NULL,
    NULL, 1, NULL,
    0, NULL, NULL, 'Welcome offer — 15% off the first booking. Single-use per user.'
WHERE NOT EXISTS (SELECT 1 FROM public."PromoCodes" WHERE "Code" = 'WELCOME15' AND "TenantId" = 'cleansia-cz');

-- SPRING20 — 20% off bookings >= 1500 CZK, single-use per user.
INSERT INTO public."PromoCodes" (
    "Id", "IsActive", "CreatedBy", "CreatedOn", "UpdatedBy", "UpdatedOn",
    "DeactivatedBy", "DeactivatedOn", "TenantId",
    "Code", "Type", "DiscountPercent", "DiscountAmount", "CurrencyId",
    "MinimumOrderAmount", "MaxRedemptionsPerUser", "GlobalMaxRedemptions",
    "CurrentRedemptionsCount", "ValidFrom", "ValidUntil", "Description"
)
SELECT '01PROMOSPRING20000000000A', true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'cleansia-cz',
    'SPRING20', 1, 0.2000, NULL, NULL,
    1500.00, 1, NULL,
    0, NULL, NULL, 'Seasonal — 20% off bookings of 1500 CZK or more.'
WHERE NOT EXISTS (SELECT 1 FROM public."PromoCodes" WHERE "Code" = 'SPRING20' AND "TenantId" = 'cleansia-cz');

-- LOYAL10 — 10% off bookings >= 800 CZK, repeatable up to 5 times per user.
-- Useful for return-customer marketing pushes.
INSERT INTO public."PromoCodes" (
    "Id", "IsActive", "CreatedBy", "CreatedOn", "UpdatedBy", "UpdatedOn",
    "DeactivatedBy", "DeactivatedOn", "TenantId",
    "Code", "Type", "DiscountPercent", "DiscountAmount", "CurrencyId",
    "MinimumOrderAmount", "MaxRedemptionsPerUser", "GlobalMaxRedemptions",
    "CurrentRedemptionsCount", "ValidFrom", "ValidUntil", "Description"
)
SELECT '01PROMOLOYAL100000000000B', true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'cleansia-cz',
    'LOYAL10', 1, 0.1000, NULL, NULL,
    800.00, 5, NULL,
    0, NULL, NULL, 'Return-customer offer — 10% off bookings of 800 CZK or more, up to 5 uses per user.'
WHERE NOT EXISTS (SELECT 1 FROM public."PromoCodes" WHERE "Code" = 'LOYAL10' AND "TenantId" = 'cleansia-cz');

-- ─── Cleansia Plus membership plans ───
-- Two plans: monthly + yearly. The price and the Stripe Price id live on MembershipPlanPrices, one
-- row per currency (ADR-0059): CZK below, nothing for EUR until the owner mints EUR Stripe Prices
-- and the admin enters them. The monthly→yearly upgrade path (SwapMembershipPlan command) reads
-- BillingInterval to know which plan is the "upgrade target".
-- TrialPeriodDays is 14 on both (owner ruling 2026-09-30): a first-time subscriber gets a 14-day free
-- trial with every Plus benefit from day one, and is charged when it ends unless they cancel before.
-- One trial per account — MembershipTrialResolver sends 0 days to Stripe for anyone who has had one.
-- A deployed database gets its plans from the admin console, where the trial length is per plan.

-- PLUS_MONTHLY
INSERT INTO public."MembershipPlans" (
    "Id", "IsActive", "CreatedBy", "CreatedOn", "UpdatedBy", "UpdatedOn",
    "DeactivatedBy", "DeactivatedOn",
    "Code", "Name",
    "DiscountPercentage", "FreeCancellationWindowHours", "AllowsExpressUpgrade",
    "ExpressUpgradesPerMonth",
    "BillingInterval", "TrialPeriodDays"
)
SELECT '01PLUSMONTHLY00000000000A', true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
    'PLUS_MONTHLY', 'Cleansia Plus (Monthly)',
    5.00, 4, true,
    1,
    1, 14
WHERE NOT EXISTS (SELECT 1 FROM public."MembershipPlans" WHERE "Code" = 'PLUS_MONTHLY');

-- PLUS_YEARLY (the annual charge is on its price row; ≈15% off vs monthly in CZK).
INSERT INTO public."MembershipPlans" (
    "Id", "IsActive", "CreatedBy", "CreatedOn", "UpdatedBy", "UpdatedOn",
    "DeactivatedBy", "DeactivatedOn",
    "Code", "Name",
    "DiscountPercentage", "FreeCancellationWindowHours", "AllowsExpressUpgrade",
    "ExpressUpgradesPerMonth",
    "BillingInterval", "TrialPeriodDays"
)
SELECT '01PLUSYEARLY000000000000A', true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
    'PLUS_YEARLY', 'Cleansia Plus (Annual)',
    5.00, 4, true,
    1,
    2, 14
WHERE NOT EXISTS (SELECT 1 FROM public."MembershipPlans" WHERE "Code" = 'PLUS_YEARLY');

-- One price per (plan, currency). CZK only: 199 Kč/month and 2030 Kč/year against the two sandbox
-- Stripe Prices. A currency with no row here is a market where Plus is not on sale (ADR-0059 D4).
INSERT INTO public."MembershipPlanPrices" (
  "Id", "IsActive", "CreatedBy", "CreatedOn", "UpdatedBy", "UpdatedOn",
  "DeactivatedBy", "DeactivatedOn", "MembershipPlanId", "CurrencyId", "Price", "StripePriceId"
)
SELECT generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
       p."Id", c."Id", v."Price", v."StripePriceId"
FROM (VALUES
  ('PLUS_MONTHLY',  199.00, 'price_1TSiJ83KjMqxM0RBVaiKAF6r'),
  ('PLUS_YEARLY',  2030.00, 'price_1TSiJ83KjMqxM0RBrfMWdjrF')
) AS v("Code", "Price", "StripePriceId")
JOIN public."MembershipPlans" p ON p."Code" = v."Code"
CROSS JOIN (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1) c
WHERE NOT EXISTS (
  SELECT 1 FROM public."MembershipPlanPrices" mp WHERE mp."MembershipPlanId" = p."Id" AND mp."CurrencyId" = c."Id"
);

-- ============================================================================
-- PropertySizePresets — Slovakia takes Czechia's ladder under its own codes
-- ============================================================================
INSERT INTO public."PropertySizePresets" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy", "DeactivatedOn",
  "CountryId", "Code", "SortOrder", "Rooms", "Bathrooms", "Translations"
)
SELECT generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
       sk."Id", REPLACE(ps."Code", 'CZE_', 'SVK_'), ps."SortOrder", ps."Rooms", ps."Bathrooms", ps."Translations"
FROM public."PropertySizePresets" ps
JOIN public."Countries" cz ON cz."Id" = ps."CountryId"
CROSS JOIN public."Countries" sk
WHERE cz."IsoCode" = 'CZE' AND sk."IsoCode" = 'SVK'
ON CONFLICT ("CountryId", "Code") DO NOTHING;

-- Constraints are checked at COMMIT when using SET CONSTRAINTS ALL DEFERRED

COMMIT;
