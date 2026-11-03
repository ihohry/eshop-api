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
- Stripe Checkout (hosted payment page) for card payments, with webhooks and refunds

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
- Docker with Docker Compose v2 (Linux: `docker.io` + compose plugin; Windows and macOS: Docker Desktop)

### Run locally

The steps below use a Bash shell (Linux and macOS). On Windows, use the PowerShell variant further down.

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

# 5b. Store your Stripe test keys in user-secrets (the Stripe settings are
#     validated on startup; use a sandbox key that starts with sk_test_)
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..." --project src/EShop.Api
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..." --project src/EShop.Api

# 6. Run the API
dotnet run --project src/EShop.Api
```

To receive Stripe webhooks locally, install the [Stripe CLI](https://docs.stripe.com/stripe-cli) and run it in a second terminal, with your `sk_test_...` key (the same sandbox as the app):

```bash
stripe listen --api-key "sk_test_..." --events checkout.session.completed --forward-to localhost:5097/webhooks/stripe
```

The command prints a `whsec_...` signing secret. Store it as `Stripe:WebhookSecret` (as in step 5b) and restart the API.

**macOS:** install the .NET SDK (for example `brew install --cask dotnet-sdk`) and Docker Desktop, then use the Bash steps above.

**Windows (PowerShell):** install the .NET SDK and Docker Desktop. Differences from the Bash steps:

```powershell
# 1. Copy the settings file
Copy-Item .env.example .env

# 3. Use backticks (not backslashes) for line breaks, or put the command on one line
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5433;Database=eshop;Username=eshop;Password=<your password>" --project src/EShop.Api

# 5. openssl is usually not available; generate the key with .NET instead
$key = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
dotnet user-secrets set "Identity:Jwt:SigningKey" $key --project src/EShop.Api
```

Steps 2, 4, 5b and 6 are the same (the Stripe commands are single-line and work in PowerShell too). These Windows and macOS steps are written from the tool documentation; the project is developed and tested on Linux.

Check that it works (on Windows PowerShell use `curl.exe`, because `curl` is an alias there):

```bash
curl -i http://localhost:5097/health
```

Development mail inbox (Mailpit): <http://localhost:8025>

The email settings for Development (Mailpit, sender address, link templates) are in `appsettings.Development.json`. In production there is no such file: all required values come from environment variables, where a double underscore replaces the colon (for example `Identity__Jwt__SigningKey`, `Email__Host`, `Email__Password`, `Stripe__SecretKey`, `Stripe__WebhookSecret`).

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
| `POST /orders/{id}/cancel` | Cancel an order that is not yet paid or confirmed |
| `POST /orders/{id}/pay` | Get a payment link for a prepaid order |

At checkout the customer chooses a delivery method (`Courier` or `PickupPoint`) and a payment method:

- `Prepaid` (default): the order waits for payment (`PendingPayment`).
- `PayOnReceipt`: pickup points only. The order waits for the admin to confirm it (`PendingConfirmation`). This is disabled by default (`Orders:PayOnReceiptEnabled`) and limited by `Orders:PayOnReceiptMaxTotal` (default 300).

Unpaid and unconfirmed orders expire after a payment window (`Orders:PaymentWindowMinutes`, default 24 hours). A background worker checks every `Orders:ExpiryCheckIntervalSeconds` (default 60) and returns the stock of expired orders.

Admin endpoints (role `Admin`):

| Endpoint | Description |
| --- | --- |
| `GET /admin/orders` | List all orders (paged, optional `status` filter) |
| `GET /admin/orders/{id}` | Show any order |
| `POST /admin/orders/{id}/mark-paid` | Confirm the payment (prepaid orders) |
| `POST /admin/orders/{id}/confirm` | Confirm an order after contacting the customer (pay on receipt) |
| `POST /admin/orders/{id}/ship` | Mark as shipped |
| `POST /admin/orders/{id}/deliver` | Mark as delivered |
| `POST /admin/orders/{id}/refuse` | The customer refused the parcel (pay on receipt); returns the stock |
| `POST /admin/orders/{id}/cancel` | Cancel an order that was not shipped and return the stock |
| `POST /admin/orders/{id}/refund` | Refund a paid or cancelled prepaid order through Stripe |

## Returns and warranty

A customer can return products from a delivered order within the return window (`Orders:ReturnWindowDays`, default 14). Orders show `returnableUntil` once they are delivered. Several partial returns per order are possible, as long as the total does not exceed the quantity bought.

Customer endpoints (logged-in user):

| Endpoint | Description |
| --- | --- |
| `POST /orders/{orderId}/returns` | Request a return (product lines, quantities, reason) |
| `GET /orders/{orderId}/returns` | List the returns of my order |

Admin endpoints (role `Admin`):

| Endpoint | Description |
| --- | --- |
| `GET /admin/returns` | List returns (paged, optional `status` filter) |
| `GET /admin/returns/{id}` | Show one return |
| `POST /admin/returns/{id}/approve` | Approve a request |
| `POST /admin/returns/{id}/reject` | Reject a request, with an optional reason |
| `POST /admin/returns/{id}/receive` | Mark the goods as received and return them to stock |
| `POST /admin/returns/{id}/refund` | Refund a received return (through Stripe for prepaid orders; a status change for pay on receipt) |

For prepaid orders the refund goes through Stripe (see [Payments](#payments)); for pay-on-receipt orders it is only a status, and the money is returned manually.

Every product has `warrantyMonths` (default 24, `0` means no warranty). It is shown in the product responses and can be set by an admin when creating or updating a product. Warranty claims are a manual support process; the API has no warranty logic.

## Payments

Prepaid orders are paid on a Stripe-hosted Checkout page (test mode in development). The provider is behind `IPaymentGateway`, so another one can be added later.

1. The customer calls `POST /orders/{id}/pay` and gets a payment link. A valid open link is returned again. The link lives until the earlier of the order expiry and 23 hours, and `/pay` is refused when less than 31 minutes remain. The link can be forwarded to someone else.
2. Stripe calls `POST /webhooks/stripe`. This endpoint is anonymous; the Stripe signature check is the authentication. Only `checkout.session.completed` with a paid status is handled, and the order and payment become `Paid` in one transaction. Repeated events are harmless.
3. If an order is cancelled or expires, its open payment link is closed. If a payment still arrives for an order that is no longer waiting for payment, it is refunded automatically.

Settings (section `Stripe`): `SecretKey`, `WebhookSecret`, `SuccessUrlTemplate`, `CancelUrlTemplate`. Keys come from user-secrets or environment variables (`Stripe__SecretKey`, `Stripe__WebhookSecret`), never from files.

### Refunds

- `POST /admin/orders/{id}/refund`: refunds a `Paid` or `Cancelled` prepaid order (a paid order is cancelled first, and the stock is returned).
- `POST /admin/returns/{id}/refund`: for prepaid orders, this refunds the stored return amount through Stripe. Partial refunds are supported; the payment becomes `Refunded` when the full amount is returned.
- Before refunding, the API checks Stripe for an existing refund with the same key, so a retry never refunds twice. A Stripe failure gives `502` and can be repeated.
- Returns of pay-on-receipt orders stay a manual status change, because the money can be cash, card or something else.

Known limitations: a failed automatic refund of a late payment is not retried (refund it in the Stripe Dashboard), and refunds made by hand in the Dashboard are not detected.

## Status

Identity, catalog, cart, orders (including pay on receipt), returns with warranty and payments are complete. Integration tests and the production delivery work are next.

- [x] Foundation: solution structure, error handling, PostgreSQL, logging, CI
- [x] Identity (registration, email confirmation, login, JWT, refresh tokens, password reset)
- [x] Catalog (categories, products, images, stock)
- [x] Cart
- [x] Orders and inventory (checkout, expiry, pay on receipt)
- [x] Returns and warranty
- [x] Payments (Stripe Checkout, webhook, refunds)
- [ ] Tests and quality
- [ ] Delivery