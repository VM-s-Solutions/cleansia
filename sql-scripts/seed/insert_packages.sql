-- INSERT PACKAGES
INSERT INTO public."Packages" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "Name", "Description", "Price", "Translations"
)
VALUES
  -- Basic Packages
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Essential Clean', 'Perfect for regular maintenance cleaning of your home',
   799.00,
   '{"en": {"Name": "Essential Clean", "Description": "Perfect for regular maintenance cleaning of your home"}, "cs": {"Name": "Základní úklid", "Description": "Ideální pro pravidelný udržovací úklid vašeho domova"}, "ru": {"Name": "Основная уборка", "Description": "Идеально для регулярной поддерживающей уборки вашего дома"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Complete Home Clean', 'Comprehensive cleaning package for the entire home',
   1299.00,
   '{"en": {"Name": "Complete Home Clean", "Description": "Comprehensive cleaning package for the entire home"}, "cs": {"Name": "Kompletní úklid domova", "Description": "Komplexní úklidový balíček pro celý domov"}, "ru": {"Name": "Полная уборка дома", "Description": "Комплексный пакет уборки для всего дома"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Deep Clean Premium', 'Intensive deep cleaning for thoroughly clean spaces',
   1799.00,
   '{"en": {"Name": "Deep Clean Premium", "Description": "Intensive deep cleaning for thoroughly clean spaces"}, "cs": {"Name": "Prémiový hloubkový úklid", "Description": "Intenzivní hloubkový úklid pro dokonale čisté prostory"}, "ru": {"Name": "Премиум глубокая уборка", "Description": "Интенсивная глубокая уборка для идеально чистых помещений"}}'),

  -- Specialized Packages
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Kitchen & Bathroom Focus', 'Specialized package focusing on kitchen and bathroom deep cleaning',
   999.00,
   '{"en": {"Name": "Kitchen & Bathroom Focus", "Description": "Specialized package focusing on kitchen and bathroom deep cleaning"}, "cs": {"Name": "Zaměření na kuchyň a koupelnu", "Description": "Specializovaný balíček zaměřený na hloubkový úklid kuchyně a koupelny"}, "ru": {"Name": "Фокус на кухню и ванную", "Description": "Специализированный пакет с акцентом на глубокую уборку кухни и ванной"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Eco-Green Package', 'Complete eco-friendly cleaning using only green products',
   1499.00,
   '{"en": {"Name": "Eco-Green Package", "Description": "Complete eco-friendly cleaning using only green products"}, "cs": {"Name": "Eko-zelený balíček", "Description": "Kompletní ekologický úklid používající pouze zelené produkty"}, "ru": {"Name": "Эко-зеленый пакет", "Description": "Полная экологическая уборка с использованием только зеленых продуктов"}}'),

  -- Premium Packages
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Moving Day Special', 'Perfect for move-in or move-out situations',
   2299.00,
   '{"en": {"Name": "Moving Day Special", "Description": "Perfect for move-in or move-out situations"}, "cs": {"Name": "Speciál pro den stěhování", "Description": "Ideální pro situace nastěhování nebo vystěhování"}, "ru": {"Name": "Специальный пакет для переезда", "Description": "Идеально для ситуаций въезда или выезда"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Post-Renovation Clean', 'Specialized cleaning after construction or renovation work',
   2799.00,
   '{"en": {"Name": "Post-Renovation Clean", "Description": "Specialized cleaning after construction or renovation work"}, "cs": {"Name": "Úklid po rekonstrukci", "Description": "Specializovaný úklid po stavebních nebo rekonstrukčních pracích"}, "ru": {"Name": "Уборка после ремонта", "Description": "Специализированная уборка после строительных или ремонтных работ"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Luxury Full Service', 'Premium package with all services included',
   3499.00,
   '{"en": {"Name": "Luxury Full Service", "Description": "Premium package with all services included"}, "cs": {"Name": "Luxusní kompletní služba", "Description": "Prémiový balíček se všemi zahrnutými službami"}, "ru": {"Name": "Роскошный полный сервис", "Description": "Премиум пакет со всеми включенными услугами"}}');