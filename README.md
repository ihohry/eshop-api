# EShop API

An e-commerce REST API built with ASP.NET Core 10, EF Core and PostgreSQL, with production-minded practices from day one: structured logging, validated configuration, consistent error handling and CI.

> **Status:** under active development. See [Status](#status) below.

## Tech stack

- .NET 10, ASP.NET Core minimal APIs
- EF Core 10 with PostgreSQL 17 (Docker)
- Serilog for structured logging
- OpenAPI with the Scalar UI (Development only)
- GitHub Actions for CI, Dependabot for dependency updates

## Architecture

A modular monolith with three projects. Dependencies point inward (Api, Infrastructure, Domain):

- `src/EShop.Api`: endpoints, middleware, composition root
- `src/EShop.Domain`: entities and domain rules, no dependencies
- `src/EShop.Infrastructure`: EF Core, DbContext, options

Other design choices:

- Errors are returned as RFC 9457 ProblemDetails, handled by one global exception handler.
- Settings are typed options validated on startup. Secrets come from environment variables or user-secrets, never from committed files.
- Strict analyzers are enabled, and warnings are treated as errors.

## Getting started

### Prerequisites

- .NET SDK 10.0.401 or newer (see `global.json`)
- Docker with Docker Compose v2

### Run locally

```bash
# 1. Create your local settings and set a password in .env
cp .env.example .env

# 2. Start PostgreSQL (host port 5433)
docker compose up -d

# 3. Store the connection string in user-secrets
#    (use the same password as POSTGRES_PASSWORD in .env)
dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=localhost;Port=5433;Database=eshop;Username=eshop;Password=<your password>" \
  --project src/EShop.Api

# 4. Restore the pinned dotnet-ef tool and apply the database migrations
dotnet tool restore
dotnet ef database update \
  --project src/EShop.Infrastructure \
  --startup-project src/EShop.Api

# 5. Run the API
dotnet run --project src/EShop.Api
```

Check that it works:

```bash
curl -i http://localhost:5097/health
```

### API documentation

In Development, the interactive API documentation is available at <http://localhost:5097/scalar>.

## Status

The foundation is complete. Next: identity.

- [x] Foundation: solution structure, error handling, PostgreSQL, logging, CI
- [ ] Identity (registration, login, JWT)
- [ ] Catalog
- [ ] Cart
- [ ] Orders and inventory
- [ ] Payments
- [ ] Tests and quality
- [ ] Delivery