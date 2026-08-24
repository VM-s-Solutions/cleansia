# Getting started with Cleansia

This page is for someone who has just been handed the platform and needs to see it work. It answers
what the pieces are, which one to open first, and the handful of things that reliably confuse people
on day one.

It is deliberately not an architecture document. When you want to know *why* something is the way it
is, [the decisions](/decisions/) have it; when you want the exact numbers the platform charges by,
[business rules](/product/business-rules) has those.

## What Cleansia is

A marketplace for home cleaning. **Customers** book a clean, **cleaners** (called *partners*
everywhere in the product, *employees* everywhere in the code) take the job and do the work, and
**admins** watch the whole thing and step in when it goes wrong.

Every one of those three audiences has its own front end, and the partner and customer audiences have
a mobile app as well as a web app. That is why there are so many apps for one product.

## The apps, and who each one is for

| App | Audience | Where | What you do in it |
|---|---|---|---|
| Customer web | Customers | `src/Cleansia.App` → `cleansia.app` | Book a clean, pay, track it, subscribe to Plus |
| Customer mobile | Customers | Android `:customer-app`, iOS `CleansiaCustomer` | The same, on a phone — plus push notifications |
| Partner web | Cleaners | `src/Cleansia.App` → `cleansia-partner.app` | Register, upload documents, see pay |
| Partner mobile | Cleaners | Android `:partner-app`, iOS `CleansiaPartner` | Take jobs, run the job, get paid — the main partner surface |
| Admin web | Staff | `src/Cleansia.App` → `cleansia-admin.app` | Approve cleaners, oversee orders, disputes, payroll, the catalogue |

**The partner mobile app is the one that matters most.** Cleaners work from a phone; the partner web
app exists mainly for registration and paperwork.

Behind them are **five API hosts**, one per audience, each with its own port locally:

| Host | Port | Serves |
|---|---|---|
| Partner API | 5000 | Partner web |
| Admin API | 5001 | Admin web |
| Partner Mobile API | 5002 | Partner Android + iOS |
| Customer API | 5003 | Customer web |
| Customer Mobile API | 5004 | Customer Android + iOS |

They are separate hosts on purpose, not one API with role checks — see
[the security rules](/architecture/security-rules). All five share **one PostgreSQL database**.

## Getting a customer to the point of booking

This is the short path, and nothing gates it but an email confirmation.

1. **Register** in the customer app.
2. **Confirm the email.** The link arrives by SendGrid. On a local run, check the API logs rather
   than your inbox.
3. **Add an address.** Pick it on the map; the app fills in the street, city and postcode.
4. **Book.** Choose services, a date and a time, then pay by card or choose cash.

That is the whole customer onboarding. There is no approval step and no document upload.

## Getting a cleaner to the point of taking work

This is the long path, and it is where most first-day confusion lives. A newly registered cleaner
**cannot take any work at all** until an admin approves them, and the app will show a lock screen
saying so.

Four things must be true:

1. **Profile complete** — name, phone, birth date, address, identification, entity type.
2. **Documents uploaded** — the identity and eligibility paperwork.
3. **Availability set** — which days and hours they work.
4. **An admin has approved them** — in the admin app, under the cleaner's detail page.

Only the fourth is out of the cleaner's hands, and it is the one people forget. Until it happens the
contract status is `Pending`, and `Pending` cannot take, start or complete an order.

::: tip Testing both sides yourself
Register the cleaner first, complete all three of their steps, then switch to the admin app and
approve them. Only then go and create an order as a customer — otherwise there is nobody who can
take it and the order sits unassigned, which looks like a bug and is not one.
:::

## The things that confuse people on day one

**A cleaner who is not approved sees a lock screen, not an error.** That is the registration lock. It
lists what is still missing. If everything is ticked and it still shows, the missing piece is the
admin approval.

**Orders are refused outside serviced cities.** The platform only operates where a `ServiceCity` row
exists. Prague is seeded, along with its district spellings in both Czech and English (`Praha 4`,
`Prague 4`, and so on). An address in a city with no row is refused at booking. The mobile apps warn
about this at address-selection time so it is not discovered at payment — but that warning is
advisory, and the **address still saves**: people move, and coverage grows.

**`Confirmed` does not mean a cleaner is assigned.** It is deliberately overloaded — it means either
"money is settled" or "a cleaner took it". To find out whether anyone is actually doing the job, look
at the assigned crew. See [the order lifecycle](/domain/order-lifecycle).

**A job can need more than one cleaner.** Crew size is derived from the estimated duration, so a long
service needs two people and stays partly open until both seats are taken.

**Cash and card behave differently.** A card order settles before the work; a cash order is collected
by the cleaner and marked on the job. Refunds and cancellation fees differ between them.

**Plus is a subscription with a trial.** Two plans, monthly and annual, both with a 14-day trial and a
discount on every booking. Starting one needs a real Stripe payment method even during the trial,
because the trial converts.

**Loyalty tiers are automatic.** They rise with completed bookings and unlock a discount at the higher
tiers. Nothing needs enabling.

**Push notifications need a real device.** Simulators and emulators do not receive them. The in-app
notification feed still fills up, so use that to check something fired.

**Five languages, everywhere.** English, Czech, Slovak, Ukrainian and Russian. If a screen shows a raw
key like `order.something`, that is a missing translation and worth reporting.

## Running it locally

The full instructions are in the repository's `README.md`. The short version:

```bash
cd src
dotnet run --project Cleansia.AppHost     # Postgres, storage, migrator, all five APIs, Functions
```

then whichever front end you need:

```bash
cd src/Cleansia.App && npm ci
npm run start:cleansia            # customer  :4202
npm run start:cleansia-partner    # partner   :4200
npm run start:cleansia-admin      # admin     :4201
```

Every API waits for the migrator to exit cleanly, so if the APIs never come up, read the migrator's
output first — it is almost always the database.

## Where to go next

| You want | Page |
|---|---|
| Every number the platform charges, pays or refuses by | [Business rules](/product/business-rules) |
| What each feature actually does | [Features](/product/features) |
| How an order moves through its states | [Order lifecycle](/domain/order-lifecycle) |
| Who gets offered a job, and when | [Offerability](/domain/offerability) |
| The ten flows end to end | [Flows](/flows/) |
| Why a decision was made | [Decisions](/decisions/) |
