# Cleansia — Gaps & Roadmap

> Generated: 2026-03-15
> Status: Living document — update as items are completed

---

## Current State Summary

- All 3 web apps complete (Customer, Partner, Admin)
- Android Partner app complete
- Backend APIs complete (.NET 10, Aspire, CQRS with MediatR)
- Stripe payments integrated (checkout sessions, webhooks)
- SendGrid email service integrated (with Polly retry)
- Multi-tenancy, GDPR compliance, rate limiting, health checks — all implemented

---

## Critical Gaps (Must Fix Before Deployment)

### 1. Email Confirmation Disabled

- [ ] `Register.cs` and `RegisterEmployee.cs` have the email confirmation call commented out
- Path: `src/Cleansia.Core.AppServices/Features/Auth/Register.cs`
- Impact: Users can register without confirming email
- Fix: Uncomment the `SendConfirmationEmailAsync` call
- Priority: **HIGH**

### 2. No Production Deployment Pipeline

- [ ] No CI/CD workflow exists
- [ ] No environment-specific configurations
- [ ] No Azure resources provisioned
- See DEPLOYMENT_PLAN.md for full plan
- Priority: **HIGH**

### 3. No Environment-Specific Configs

- [ ] Need appsettings.Production.json
- [ ] Need Angular environment files
- [ ] Need Key Vault integration
- Priority: **HIGH**

### 4. Database Migration Strategy

- [ ] No migration scripts for Azure PostgreSQL
- [ ] Need seed data strategy for new environments
- [ ] Need backup/restore procedures
- Priority: **HIGH**

---

## High Priority Gaps

### 5. Incomplete Order Status Notifications

- [ ] Email notifications only on payment completion and order completion
- [ ] Missing: emails for InProgress, Cancelled, Assigned status changes
- Impact: Customers don't know when their cleaning is in progress or cancelled
- Priority: **HIGH**

### 6. No E2E Tests

- [ ] No Cypress/Playwright tests for any frontend
- [ ] No integration tests for critical flows (order -> payment -> webhook -> status)
- Priority: **HIGH**

### 7. Real Translations Needed

- [ ] SK, UK, RU translation files need proper translations (currently being done)
- [ ] Android strings.xml needs translations
- Priority: **HIGH** (in progress)

---

## Medium Priority Gaps

### 8. No Monitoring Dashboard

- [ ] Application Insights not configured
- [ ] No Sentry integration (DSN needed)
- [ ] No custom health check dashboard
- Priority: **MEDIUM**

### 9. No SEO Optimization

- [ ] Customer app needs meta tags, OG tags, structured data
- [ ] SSR helps but needs proper meta tag management
- Priority: **MEDIUM**

### 10. No Analytics

- [ ] No Google Analytics or similar
- [ ] No conversion tracking
- Priority: **MEDIUM**

### 11. No Customer Reviews/Ratings

- [ ] Backend model exists but no UI
- [ ] Would improve trust and conversion
- Priority: **MEDIUM**

### 12. No Real-Time Tracking

- [ ] SignalR not yet implemented
- [ ] Could show live order status updates
- Priority: **MEDIUM**

---

## Low Priority Gaps

### 13. No Push Notifications

- [ ] Android app has notification preference but no FCM integration
- Priority: **LOW**

### 14. No Offline Support

- [ ] Android app requires internet
- [ ] Could cache recent data
- Priority: **LOW**

### 15. Code Quality

- [ ] Move duplicate EnumSchemaFilter to shared project
- [ ] Some components missing OnPush change detection
- [ ] Some components use TranslateModule instead of TranslatePipe
- Priority: **LOW** (see plan file for details)

---

## Roadmap

### Phase 1: Deployment Ready (Priority: NOW)

- [ ] Fix email confirmation (uncomment in Register.cs)
- [ ] Create environment configs (appsettings, Key Vault)
- [ ] Set up Azure resources (DEV environment)
- [ ] Create CI/CD pipeline (GitHub Actions)
- [ ] Run database migrations on Azure
- [ ] Deploy to DEV and verify
- [ ] Set up custom domain + SSL
- [ ] Deploy to PRO

### Phase 2: Production Hardening (Priority: Next 2-4 weeks)

- [ ] Add Application Insights / Sentry
- [ ] Complete order status notifications
- [ ] Add E2E tests for critical paths
- [ ] SEO + meta tags for customer app
- [ ] Google Analytics integration

### Phase 3: Mobile Apps (Priority: Next 1-3 months)

- [ ] iOS Partner app (Swift)
- [ ] Android Customer app (Kotlin)
- [ ] iOS Customer app (Swift)
- See MOBILE_APP_PLAN.md for details

### Phase 4: Growth Features (Priority: 3-6 months)

- [ ] Customer reviews/ratings
- [ ] Real-time order tracking (SignalR)
- [ ] Push notifications (FCM + APNs)
- [ ] Analytics dashboard for admin
- [ ] Multi-language SEO (hreflang tags)
- [ ] Marketing landing pages per service
