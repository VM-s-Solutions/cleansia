import { defineConfig } from 'vitepress';

export default defineConfig({
  title: 'Cleansia Docs',
  description: 'Technical documentation for the Cleansia platform',
  srcExclude: [
    '**/templates/**',
    '**/node_modules/**',
  ],
  themeConfig: {
    logo: '/logo.svg',
    nav: [
      { text: 'Architecture', link: '/architecture/overview' },
      { text: 'Customer App', link: '/customer-app/overview' },
      { text: 'Partner App', link: '/partner-app/overview' },
      { text: 'Admin App', link: '/admin-app/overview' },
      { text: 'API', link: '/api/authentication' },
    ],
    sidebar: {
      '/architecture/': [
        {
          text: 'Architecture',
          items: [
            { text: 'Overview', link: '/architecture/overview' },
            { text: 'Backend (.NET)', link: '/architecture/backend' },
            { text: 'Frontend (Angular)', link: '/architecture/frontend' },
            { text: 'Database', link: '/architecture/database' },
            { text: 'Infrastructure', link: '/architecture/infrastructure' },
            { text: 'Fiscal Compliance', link: '/architecture/fiscal-compliance' },
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
          text: 'Deployment',
          items: [
            { text: 'CI/CD', link: '/deployment/ci-cd' },
            { text: 'Azure Setup', link: '/deployment/azure-setup' },
            { text: 'Environment Config', link: '/deployment/environment-config' },
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
});
