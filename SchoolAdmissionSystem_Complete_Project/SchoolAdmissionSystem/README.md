# School Admission System

A take-home assignment implementation using .NET 8, PostgreSQL, EF Core, React 18 + TypeScript and Docker Compose.

## Run the complete project

Requirements: Docker Desktop (with Docker Compose). No local .NET, Node.js or PostgreSQL installation is needed.

1. Extract this repository.
2. Open a terminal in the project root (the folder containing `docker-compose.yml`).
3. Run:

```bash
docker compose up --build
```

4. Open the UI: http://localhost:5173
5. Open API Swagger: http://localhost:8080/swagger

Stop with `Ctrl+C`, or run `docker compose down`. Database data is stored in the named Docker volume `postgres_data` and survives `docker compose down`. Do not use `docker compose down -v` unless you intentionally want to delete the database.

## Quick demo

1. Select **School Admin** and create `Class 6 - Morning Shift` with 2 seats.
2. Select **Parent / Student** and enter a 17-digit numeric birth registration number.
3. Save the ID and reserve a seat.
4. Click **Pay & Confirm** to simulate successful payment, or Cancel to release a held seat.
5. Use another browser/private window with a different 17-digit ID to test the second seat.

Hold duration defaults to 600 seconds. To change it, create a `.env` file in the root:

```env
HOLD_DURATION_SECONDS=60
```

Then restart with `docker compose up --build`.

## API contract

| Method | Path | Purpose |
|---|---|---|
| POST | `/classes` | Create class / shift |
| GET | `/classes` | List classes and seat counts |
| GET | `/classes/{classId}` | Class detail and seat counts |
| POST | `/classes/{classId}/holds` | Reserve a seat; requires `X-Student-Id` |
| GET | `/holds/{holdId}` | Read reservation |
| POST | `/holds/{holdId}/confirm` | Confirm simulated payment; requires `X-Student-Id` |
| DELETE | `/holds/{holdId}` | Cancel reservation; requires `X-Student-Id` |
| GET | `/me/holds` | List this student's reservations; requires `X-Student-Id` |

IDs are returned as `c_1` and `h_1` for readability. Numeric IDs are also accepted by the API.

## Important implementation notes

- Seat reservation locks the relevant PostgreSQL class row (`SELECT ... FOR UPDATE`) inside a transaction before counting occupied seats and creating a hold. Concurrent reservations for one class are serialized.
- Expired holds are changed to `EXPIRED` when relevant API requests are processed. The UI polls seat counts and application status every five seconds.
- Confirm is idempotent: confirming an already confirmed hold does not increment a counter or create another admission.
- Payment is intentionally simulated. No payment gateway or real money transaction is connected.
- The assignment brief does not define authentication. The admin screen is therefore a demo-only open screen, not production-secure.
- Student ID is a 17-digit numeric identifier in this implementation, as demonstrated in the assignment.

## Validation checklist

- [ ] Create a class with a small capacity (e.g. 2).
- [ ] Reserve both seats using two different 17-digit student IDs.
- [ ] Attempt a third reservation; it should return HTTP 409.
- [ ] Confirm a held reservation; it should show `CONFIRMED`.
- [ ] Cancel a held reservation; it should free capacity.
- [ ] Set `HOLD_DURATION_SECONDS=10`, reserve a seat, wait, and refresh; it should expire.
- [ ] Restart containers with `docker compose down` and `docker compose up`; class data should remain.

## Development

Backend: `backend/Program.cs`, `backend/Models.cs`  
Frontend: `frontend/src/App.tsx`, `frontend/src/style.css`

The project uses EF Core `EnsureCreated` for a zero-command demo bootstrap. For a production deployment, replace this with versioned EF Core migrations, authentication/authorization, payment integration, rate limiting, structured logging and a background expiration worker.

## Automated checks

With the containers running, execute:

```bash
python tests/api_smoke_test.py
python tests/concurrency_test.py
```

The smoke suite checks class creation/listing, reservation and idempotent confirmation, and capacity enforcement. The concurrency script fires 30 simultaneous requests at a five-seat class and expects exactly five successful holds and 25 HTTP 409 responses. Python 3 is needed only to run these optional test scripts; it is not needed to run the application itself.
