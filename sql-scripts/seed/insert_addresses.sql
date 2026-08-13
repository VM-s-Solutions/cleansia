-- INSERT ADDRESSES
INSERT INTO public."Addresses" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "Street", "City", "ZipCode", "CountryId"
)
VALUES
  -- Customer Addresses in Prague
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Wenceslas Square 1', 'Prague', '11000',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE')),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Národní třída 25', 'Prague', '11000',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE')),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Vinohrady 456', 'Prague', '12000',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE')),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Karlínské náměstí 12', 'Prague', '18600',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE')),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Smíchov 789', 'Prague', '15000',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE')),

  -- Customer Addresses in Other Czech Cities
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Masarykova 567', 'Brno', '60200',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE')),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Stodolní 123', 'Ostrava', '70200',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE')),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Americká 45', 'Plzen', '30100',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE')),

  -- Employee Addresses
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Dejvická 321', 'Prague', '16000',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE')),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Anděl 654', 'Prague', '15000',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE')),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Náměstí Míru 987', 'Prague', '12000',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE')),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Florenc 147', 'Prague', '18600',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE')),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Waltrovka 258', 'Prague', '15000',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE'));