# Frontend Developer Handoff Guide

Welcome! The backend API is fully containerized, tested (100% pass across 250 tests), and ready for frontend integration.

---

## 🚀 1. Quick Start (Docker Compose)

To start the full stack (API + SQL Server + Redis + Elasticsearch + Kibana):

```bash
docker compose up -d
```

To stop the stack:
```bash
docker compose down
```

To view live API logs:
```bash
docker compose logs -f vendor-api
```

---

## 🌐 2. Service Endpoints & Ports

| Service | Host URL | Description |
| :--- | :--- | :--- |
| **API Base URL** | `http://localhost:8080` (or `http://localhost:8081`) | ASP.NET Core 9 Minimal API |
| **Swagger UI** | `http://localhost:8080/swagger` | Interactive OpenAPI documentation |
| **Storefront Demo** | `http://localhost:8080/` | Built-in showcase UI |
| **Health Check (Live)** | `http://localhost:8080/health/live` | Liveness probe (200 OK) |
| **Health Check (Ready)**| `http://localhost:8080/health/ready` | Readiness probe (DB + Redis checks) |
| **SQL Server** | `localhost:14330` | `User: sa`, `Password: YourStr0ng!Pass` |
| **Redis Cache** | `localhost:6379` | In-memory & Redis hybrid cache |
| **Elasticsearch** | `http://localhost:9200` | Full-text product search engine |
| **Kibana** | `http://localhost:56010` | Observability & logging dashboard |

---

## 🔑 3. Default Seed Accounts & Authentication

The database automatically seeds default users and initial catalog data on startup:

### Admin Account
- **Email**: `admin@vendor.com`
- **Password**: `Admin123!`
- **Role**: `Admin`

### Customer Account
- **Email**: `customer@vendor.com`
- **Password**: `Customer123!`
- **Role**: `Customer`

### JWT Authentication Flow
1. Send `POST /api/v1/auth/login` with `{ "email": "customer@vendor.com", "password": "Customer123!" }`
2. Receive `{ "accessToken": "...", "refreshToken": "..." }`
3. Pass `Authorization: Bearer <accessToken>` header on protected endpoints.
4. Guest sessions can be created via `POST /api/v1/auth/guest` (returns a guest token and session ID).

---

## 🛍️ 4. Key Frontend REST Endpoints

### Catalog & Products
- `GET /api/v1/products` — List active products (supports pagination, category filtering, search)
- `GET /api/v1/products/{slug}` — Get product details by slug (e.g. `studio-pods-pro`, `smartwatch-v2`)
- `POST /api/v1/products` — Create product *(Admin)*
- `POST /api/v1/admin/products/{id}/variants` — Add product variant *(Admin)*
- `POST /api/v1/admin/products/{id}/activate` — Activate product *(Admin)*

### Cart & Checkout
- `GET /api/v1/cart` — Get current customer's cart
- `POST /api/v1/cart/items` — Add item to cart (`{ "variantId": "...", "quantity": 1 }`)
- `DELETE /api/v1/cart/items/{variantId}` — Remove item from cart
- `POST /api/v1/orders/checkout` — Two-phase checkout execution

### Customer & Auth
- `POST /api/v1/auth/register` — Register a new customer
- `POST /api/v1/auth/login` — Login with email/password
- `POST /api/v1/auth/guest` — Start anonymous guest session
- `GET /api/v1/customer/profile` — Get authenticated customer profile
- `PUT /api/v1/customer/profile` — Update customer profile

### Real-Time SignalR Hub
- `WS /hubs/admin` — Real-time telemetry, orders, inventory updates (pass `?access_token=<JWT>` for authentication).

---

## 🔒 5. CORS Configuration

CORS is pre-configured to allow local frontend development on:
- `http://localhost:3000` (Next.js / React)
- `http://localhost:3001`
- `http://localhost:5173` (Vite)
- `http://localhost:5174`
- `http://localhost:4200` (Angular)
- `http://localhost:8080` / `http://localhost:8081`
- `http://127.0.0.1:*`
- Headers & credentials allowed (`AllowCredentials`).
