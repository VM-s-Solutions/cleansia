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
            items: [{ text: 'Overview', link: '/product/' }],
          },
        ],
        '/domain/': [
          {
            text: 'Domain',
            items: [{ text: 'Overview', link: '/domain/' }],
          },
        ],
        '/flows/': [
          {
            text: 'Flows',
            items: [{ text: 'Overview', link: '/flows/' }],
          },
        ],
        '/decisions/': [
          {
            text: 'Decisions',
            items: [{ text: 'Overview', link: '/decisions/' }],
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
                text: 'Push Notifications',
                link: '/architecture/push-notifications',
              },
              {
                text: 'Fiscal Compliance',
                link: '/architecture/fiscal-compliance',
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
