-- INSERT SERVICES
INSERT INTO public."Services" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "Name", "Description",
  "BasePrice", "PerRoomPrice", "EstimatedTime", "Translations"
)
VALUES
  -- Basic Cleaning Services
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'General Cleaning', 'Standard cleaning of all rooms including dusting, vacuuming, and sanitizing',
   500.00, 150.00, 120,
   '{"en": {"Name": "General Cleaning", "Description": "Standard cleaning of all rooms including dusting, vacuuming, and sanitizing"}, "cs": {"Name": "Obecný úklid", "Description": "Standardní úklid všech místností včetně otírání prachu, vysávání a dezinfekce"}, "ru": {"Name": "Общая уборка", "Description": "Стандартная уборка всех комнат включая протирание пыли, пылесос и дезинфекцию"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Deep Cleaning', 'Thorough cleaning including baseboards, inside appliances, and detailed sanitization',
   800.00, 250.00, 180,
   '{"en": {"Name": "Deep Cleaning", "Description": "Thorough cleaning including baseboards, inside appliances, and detailed sanitization"}, "cs": {"Name": "Hloubkový úklid", "Description": "Důkladný úklid včetně lišt, vnitřků spotřebičů a detailní dezinfekce"}, "ru": {"Name": "Глубокая уборка", "Description": "Тщательная уборка включая плинтуса, внутри бытовой техники и детальная дезинфекция"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Bathroom Cleaning', 'Specialized bathroom cleaning with tile scrubbing and grout cleaning',
   300.00, 0.00, 45,
   '{"en": {"Name": "Bathroom Cleaning", "Description": "Specialized bathroom cleaning with tile scrubbing and grout cleaning"}, "cs": {"Name": "Úklid koupelny", "Description": "Specializovaný úklid koupelny s drhnáním dlaždic a čištěním spár"}, "ru": {"Name": "Уборка ванной", "Description": "Специализированная уборка ванной с чисткой плитки и швов"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Kitchen Deep Clean', 'Comprehensive kitchen cleaning including oven, refrigerator, and cabinets',
   400.00, 0.00, 90,
   '{"en": {"Name": "Kitchen Deep Clean", "Description": "Comprehensive kitchen cleaning including oven, refrigerator, and cabinets"}, "cs": {"Name": "Hloubkový úklid kuchyně", "Description": "Komplexní úklid kuchyně včetně trouby, lednice a skříněk"}, "ru": {"Name": "Глубокая уборка кухни", "Description": "Комплексная уборка кухни включая духовку, холодильник и шкафы"}}'),

  -- Specialized Services
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Window Cleaning', 'Interior and exterior window cleaning with streak-free finish',
   200.00, 50.00, 60,
   '{"en": {"Name": "Window Cleaning", "Description": "Interior and exterior window cleaning with streak-free finish"}, "cs": {"Name": "Mytí oken", "Description": "Mytí oken zevnitř i zvenčí bez šmouh"}, "ru": {"Name": "Мытье окон", "Description": "Мытье окон изнутри и снаружи без разводов"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Carpet Cleaning', 'Professional carpet steam cleaning and stain removal',
   350.00, 100.00, 90,
   '{"en": {"Name": "Carpet Cleaning", "Description": "Professional carpet steam cleaning and stain removal"}, "cs": {"Name": "Čištění koberců", "Description": "Profesionální parní čištění koberců a odstraňování skvrn"}, "ru": {"Name": "Чистка ковров", "Description": "Профессиональная паровая чистка ковров и удаление пятен"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Upholstery Cleaning', 'Deep cleaning of sofas, chairs, and fabric furniture',
   450.00, 0.00, 75,
   '{"en": {"Name": "Upholstery Cleaning", "Description": "Deep cleaning of sofas, chairs, and fabric furniture"}, "cs": {"Name": "Čištění čalounění", "Description": "Hloubkové čištění sedaček, židlí a látkového nábytku"}, "ru": {"Name": "Чистка обивки", "Description": "Глубокая чистка диванов, кресел и тканевой мебели"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Post-Construction Cleanup', 'Specialized cleaning after renovation or construction work',
   1200.00, 300.00, 240,
   '{"en": {"Name": "Post-Construction Cleanup", "Description": "Specialized cleaning after renovation or construction work"}, "cs": {"Name": "Úklid po rekonstrukci", "Description": "Specializovaný úklid po rekonstrukci nebo stavebních pracích"}, "ru": {"Name": "Уборка после ремонта", "Description": "Специализированная уборка после ремонта или строительных работ"}}'),

  -- Premium Services
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Move-in/Move-out Cleaning', 'Complete cleaning for moving in or out of property',
   1000.00, 200.00, 180,
   '{"en": {"Name": "Move-in/Move-out Cleaning", "Description": "Complete cleaning for moving in or out of property"}, "cs": {"Name": "Úklid při stěhování", "Description": "Kompletní úklid při nastěhování nebo vystěhování z nemovitosti"}, "ru": {"Name": "Уборка при переезде", "Description": "Полная уборка при въезде или выезде из недвижимости"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Eco-Friendly Cleaning', 'Green cleaning using only eco-friendly and non-toxic products',
   600.00, 180.00, 135,
   '{"en": {"Name": "Eco-Friendly Cleaning", "Description": "Green cleaning using only eco-friendly and non-toxic products"}, "cs": {"Name": "Ekologický úklid", "Description": "Zelený úklid používající pouze ekologické a netoxické produkty"}, "ru": {"Name": "Экологическая уборка", "Description": "Зеленая уборка с использованием только экологически чистых и нетоксичных продуктов"}}');