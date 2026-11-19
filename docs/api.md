# MEMS API

Tenant-scoped ASP.NET Core WebAPI (`MEMS.Api`). Run the solution with `dotnet run --project MEMS.AppHost`, then use:

- **Swagger UI:** http://localhost:5001/api/swagger (interactive; click **Authorize** and paste a token)
- **OpenAPI document:** http://localhost:5001/api/openapi/v1.json
- **Requests file:** [`MEMS.Api/MEMS.Api.http`](../MEMS.Api/MEMS.Api.http)
- **Automated check:** `python3 tools/api_smoke_test.py` (49 checks, including cross-tenant attacks)

## Rules

- Every tenant-owned route starts with `/api/{tenant}/`, where `{tenant}` is the municipality slug
  (`burnaby`, `surrey`, `richmond`, `pitt-meadows`; case-insensitive). Responses only ever contain that tenant's data.
- **GET** endpoints are public. **POST / PUT / DELETE** need `Authorization: Bearer {token}` from `POST /api/auth/login`.
- A **Municipality Admin** token works only for its own tenant; any other tenant returns **403** before anything runs.
  A **Super Admin** token works for every tenant and is required to create or delete municipalities.
- Records are looked up inside the tenant from the URL: a Surrey candidate id requested through `/api/burnaby/...`
  returns **404**, the same as a missing record.
- Errors are [RFC 7807](https://www.rfc-editor.org/rfc/rfc7807) ProblemDetails (`application/problem+json`).
  No stack traces or SQL errors are ever returned.
- Image URLs (`logoUrl`, `photoUrl`) are relative to the gateway, e.g. `/api/burnaby/images/7`.

## Endpoints

### Auth

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/login` | — | Sign in an admin; returns a JWT (60 min). Locks for 5 min after 5 wrong passwords. |

### Municipalities (tenants)

| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/api/municipalities` | — | All municipalities (public municipality picker) |
| GET | `/api/municipalities/{id}` | — | One municipality by id |
| GET | `/api/municipalities/{slug}` | — | One municipality by slug |
| POST | `/api/municipalities` | Super Admin | Create a tenant |
| PUT | `/api/municipalities/{id}` | Super Admin, or that tenant's admin | Update (only Super Admin may change the slug) |
| DELETE | `/api/municipalities/{id}` | Super Admin | Delete the tenant with its positions, candidates, images and admin accounts |
| PUT | `/api/municipalities/{id}/logo` | Super Admin, or that tenant's admin | Upload/replace logo (multipart field `file`) |
| DELETE | `/api/municipalities/{id}/logo` | Super Admin, or that tenant's admin | Remove logo |

### Positions

| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/api/{tenant}/positions` | — | The tenant's positions |
| GET | `/api/{tenant}/positions/{id}` | — | One position |
| POST | `/api/{tenant}/positions` | Admin of `{tenant}` | Create |
| PUT | `/api/{tenant}/positions/{id}` | Admin of `{tenant}` | Update name/seats |
| DELETE | `/api/{tenant}/positions/{id}` | Admin of `{tenant}` | Delete with its candidates |

### Candidates

| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/api/{tenant}/candidates?search=&positionId=` | — | The tenant's candidates; `search` matches part of the name (case-insensitive) |
| GET | `/api/{tenant}/candidates/{id}` | — | One candidate |
| POST | `/api/{tenant}/candidates` | Admin of `{tenant}` | Create (position must be in `{tenant}`) |
| PUT | `/api/{tenant}/candidates/{id}` | Admin of `{tenant}` | Update (photo kept) |
| DELETE | `/api/{tenant}/candidates/{id}` | Admin of `{tenant}` | Delete with photo |
| PUT | `/api/{tenant}/candidates/{id}/photo` | Admin of `{tenant}` | Upload/replace photo (multipart field `file`) |
| DELETE | `/api/{tenant}/candidates/{id}/photo` | Admin of `{tenant}` | Remove photo |

### Images

| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/api/{tenant}/images/{id}` | — | Logo or photo bytes; 404 if it belongs to another tenant |

Image uploads: `.jpg`, `.jpeg`, `.png`, `.webp`, max 2 MB, and the file's bytes must really be a JPEG/PNG/WebP.

## Request and response examples

### Sign in

```http
POST /api/auth/login
Content-Type: application/json

{ "userName": "burnabyadmin", "password": "P@$$w0rd" }
```

`200 OK`
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5...",
  "expiresAt": "2026-11-19T09:27:43.556945Z",
  "userName": "burnabyadmin",
  "role": "Municipality Admin",
  "tenant": "burnaby"
}
```

### List municipalities

```http
GET /api/municipalities
```

`200 OK`
```json
[
  {
    "id": 1,
    "name": "Burnaby",
    "slug": "burnaby",
    "province": "British Columbia",
    "electionDate": "2026-10-17",
    "description": "One of the largest cities in Metro Vancouver, home to Metrotown, Burnaby Mountain and Simon Fraser University.",
    "themeColor": "#1d4e89",
    "logoUrl": null
  }
]
```

### List a tenant's candidates

```http
GET /api/burnaby/candidates?search=price
```

`200 OK`
```json
[
  {
    "id": 4,
    "name": "Caroline Price",
    "profile": "Caroline Price is running for Councillor in Burnaby. ...",
    "website": null,
    "email": "caroline.price@example.com",
    "dateCreated": "2026-09-01T00:00:00Z",
    "positionId": 2,
    "positionName": "Councillor",
    "municipalityId": 1,
    "photoUrl": null
  }
]
```

### Create a position

```http
POST /api/burnaby/positions
Authorization: Bearer {token}
Content-Type: application/json

{ "name": "Park Commissioner", "seatsAvailable": 3 }
```

`201 Created` (`Location: /api/burnaby/positions/15`)
```json
{ "id": 15, "name": "Park Commissioner", "seatsAvailable": 3, "municipalityId": 1, "candidateCount": 0 }
```

### Create a candidate

```http
POST /api/burnaby/candidates
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "Alex Rivera",
  "profile": "Long-time resident running on transit and parks.",
  "website": "https://example.com/alex",
  "email": "alex@example.com",
  "positionId": 15
}
```

`201 Created` (`Location: /api/burnaby/candidates/44`)
```json
{
  "id": 44,
  "name": "Alex Rivera",
  "profile": "Long-time resident running on transit and parks.",
  "website": "https://example.com/alex",
  "email": "alex@example.com",
  "dateCreated": "2026-11-19T08:27:43.628684Z",
  "positionId": 15,
  "positionName": "Park Commissioner",
  "municipalityId": 1,
  "photoUrl": null
}
```

### Upload a photo

```http
PUT /api/burnaby/candidates/44/photo
Authorization: Bearer {token}
Content-Type: multipart/form-data; boundary=...

(file field "file": photo.png)
```

`200 OK` — the candidate, with `"photoUrl": "/api/burnaby/images/7"`.

### Create a municipality (Super Admin)

```http
POST /api/municipalities
Authorization: Bearer {super token}
Content-Type: application/json

{
  "name": "Coquitlam",
  "slug": "coquitlam",
  "province": "British Columbia",
  "electionDate": "2026-10-17",
  "description": "Tri-Cities municipality.",
  "themeColor": "#336699"
}
```

`201 Created` — the new municipality. `/coquitlam` and `/api/coquitlam/...` work immediately; no new app is needed.

## Error responses

| Status | When | Example body |
|---|---|---|
| 400 | Validation failed (data annotations or service rules) | see below |
| 401 | No/invalid/expired token on a write, or bad login | `{ "title": "Unauthorized", "status": 401 }` |
| 403 | Token for another tenant, or not allowed (e.g. Municipality Admin creating a tenant) | see below |
| 404 | Unknown tenant slug, or record not in this tenant | see below |
| 409 | Database constraint conflict (e.g. two requests creating the same slug at once) | `{ "title": "The change conflicts with existing data.", "status": 409 }` |
| 500 | Unexpected error (logged with a trace id; no details returned) | `{ "title": "An error occurred while processing your request.", "status": 500 }` |

**400 — validation**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "SeatsAvailable": [ "The field SeatsAvailable must be between 1 and 50." ] },
  "traceId": "00-38dcd95cab2108c5a4adb6feaa138a42-4fc849e8b61d22bb-01"
}
```

Other 400 messages from the service layer: `"This municipality already has a position with this name."`,
`"Choose a position in your municipality."` (position from another tenant), `"This URL slug is already used by another
municipality."`, `"Only a Super Admin can change the URL slug."`, `"The file is not a valid JPEG, PNG or WebP image."`,
`"The image must be 2 MB or smaller."`.

**401 — bad login**
```json
{ "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2", "title": "Invalid user name or password.", "status": 401 }
```

**403 — Burnaby admin calling `/api/surrey/...`**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Access to this municipality is not allowed.",
  "status": 403,
  "detail": "Municipality Administrators can only access their own municipality.",
  "traceId": "00-a6d098e787371d034a1ffed239e489b8-e8ad25ec5c5d67d2-01"
}
```

**404 — unknown tenant**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Municipality not found.",
  "status": 404,
  "detail": "No municipality uses the URL segment 'nowhere'.",
  "traceId": "00-0b0174d2c10ae3e8a47ff47425b8b8ba-3a1d058b3b3b537f-01"
}
```

The `traceId` matches the trace in the Aspire dashboard, so an error report can be followed to the exact request.
