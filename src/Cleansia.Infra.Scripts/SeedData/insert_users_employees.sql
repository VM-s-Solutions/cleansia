-- INSERT USERS (Customers and Employees)
INSERT INTO public."Users" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "Password", "FirstName", "LastName",
  "Email", "PhoneNumber", "BirthDate", "Profile",
  "AuthenticationType", "EmailConfirmed"
)
VALUES
  -- Customer Users
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '$2a$11$LGWjlgYDdH1Zso.FvdZbkebhVtKj39L1HYN0GlbE3rRYcZw5I9RQ6', -- Password: Test123!
   'Jan', 'Novák', 'jan.novak@email.cz', '+420123456789', '1985-03-15', 0, 0, true),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '$2a$11$LGWjlgYDdH1Zso.FvdZbkebhVtKj39L1HYN0GlbE3rRYcZw5I9RQ6', -- Password: Test123!
   'Marie', 'Svobodová', 'marie.svobodova@email.cz', '+420234567890', '1990-07-22', 0, 0, true),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '$2a$11$LGWjlgYDdH1Zso.FvdZbkebhVtKj39L1HYN0GlbE3rRYcZw5I9RQ6', -- Password: Test123!
   'Petr', 'Dvořák', 'petr.dvorak@email.cz', '+420345678901', '1988-11-05', 0, 0, true),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '$2a$11$LGWjlgYDdH1Zso.FvdZbkebhVtKj39L1HYN0GlbE3rRYcZw5I9RQ6', -- Password: Test123!
   'Anna', 'Černá', 'anna.cerna@email.cz', '+420456789012', '1992-04-18', 0, 0, true),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '$2a$11$LGWjlgYDdH1Zso.FvdZbkebhVtKj39L1HYN0GlbE3rRYcZw5I9RQ6', -- Password: Test123!
   'Tomáš', 'Procházka', 'tomas.prochazka@email.cz', '+420567890123', '1987-09-12', 0, 0, true),

  -- Employee Users
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '$2a$11$LGWjlgYDdH1Zso.FvdZbkebhVtKj39L1HYN0GlbE3rRYcZw5I9RQ6', -- Password: Test123!
   'Kateřina', 'Novotná', 'katerina.novotna@cleansia.cz', '+420678901234', '1993-06-08', 1, 0, true),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '$2a$11$LGWjlgYDdH1Zso.FvdZbkebhVtKj39L1HYN0GlbE3rRYcZw5I9RQ6', -- Password: Test123!
   'Michal', 'Krejčí', 'michal.krejci@cleansia.cz', '+420789012345', '1991-12-03', 1, 0, true),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '$2a$11$LGWjlgYDdH1Zso.FvdZbkebhVtKj39L1HYN0GlbE3rRYcZw5I9RQ6', -- Password: Test123!
   'Zuzana', 'Horáková', 'zuzana.horakova@cleansia.cz', '+420890123456', '1989-02-14', 1, 0, true),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '$2a$11$LGWjlgYDdH1Zso.FvdZbkebhVtKj39L1HYN0GlbE3rRYcZw5I9RQ6', -- Password: Test123!
   'Pavel', 'Veselý', 'pavel.vesely@cleansia.cz', '+420901234567', '1986-08-27', 1, 0, true),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '$2a$11$LGWjlgYDdH1Zso.FvdZbkebhVtKj39L1HYN0GlbE3rRYcZw5I9RQ6', -- Password: Test123!
   'Lenka', 'Marková', 'lenka.markova@cleansia.cz', '+420012345678', '1994-05-19', 1, 0, true);

-- INSERT EMPLOYEES
INSERT INTO public."Employees" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "ICO", "IBAN", "AverageRating",
  "ComplaintsCount", "ContractStatus", "PassportId",
  "NationalityId", "EmergencyContactName", "EmergencyContactPhone",
  "Availability", "DocumentFileNames", "UserId"
)
VALUES
  -- Employee 1: Kateřina Novotná
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '87654321', 'CZ6508000000192000145399', 4.8, 0, 1, 'P123456789',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE'),
   'Jana Novotná', '+420777888999',
   '{"Monday":[{"Start":"09:00:00","End":"17:00:00"}],"Tuesday":[{"Start":"09:00:00","End":"17:00:00"}],"Wednesday":[{"Start":"09:00:00","End":"17:00:00"}],"Thursday":[{"Start":"09:00:00","End":"17:00:00"}],"Friday":[{"Start":"09:00:00","End":"17:00:00"}]}',
   '[]',
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'katerina.novotna@cleansia.cz')),

  -- Employee 2: Michal Krejčí
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '87654322', 'CZ6508000000192000145400', 4.6, 1, 1, 'P987654321',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE'),
   'Eva Krejčí', '+420777888998',
   '{"Monday":[{"Start":"08:00:00","End":"16:00:00"}],"Tuesday":[{"Start":"08:00:00","End":"16:00:00"}],"Wednesday":[{"Start":"08:00:00","End":"16:00:00"}],"Thursday":[{"Start":"08:00:00","End":"16:00:00"}],"Friday":[{"Start":"08:00:00","End":"16:00:00"}],"Saturday":[{"Start":"10:00:00","End":"14:00:00"}]}',
   '[]',
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'michal.krejci@cleansia.cz')),

  -- Employee 3: Zuzana Horáková
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '87654323', 'CZ6508000000192000145401', 4.9, 0, 1, 'P456789123',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE'),
   'Martin Horák', '+420777888997',
   '{"Monday":[{"Start":"10:00:00","End":"18:00:00"}],"Tuesday":[{"Start":"10:00:00","End":"18:00:00"}],"Wednesday":[{"Start":"10:00:00","End":"18:00:00"}],"Thursday":[{"Start":"10:00:00","End":"18:00:00"}],"Friday":[{"Start":"10:00:00","End":"18:00:00"}]}',
   '[]',
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'zuzana.horakova@cleansia.cz')),

  -- Employee 4: Pavel Veselý
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '87654324', 'CZ6508000000192000145402', 4.5, 2, 1, 'P789123456',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE'),
   'Petra Veselá', '+420777888996',
   '{"Monday":[{"Start":"07:00:00","End":"15:00:00"}],"Tuesday":[{"Start":"07:00:00","End":"15:00:00"}],"Wednesday":[{"Start":"07:00:00","End":"15:00:00"}],"Thursday":[{"Start":"07:00:00","End":"15:00:00"}],"Friday":[{"Start":"07:00:00","End":"15:00:00"}],"Saturday":[{"Start":"09:00:00","End":"13:00:00"}]}',
   '[]',
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'pavel.vesely@cleansia.cz')),

  -- Employee 5: Lenka Marková
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '87654325', 'CZ6508000000192000145403', 4.7, 0, 1, 'P321654987',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE'),
   'Tomáš Marek', '+420777888995',
   '{"Monday":[{"Start":"09:30:00","End":"17:30:00"}],"Tuesday":[{"Start":"09:30:00","End":"17:30:00"}],"Wednesday":[{"Start":"09:30:00","End":"17:30:00"}],"Thursday":[{"Start":"09:30:00","End":"17:30:00"}],"Friday":[{"Start":"09:30:00","End":"17:30:00"}],"Sunday":[{"Start":"12:00:00","End":"16:00:00"}]}',
   '[]',
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'lenka.markova@cleansia.cz'));