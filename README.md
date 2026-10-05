# ArmaFit API

REST API for ArmaFit, a fitness app where athletes follow workout plans and trainers manage the plans of the athletes they coach.

Built with ASP.NET Core (.NET 10), Entity Framework Core and PostgreSQL.

## Running it

You need the .NET 10 SDK and PostgreSQL listening on `localhost:5432` with user `postgres` / password `postgres` (change the connection string in `ArmaFit-API/appsettings.json` if yours differs).

```bash
dotnet run --project ArmaFit-API
```

- API: `http://localhost:5087`
- Swagger UI: `http://localhost:5087/swagger` (use **Authorize** and paste an access token)
- OpenAPI document: `http://localhost:5087/openapi/v1.json`

On first run the app creates the `armafit` database, its tables and sample data. It does **not** update an existing database: after changing the entities, either drop the database or apply the change by hand.

### Sample users

All seeded users have the password `Password123!`.

| Email | Role |
|---|---|
| `tomas@armafit.lt`, `rasa@armafit.lt` | trainer |
| `jonas@armafit.lt`, `egle@armafit.lt`, `lukas@armafit.lt`, `greta@armafit.lt` | athlete |

### Configuration

| Setting | Where | Notes |
|---|---|---|
| `ConnectionStrings:Default` | `appsettings.json` | PostgreSQL connection string |
| `Jwt:Key` | `appsettings.Development.json` | Signing key for access tokens, at least 32 bytes. The committed value is for development only; elsewhere set the `Jwt__Key` environment variable. The app does not start without it. |

## Authentication

Every endpoint needs a logged-in user, except `auth/*` and the OpenAPI document.

| Endpoint | Body | Result |
|---|---|---|
| `POST /api/auth/register` | `email`, `password`, `fullName`, `role` (`athlete` or `trainer`), optional profile fields | `201` with the user |
| `POST /api/auth/login` | `email`, `password` | `accessToken`, `refreshToken`, `user` |
| `POST /api/auth/refresh` | `refreshToken` | a new `accessToken` and `refreshToken` |
| `POST /api/auth/logout` | `refreshToken` | `204`; both tokens of that login stop working |

Send the access token on every other request:

```
Authorization: Bearer <accessToken>
```

How the tokens work:

- **Access token**: a JWT valid for 15 minutes. Its claims are `sub` (user id), `role` (`Athlete` or `Trainer`) and `sid` (session id).
- **Refresh token**: a random string valid for 7 days. Each one works once: refreshing returns a new one and the old one is rejected. Only its hash is stored.
- **Sessions**: each login is a row in `sessions`. Every request checks that the token's session is still valid, so logging out invalidates the access token immediately rather than when it expires. Logging out on one device does not affect other logins.

```bash
# log in
curl -s http://localhost:5087/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"jonas@armafit.lt","password":"Password123!"}'

# call the API
curl -s http://localhost:5087/api/plans -H "Authorization: Bearer $ACCESS_TOKEN"
```

## Who can do what

**The access rule:** an athlete's data (plans, workouts, exercises, logs, progress) can be read and changed by that athlete and by trainers who have an **active** link with them. A link becomes active when the trainer accepts the athlete's invitation.

| Endpoint | Who |
|---|---|
| `GET /api/exercises` | any logged-in user |
| `GET /api/plans` | returns only plans the caller can access; filters: `athleteId`, `createdBy` |
| `POST /api/plans` | an athlete for themselves, a trainer for an active athlete |
| `GET`, `PUT`, `DELETE /api/plans/{id}` | access rule |
| `/api/plans/{planId}/workouts` and `/api/plans/{planId}/workouts/{workoutId}/exercises` (list, get, create, update, delete) | access rule on the plan |
| `GET /api/invitations` | returns only the caller's own invitations; filters: `trainerId`, `athleteId`, `status` |
| `POST /api/invitations` | athletes only; invites a trainer by email |
| `POST /api/invitations/{id}/accept` | trainers only, and only the invited trainer |
| `GET /api/workout-logs` | returns only logs the caller can access; filters: `athleteId`, `workoutId` |
| `POST /api/workout-logs` | athletes only, for a workout in one of their own plans |
| `GET /api/athletes/{athleteId}/progress` | access rule |

The caller's identity always comes from the token. Requests never carry the acting user's id in the body.

## Responses

- Errors are RFC 7807 problem details (`application/problem+json`).
- `401` missing, expired or revoked token. `403` logged in but not allowed. `404` the resource does not exist. `422` the request is valid but refers to something that does not exist or breaks a rule. `409` conflicting state.
- Enums are snake_case strings, for example `"role": "athlete"`.
- Plans, invitations and workout logs are paged (`page`, `pageSize` up to 100) and return `items`, `page`, `pageSize`, `totalCount`, `totalPages`.
- Resources carry a `_links` object with the actions available on them.

## Testing

`auth-smoke.sh` checks authentication, roles and ownership end to end against a running API. It needs `curl`, `jq` and `psql`, registers throwaway users (`authtest-*@example.test`) and deletes them when it finishes.

```bash
./auth-smoke.sh                           # against http://localhost:5087
API=http://localhost:5099 ./auth-smoke.sh
```

`ArmaFit-API/ArmaFit-API.http` has ready-made requests for the login, refresh and logout flow.

## Layout

| Path | Contents |
|---|---|
| `ArmaFit-API/Program.cs` | Startup: database, JWT validation, authorization, OpenAPI |
| `ArmaFit-API/Controllers/` | One controller per resource |
| `ArmaFit-API/Controllers/Access.cs` | The access rule and the guard for routes under `plans/{planId}` |
| `ArmaFit-API/Data/` | Entities, `AppDbContext`, sample data |
| `ArmaFit-API/Models/` | Request and response types, links, paging |
| `schema.dbml` | Database schema |
| `openapi.json` | Exported snapshot of the OpenAPI document; the live one at `/openapi/v1.json` is authoritative |
