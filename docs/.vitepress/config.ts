import { defineConfig } from 'vitepress';
import { withMermaid } from 'vitepress-plugin-mermaid';

// withMermaid wraps defineConfig so ```mermaid fences render as diagrams. The fence is emitted as a
// <div class="mermaid"> and drawn in the BROWSER, not at build time — so a malformed diagram builds
// clean and fails only when a reader opens the page. Check a diagram by looking at it.
export default withMermaid(
  defineConfig({
    title: 'Cleansia Docs',
    description:
      'The source of truth for how the Cleansia platform works — flows, domain, decisions.',
    srcExclude: ['**/templates/**', '**/node_modules/**'],

    // A link to a page that does not exist fails the build. This is what makes the docs→docs half of
    // the reference contract mechanical; the code→docs half is agents/tools/check-docs-refs.mjs.
    ignoreDeadLinks: false,

    mermaid: {
      // Follow the site theme rather than pinning a palette, so diagrams stay legible in dark mode.
      theme: 'default',
    },

    themeConfig: {
      logo: '/logo.svg',
      nav: [
        // First, and on purpose: someone handed the platform for the first time needs orientation
        // before any of the reference sections mean anything.
        { text: 'Start here', link: '/getting-started' },
        { text: 'Product', link: '/product/' },
        { text: 'Domain', link: '/domain/' },
        { text: 'Flows', link: '/flows/' },
        { text: 'Decisions', link: '/decisions/' },
        { text: 'Architecture', link: '/architecture/overview' },
        {
          text: 'Apps',
          items: [
            { text: 'Customer', link: '/customer-app/overview' },
            { text: 'Partner', link: '/partner-app/overview' },
            { text: 'Admin', link: '/admin-app/overview' },
            { text: 'Mobile', link: '/mobile-app/overview' },
          ],
        },
        { text: 'API', link: '/api/authentication' },
        { text: 'Operations', link: '/deployment/ci-cd' },
        // The changelog is a repo-root artifact, not a page in this site, so this is an external link
        // rather than a route. Keeping one copy is the point: a published duplicate would rot.
        {
          text: 'Changelog',
          link: 'https://github.com/VM-s-Solutions/cleansia/blob/master/CHANGELOG.md',
        },
      ],
      sidebar: {
        '/product/': [
          {
            text: 'Product',
            items: [
              { text: 'Overview', link: '/product/' },
              { text: 'Features', link: '/product/features' },
              { text: 'Business rules', link: '/product/business-rules' },
            ],
          },
        ],
        '/domain/': [
          {
            text: 'Domain',
            items: [
              { text: 'Overview', link: '/domain/' },
              { text: 'Domain model', link: '/domain/model' },
              { text: 'Order lifecycle', link: '/domain/order-lifecycle' },
              { text: 'Offerability', link: '/domain/offerability' },
              { text: 'Component contracts', link: '/domain/roles/' },
            ],
          },
        ],
        '/flows/': [
          {
            text: 'Flows',
            items: [
              { text: 'Overview', link: '/flows/' },
              { text: 'Auth and identity', link: '/flows/auth-and-identity' },
              { text: 'Booking and pricing', link: '/flows/booking-and-pricing' },
              { text: 'Payment and fiscal', link: '/flows/payment-and-fiscal' },
              { text: 'Offerability and the take', link: '/flows/offerability-and-take' },
              { text: 'Execution and completion', link: '/flows/execution-and-completion' },
              { text: 'Cancellation, refund and dispute', link: '/flows/cancellation-refund-dispute' },
              { text: 'Pay, periods, invoices and payouts', link: '/flows/pay-and-payouts' },
              { text: 'Loyalty, memberships and referrals', link: '/flows/loyalty-and-memberships' },
              { text: 'GDPR, retention and audit', link: '/flows/gdpr-and-audit' },
              { text: 'Cross-cutting concerns', link: '/flows/cross-cutting' },
            ],
          },
        ],
        '/decisions/': [
          {
            text: 'Decisions',
            // Generated from docs/decisions/adr-*.md frontmatter. The link is the STABLE id, never the
            // title — ~618 source files cite `ADR-NNNN`, and the id is what has to keep resolving.
            items: [
              { text: 'All decisions', link: '/decisions/' },
              { text: "ADR-0001 — Authorization model", link: '/decisions/adr-0001' },
              { text: "ADR-0002 — Outbox dispatch contract", link: '/decisions/adr-0002' },
              { text: "ADR-0003 — Partitioned rate limiting", link: '/decisions/adr-0003' },
              { text: "ADR-0004 — Fiscal receipt idempotency boundary", link: '/decisions/adr-0004' },
              { text: "ADR-0005 — Integration resilience contract", link: '/decisions/adr-0005' },
              { text: "ADR-0006 — Refund dispute money path", link: '/decisions/adr-0006' },
              { text: "ADR-0007 — Soft delete policy", link: '/decisions/adr-0007' },
              { text: "ADR-0008 — Outbox table and drainer", link: '/decisions/adr-0008' },
              { text: "ADR-0009 — Refund policy", link: '/decisions/adr-0009' },
              { text: "ADR-0010 — Durable consumer idempotency", link: '/decisions/adr-0010' },
              { text: "ADR-0011 — Mobile apiresult contract", link: '/decisions/adr-0011' },
              { text: "ADR-0012 — Admin action audit log", link: '/decisions/adr-0012' },
              { text: "ADR-0013 — Ios app architecture and port strategy", link: '/decisions/adr-0013' },
              { text: "ADR-0014 — Ios deployment target ios16 and state mechanism", link: '/decisions/adr-0014' },
              { text: "ADR-0015 — Azure dev deployment bicep and github environments", link: '/decisions/adr-0015' },
              { text: "ADR-0016 — Apple app review compliance and ios quality bar", link: '/decisions/adr-0016' },
              { text: "ADR-0017 — Multi region expansion seam and its composition…", link: '/decisions/adr-0017' },
              { text: "ADR-0018 — Ios design parity principle", link: '/decisions/adr-0018' },
              { text: "ADR-0019 — Ios generated client authenticates via the core…", link: '/decisions/adr-0019' },
              { text: "ADR-0020 — Ios partner router is a flat enum root switch gated…", link: '/decisions/adr-0020' },
              { text: "ADR-0021 — Ios non modal 3 snap map sheet on the ios16 floor", link: '/decisions/adr-0021' },
              { text: "ADR-0022 — Ios shell single navigation stack pager and pill bar", link: '/decisions/adr-0022' },
              { text: "ADR-0023 — Per consumer claim ordering email claims after…", link: '/decisions/adr-0023' },
              { text: "ADR-0024 — Mobile access token ttl is the device revocation…", link: '/decisions/adr-0024' },
              { text: "ADR-0025 — Ios push display per platform apns alert with loc…", link: '/decisions/adr-0025' },
              { text: "ADR-0026 — Immediate device revocation via device id claim and…", link: '/decisions/adr-0026' },
              { text: "ADR-0027 — Immediate user session cutoff on password reset via…", link: '/decisions/adr-0027' },
              { text: "ADR-0028 — Multi tenant activation pack", link: '/decisions/adr-0028' },
              { text: "ADR-0029 — Ios live activity for in progress clean", link: '/decisions/adr-0029' },
              { text: "ADR-0030 — Web admin access token ttl 15 min", link: '/decisions/adr-0030' },
              { text: "ADR-0031 — Nswag regen drift is guarded at regen time", link: '/decisions/adr-0031' },
              { text: "ADR-0032 — Catalog law declarations require a named ci gate", link: '/decisions/adr-0032' },
              { text: "ADR-0033 — Catalog edit authority the routing test and cross…", link: '/decisions/adr-0033' },
              { text: "ADR-0034 — Partner payout details shape", link: '/decisions/adr-0034' },
              { text: "ADR-0035 — Metered membership benefit usage", link: '/decisions/adr-0035' },
              { text: "ADR-0036 — Preferred cleaner first refusal hold", link: '/decisions/adr-0036' },
              { text: "ADR-0037 — Order offerability is a payment qualified status…", link: '/decisions/adr-0037' },
              { text: "ADR-0038 — Promo redemption reservation runs after the uow…", link: '/decisions/adr-0038' },
              { text: "ADR-0039 — Preferred cleaner slot availability is checked at…", link: '/decisions/adr-0039' },
              { text: "ADR-0040 — Order currentstatus is non nullable the pre…", link: '/decisions/adr-0040' },
              { text: "ADR-0041 — Self billing agreement is a versioned append only…", link: '/decisions/adr-0041' },
              { text: "ADR-0042 — Shared wire enums are generated from the nswag…", link: '/decisions/adr-0042' },
              { text: "ADR-0043 — User artifact metadata is scrubbed at intake by…", link: '/decisions/adr-0043' },
              { text: "ADR-0044 — Stored content type is byte derived on every intake", link: '/decisions/adr-0044' },
              { text: "ADR-0045 — Favourite cleaner is a reservation the cleaner must…", link: '/decisions/adr-0045' },
              { text: "ADR-0046 — Payout invoice variable symbol is a claimed number…", link: '/decisions/adr-0046' },
              { text: "ADR-0047 — A server redacted field is rendered off its own…", link: '/decisions/adr-0047' },
              { text: "ADR-0048 — A generated dto is refused at the repository…", link: '/decisions/adr-0048' },
              { text: "ADR-0049 — A disclosure block is withheld by the server when…", link: '/decisions/adr-0049' },
              { text: "ADR-0050 — A dormant tenant column arbitrates nothing the…", link: '/decisions/adr-0050' },
              { text: "ADR-0051 — A reads tenancy posture is decided by the write…", link: '/decisions/adr-0051' },
              { text: "ADR-0052 — A cleaners own deletion files a request; only an…", link: '/decisions/adr-0052' },
              { text: "ADR-0053 — The live-commitment cap is one admins decision…", link: '/decisions/adr-0053' },
              { text: "ADR-0054 — Cleaner job reminders dedupe on a stamp per…", link: '/decisions/adr-0054' },
              { text: "ADR-0055 — A cleaner may set off or start only inside a…", link: '/decisions/adr-0055' },
            ],
          },
        ],
        '/architecture/': [
          {
            text: 'Architecture',
            items: [
              { text: 'Overview', link: '/architecture/overview' },
              { text: 'Backend (.NET)', link: '/architecture/backend' },
              { text: 'Frontend (Angular)', link: '/architecture/frontend' },
              { text: 'Database', link: '/architecture/database' },
              { text: 'Infrastructure', link: '/architecture/infrastructure' },
              {
                text: 'Local orchestration',
                link: '/architecture/local-orchestration',
              },
              {
                text: 'Request logging & PII redaction',
                link: '/architecture/request-logging',
              },
              {
                text: 'Push Notifications',
                link: '/architecture/push-notifications',
              },
              {
                text: 'Fiscal Compliance',
                link: '/architecture/fiscal-compliance',
              },
              { text: 'Security rules (S1–S12)', link: '/architecture/security-rules' },
              {
                text: 'Platform expandability',
                link: '/architecture/platform-expandability',
              },
            ],
          },
        ],
        '/customer-app/': [
          {
            text: 'Customer App',
            items: [
              { text: 'Overview', link: '/customer-app/overview' },
              { text: 'Authentication', link: '/customer-app/authentication' },
              { text: 'Ordering Flow', link: '/customer-app/ordering-flow' },
              { text: 'Checkout & Payments', link: '/customer-app/checkout' },
              { text: 'Order Tracking', link: '/customer-app/order-tracking' },
            ],
          },
        ],
        '/partner-app/': [
          {
            text: 'Partner App',
            items: [
              { text: 'Overview', link: '/partner-app/overview' },
              { text: 'Onboarding', link: '/partner-app/onboarding' },
              { text: 'Order Management', link: '/partner-app/order-management' },
              { text: 'Invoicing', link: '/partner-app/invoicing' },
              { text: 'Dashboard', link: '/partner-app/dashboard' },
            ],
          },
        ],
        '/admin-app/': [
          {
            text: 'Admin App',
            items: [
              { text: 'Overview', link: '/admin-app/overview' },
              { text: 'User Management', link: '/admin-app/user-management' },
              { text: 'Order Management', link: '/admin-app/order-management' },
              { text: 'Pay Periods', link: '/admin-app/pay-periods' },
              { text: 'Global Rates', link: '/admin-app/pay-config' },
              { text: 'Reporting', link: '/admin-app/reporting' },
              { text: 'Fiscal Failures', link: '/admin-app/fiscal-failures' },
            ],
          },
        ],
        '/mobile-app/': [
          {
            text: 'Mobile App',
            items: [
              { text: 'Overview', link: '/mobile-app/overview' },
              { text: 'Features', link: '/mobile-app/features' },
              { text: 'Shared patterns', link: '/mobile-app/patterns' },
              { text: 'API Integration', link: '/mobile-app/api-integration' },
            ],
          },
        ],
        '/api/': [
          {
            text: 'API Reference',
            items: [
              { text: 'Authentication', link: '/api/authentication' },
              { text: 'Orders', link: '/api/orders' },
              { text: 'Payments', link: '/api/payments' },
              { text: 'Webhooks', link: '/api/webhooks' },
            ],
          },
        ],
        '/deployment/': [
          {
            text: 'Operations',
            items: [
              { text: 'CI/CD', link: '/deployment/ci-cd' },
              { text: 'Azure Setup', link: '/deployment/azure-setup' },
              {
                text: 'Environment Config',
                link: '/deployment/environment-config',
              },
            ],
          },
        ],
      },
      socialLinks: [
        { icon: 'github', link: 'https://github.com/VM-s-Solutions/cleansia' },
      ],
      search: {
        provider: 'local',
      },
      footer: {
        message: 'Cleansia s.r.o. Internal Documentation',
        copyright: '© 2026 Cleansia s.r.o.',
      },
    },
  })
);
