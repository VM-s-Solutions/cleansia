-- INSERT CURRENCIES
INSERT INTO public."Currencies" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "Code", "Symbol", "Name", "ExchangeRate"
)
VALUES
  -- European Currencies
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'CZK', 'Kč', 'Czech Koruna', 1.0),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'EUR', '€', 'Euro', 0.041),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'USD', '$', 'US Dollar', 0.044),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'GBP', '£', 'British Pound', 0.035),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'PLN', 'zł', 'Polish Zloty', 0.18),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'CHF', 'CHF', 'Swiss Franc', 0.039),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'SEK', 'kr', 'Swedish Krona', 0.47),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'NOK', 'kr', 'Norwegian Krone', 0.47),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'DKK', 'kr', 'Danish Krone', 0.31),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'HUF', 'Ft', 'Hungarian Forint', 16.2),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'RON', 'lei', 'Romanian Leu', 0.20),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'BGN', 'лв', 'Bulgarian Lev', 0.080);