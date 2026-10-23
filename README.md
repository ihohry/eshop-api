# EShop API

An e-commerce REST API built with ASP.NET Core 10, EF Core and PostgreSQL, with production-minded practices from day one: structured logging, validated configuration, consistent error handling and CI.

> **Status:** under active development. See [Status](#status) below.

## Tech stack

- .NET 10, ASP.NET Core minimal APIs
- EF Core 10 with PostgreSQL 17 (Docker)
- Serilog for structured logging
- OpenAPI with the Scalar UI (Development only)
- GitHub Actions for CI, Dependabot for dependency updates
- ASP.NET Core Identity with JWT access tokens and rotating refresh tokens
- MailKit for email, Mailpit as the development mail server
- Product images stored on local disk behind an interface (cloud storage can replace it later)

## Architecture

A modular monolith with three projects. Dependencies point inward (Api, Infrastructure, Domain):

- `src/EShop.Api`: endpoints, middleware, composition root
- `src/EShop.Domain`: entities and domain rules, no dependencies
- `src/EShop.Infrastructure`: EF Core, DbContext, options, Identity, email

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

# 4. Restore packages and the pinned dotnet-ef tool, then apply the migrations
dotnet restore
dotnet tool restore
dotnet ef database update \
  --project src/EShop.Infrastructure \
  --startup-project src/EShop.Api

# 5. Set a JWT signing key in user-secrets (at least 32 characters;
#    the app does not start without it)
dotnet user-secrets set "Identity:Jwt:SigningKey" "$(openssl rand -base64 48)" --project src/EShop.Api

# 6. Run the API
dotnet run --project src/EShop.Api
```

Check that it works:

```bash
curl -i http://localhost:5097/health
```

Development mail inbox (Mailpit): <http://localhost:8025>

The email settings for Development (Mailpit, sender address, link templates) are in `appsettings.Development.json`. In production there is no such file: all required values come from environment variables, where a double underscore replaces the colon (for example `Identity__Jwt__SigningKey`, `Email__Host`, `Email__Password`).

### API documentation

In Development, the interactive API documentation is available at <http://localhost:5097/scalar>.

## Authentication

| Endpoint | Description |
| --- | --- |
| `POST /auth/register` | Create an account and send a confirmation email |
| `POST /auth/confirm-email` | Confirm the email address |
| `POST /auth/resend-confirmation` | Send the confirmation email again |
| `POST /auth/login` | Get an access token and a refresh token |
| `POST /auth/refresh` | Rotate the refresh token |
| `POST /auth/logout` | Revoke the refresh token family |
| `POST /auth/forgot-password` | Send a password reset email |
| `POST /auth/reset-password` | Set a new password |

## Catalog

Public endpoints:

| Endpoint | Description |
| --- | --- |
| `GET /categories` | Category tree (two levels) |
| `GET /products` | Active products, with paging, category filter, search and sorting |
| `GET /products/{slug}` | Product details with images |
| `GET /images/{fileName}` | Product image file |

Admin endpoints (role `Admin`):

| Endpoint | Description |
| --- | --- |
| `POST/PUT/DELETE /admin/categories` | Manage categories |
| `POST/PUT/DELETE /admin/products` | Manage products (delete deactivates the product) |
| `PUT /admin/products/{id}/stock` | Set the stock quantity |
| `POST/DELETE /admin/products/{id}/images` | Upload and delete product images |

In Development, an empty catalog is filled with sample data on startup.

## Cart

All cart endpoints need a logged-in user.

| Endpoint | Description |
| --- | --- |
| `GET /cart` | Show the cart with prices from the catalog |
| `POST /cart/items` | Add a product |
| `PUT /cart/items/{productId}` | Set the quantity |
| `DELETE /cart/items/{productId}` | Remove a product |
| `DELETE /cart` | Clear the cart |

## Orders

Customer endpoints (logged-in user):

| Endpoint | Description |
| --- | --- |
| `POST /orders` | Checkout: turn the cart into an order and reserve the stock |
| `GET /orders` | List my orders (paged, newest first) |
| `GET /orders/{id}` | Show one of my orders |
| `POST /orders/{id}/cancel` | Cancel an order that waits for payment |

Unpaid orders expire after a payment window (`Orders:PaymentWindowMinutes`, default 24 hours). A background worker checks every `Orders:ExpiryCheckIntervalSeconds` (default 60) and returns the stock of expired orders.

Admin endpoints (role `Admin`):

| Endpoint | Description |
| --- | --- |
| `GET /admin/orders` | List all orders (paged, optional `status` filter) |
| `GET /admin/orders/{id}` | Show any order |
| `POST /admin/orders/{id}/mark-paid` | Confirm the payment |
| `POST /admin/orders/{id}/ship` | Mark as shipped |
| `POST /admin/orders/{id}/deliver` | Mark as delivered |
| `POST /admin/orders/{id}/cancel` | Cancel a pending or paid order and return the stock |

## Status

Identity, catalog and cart are complete. Orders are in progress: checkout, the customer and admin order endpoints, and the automatic expiry of unpaid orders work; the payment method and the pay-on-receipt flow are next.

- [x] Foundation: solution structure, error handling, PostgreSQL, logging, CI
- [x] Identity (registration, email confirmation, login, JWT, refresh tokens, password reset)
- [x] Catalog (categories, products, images, stock)
- [x] Cart
- [ ] Orders and inventory
- [ ] Payments
- [ ] Tests and quality
- [ ] Delivery