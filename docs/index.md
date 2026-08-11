---
layout: home
hero:
  name: Cleansia
  text: Technical Documentation
  tagline: Architecture, APIs, and deployment guides for the Cleansia platform
  actions:
    - theme: brand
      text: Architecture Overview
      link: /architecture/overview
    - theme: alt
      text: API Reference
      link: /api/authentication

features:
  - icon: 🏗️
    title: Architecture
    details: System design, backend (.NET 10), frontend (Angular 19), database schema, and Azure infrastructure.
    link: /architecture/overview
  - icon: 🛒
    title: Customer App
    details: SSR Angular app for customers — ordering flow, checkout, payments, and order tracking.
    link: /customer-app/overview
  - icon: 👷
    title: Partner App
    details: SPA for cleaning employees — onboarding, order management, invoicing, and dashboard.
    link: /partner-app/overview
  - icon: ⚙️
    title: Admin App
    details: Back-office SPA for administrators — user management, orders, services, and reporting.
    link: /admin-app/overview
  - icon: 📱
    title: Mobile Apps
    details: Native Android (Kotlin/Jetpack Compose) and iOS (Swift/SwiftUI) — a partner and a customer app on each platform.
    link: /mobile-app/overview
  - icon: 🚀
    title: Deployment
    details: Azure resources, CI/CD pipelines, environment configuration, and infrastructure scripts.
    link: /deployment/azure-setup
---
