# Architecture Overview

Cleansia is a multi-tenant cleaning services platform deployed on Azure. The system consists of 4 backend APIs, 3 frontend apps, an Android mobile app, and Azure Functions for background processing.

## System Diagram

```
                          ┌─────────────────────┐
                          │   Azure DNS          │
                          │   cleansia.cz        │
                          └──────────┬───────────┘
                                     │
              ┌──────────────────────┼──────────────────────┐
              │                      │                      │
     ┌────────▼────────┐   ┌────────▼────────┐   ┌────────▼────────┐
     │ Customer SSR    │   │ Partner SPA     │   │ Admin SPA       │
     │ App Service     │   │ Static Web App  │   │ Static Web App  │
     │ (Node.js 20)    │   │ (Angular 19)    │   │ (Angular 19)    │
     └────────┬────────┘   └────────┬────────┘   └────────┬────────┘
              │                      │                      │
     ┌────────▼────────┐   ┌────────▼────────┐   ┌────────▼────────┐
     │ Customer API    │   │ Partner API     │   │ Admin API       │
     │ .NET 10         │   │ .NET 10         │   │ .NET 10         │
     └────────┬────────┘   └────────┬────────┘   └────────┬────────┘
              │                      │                      │
              └──────────────────────┼──────────────────────┘
                                     │
                          ┌──────────▼───────────┐
                          │   PostgreSQL          │
                          │   Flexible Server     │
                          └──────────┬───────────┘
                                     │
              ┌──────────────────────┼──────────────────────┐
              │                      │                      │
     ┌────────▼────────┐   ┌────────▼────────┐   ┌────────▼────────┐
     │ Azure Functions │   │ Azure Blob      │   │ Azure Key Vault │
     │ (Docker)        │   │ Storage         │   │                 │
     └─────────────────┘   └─────────────────┘   └─────────────────┘
```

## Technology Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| Backend APIs | .NET (C#) | 10 |
| ORM | Entity Framework Core | 10 |
| Database | PostgreSQL | 16 |
| Frontend | Angular (Nx monorepo) | 19 |
| Mobile | Kotlin / Jetpack Compose | Latest |
| Background Jobs | Azure Functions (Docker) | v4 |
| PDF Generation | QuestPDF | Native .NET |
| Email | SendGrid | Dynamic Templates |
| Payments | Stripe | Checkout Sessions |
| Auth | JWT + Google OAuth | Custom |
| Orchestration | .NET Aspire | 13.1.1 |
| Cloud | Microsoft Azure | West Europe |

## Backend APIs

The backend consists of 4 separate API projects, each serving a different audience:

| API | Project | Port | Audience |
|-----|---------|------|----------|
| Partner API | `Cleansia.Web` | 5000 | Employees / Partners |
| Admin API | `Cleansia.Web.Admin` | 5001 | Back-office administrators |
| Mobile API | `Cleansia.Web.Mobile` | 5002 | Android/iOS apps |
| Customer API | `Cleansia.Web.Customer` | 5003 | Public customers |

All APIs share:
- `Cleansia.Core.Domain` — Domain entities, enums, repositories
- `Cleansia.Core.AppServices` — CQRS handlers (MediatR), services, validators
- `Cleansia.Infra.Database` — EF Core DbContext, migrations
- `Cleansia.Config` — Shared startup configuration, middleware, auth

## Frontend Apps

| App | Type | Deployment |
|-----|------|-----------|
| Customer (`cleansia.app`) | SSR (Angular Universal) | Azure App Service (Node.js) |
| Partner (`cleansia-partner.app`) | SPA | Azure Static Web App |
| Admin (`cleansia-admin.app`) | SPA | Azure Static Web App |

All 3 apps live in an Nx monorepo with shared libraries:
- `@cleansia/components` — Shared UI components
- `@cleansia/services` — Shared services (auth, theme, etc.)
- `@cleansia/partner-services` — NSwag-generated API client
- `@cleansia/customer-services` — NSwag-generated customer API client

## Background Processing

Azure Functions run as a Docker container with QuestPDF for native PDF generation:

| Function | Trigger | Purpose |
|----------|---------|---------|
| `GenerateReceipt` | Queue: `generate-receipt` | Receipt PDF + email |
| `GenerateInvoice` | Queue: `generate-invoice` | Employee invoice PDF |
| `CloseExpiredPayPeriods` | Timer: daily 2 AM | Pay period management |
| `SendPeriodEndReminders` | Timer: daily 9 AM | Reminder emails |
| `DataRetentionCleanup` | Timer: weekly Sunday 3 AM | GDPR cleanup |

## Key Design Decisions

1. **CQRS with MediatR** — All business logic goes through command/query handlers with pipeline behaviors (validation, unit of work)
2. **Multi-tenancy** — Shared database with `TenantId` global query filter on all entities
3. **QuestPDF over Chromium** — Native .NET PDF generation without browser dependency
4. **Separate APIs per audience** — Different auth policies, CORS, and rate limiting per API
5. **Queue-based PDF generation** — APIs enqueue messages, Functions process asynchronously
