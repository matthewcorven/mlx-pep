# Commerce Requirements

Required functional slices:

- storefront browsing, search, and product detail pages
- cart and checkout orchestration
- order submission and order-status lookup
- admin or backoffice flows for catalogue updates and pricing-rule refresh
- cache invalidation flow when catalogue or pricing data changes
- Aspire TypeScript AppHost composition for web frontend, web API, Redis, and background data-refresh components

Non-functional constraints:

- TypeScript end to end
- pragmatic service boundaries rather than microservice sprawl
- clear validation plan for cache correctness, degraded cache behavior, and local orchestration startup
