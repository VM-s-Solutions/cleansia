-- INSERT PROPERTY SIZE PRESETS  (T-0675 / ADR-0056)
--
-- The selectable property sizes per market. The LABEL is the only country-specific
-- part: "Orders" store Rooms and Bathrooms as plain integers and
-- OrderPricingCalculator computes BasePrice + PerRoomPrice * (rooms + bathrooms),
-- so nothing anywhere persists "3+kk". A new market is a new set of rows over the
-- same two numbers.
--
-- Because an order stores the integers rather than a preset id, deactivating or
-- relabelling a preset can never make a historic order unpriceable.
--
-- Countries carry generated ULID ids, so rows are joined on "IsoCode" rather than
-- on a hard-coded key. A country that is not seeded yields no rows rather than a
-- broken foreign key.
--
-- Idempotent: re-running inserts nothing that is already present.

INSERT INTO public."PropertySizePresets" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy", "DeactivatedOn",
  "CountryId", "Code", "SortOrder", "Rooms", "Bathrooms", "Translations"
)
SELECT
  generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP,
  NULL, NULL, NULL, NULL,
  c."Id", v."Code", v."SortOrder", v."Rooms", v."Bathrooms", v."Translations"::jsonb
FROM (VALUES
  -- ── Czechia ──────────────────────────────────────────────────────────────
  -- "N+kk" is the Czech convention: N living rooms plus a kitchen corner.
  ('CZE', 'CZ_1KK',   1, 1, 1, '{"en": {"Name": "1 room", "Description": ""}, "cs": {"Name": "1+kk", "Description": ""}, "sk": {"Name": "1+kk", "Description": ""}, "uk": {"Name": "1 кімната", "Description": ""}, "ru": {"Name": "1 комната", "Description": ""}}'),
  ('CZE', 'CZ_2KK',   2, 2, 1, '{"en": {"Name": "2 rooms", "Description": ""}, "cs": {"Name": "2+kk", "Description": ""}, "sk": {"Name": "2+kk", "Description": ""}, "uk": {"Name": "2 кімнати", "Description": ""}, "ru": {"Name": "2 комнаты", "Description": ""}}'),
  ('CZE', 'CZ_3KK',   3, 3, 1, '{"en": {"Name": "3 rooms", "Description": ""}, "cs": {"Name": "3+kk", "Description": ""}, "sk": {"Name": "3+kk", "Description": ""}, "uk": {"Name": "3 кімнати", "Description": ""}, "ru": {"Name": "3 комнаты", "Description": ""}}'),
  ('CZE', 'CZ_4KK',   4, 4, 2, '{"en": {"Name": "4 rooms", "Description": ""}, "cs": {"Name": "4+kk", "Description": ""}, "sk": {"Name": "4+kk", "Description": ""}, "uk": {"Name": "4 кімнати", "Description": ""}, "ru": {"Name": "4 комнаты", "Description": ""}}'),
  ('CZE', 'CZ_HOUSE', 5, 5, 2, '{"en": {"Name": "House", "Description": ""}, "cs": {"Name": "Dům", "Description": ""}, "sk": {"Name": "Dom", "Description": ""}, "uk": {"Name": "Будинок", "Description": ""}, "ru": {"Name": "Дом", "Description": ""}}'),

  -- ── Slovakia ─────────────────────────────────────────────────────────────
  -- Same housing convention and the same integers; a separate row set because
  -- the catalogue is keyed on country, not on a shared "cs/sk" grouping that
  -- would have to be unpicked the day the two markets diverge.
  ('SVK', 'SK_1KK',   1, 1, 1, '{"en": {"Name": "1 room", "Description": ""}, "cs": {"Name": "1+kk", "Description": ""}, "sk": {"Name": "1+kk", "Description": ""}, "uk": {"Name": "1 кімната", "Description": ""}, "ru": {"Name": "1 комната", "Description": ""}}'),
  ('SVK', 'SK_2KK',   2, 2, 1, '{"en": {"Name": "2 rooms", "Description": ""}, "cs": {"Name": "2+kk", "Description": ""}, "sk": {"Name": "2+kk", "Description": ""}, "uk": {"Name": "2 кімнати", "Description": ""}, "ru": {"Name": "2 комнаты", "Description": ""}}'),
  ('SVK', 'SK_3KK',   3, 3, 1, '{"en": {"Name": "3 rooms", "Description": ""}, "cs": {"Name": "3+kk", "Description": ""}, "sk": {"Name": "3+kk", "Description": ""}, "uk": {"Name": "3 кімнати", "Description": ""}, "ru": {"Name": "3 комнаты", "Description": ""}}'),
  ('SVK', 'SK_4KK',   4, 4, 2, '{"en": {"Name": "4 rooms", "Description": ""}, "cs": {"Name": "4+kk", "Description": ""}, "sk": {"Name": "4+kk", "Description": ""}, "uk": {"Name": "4 кімнати", "Description": ""}, "ru": {"Name": "4 комнаты", "Description": ""}}'),
  ('SVK', 'SK_HOUSE', 5, 5, 2, '{"en": {"Name": "House", "Description": ""}, "cs": {"Name": "Dům", "Description": ""}, "sk": {"Name": "Dom", "Description": ""}, "uk": {"Name": "Будинок", "Description": ""}, "ru": {"Name": "Дом", "Description": ""}}')
) AS v("IsoCode", "Code", "SortOrder", "Rooms", "Bathrooms", "Translations")
JOIN public."Countries" c ON c."IsoCode" = v."IsoCode"
WHERE NOT EXISTS (
  SELECT 1 FROM public."PropertySizePresets" p
  WHERE p."CountryId" = c."Id" AND p."Code" = v."Code"
);
