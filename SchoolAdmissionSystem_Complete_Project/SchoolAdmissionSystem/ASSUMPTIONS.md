# Assumptions and design decisions

1. **Class and shift are one admission unit.** The assignment's example calls `Class 6 - Morning Shift` a class. Each distinct class/shift combination is stored as one row with its own capacity.
2. **Student identifier.** The assignment describes a 17-digit birth registration number and the `X-Student-Id` header. The API requires exactly 17 numeric digits.
3. **One active hold per student.** A student may have only one unexpired `HELD` application at a time, including across classes. This avoids reserving multiple seats simultaneously.
4. **Hold duration.** `HOLD_DURATION_SECONDS` controls the hold window; default is 600 seconds (10 minutes).
5. **Payment.** The assignment says to treat the payment action as successful and does not require a gateway. The UI therefore provides a simulated payment confirmation.
6. **Admin authentication.** No authentication contract or credentials are supplied. The admin UI is open for assignment demonstration only; production deployment must add secure authentication and role-based authorization.
7. **Expiration.** Expired holds are marked `EXPIRED` on API activity and excluded from seat occupancy immediately based on their expiry timestamp. The UI refreshes every five seconds. A production version should add a hosted background worker for proactive status updates even when no API traffic occurs.
8. **Seat concurrency.** A PostgreSQL row-level lock is acquired for the target class inside a transaction before counting active holds and creating a new one. This serializes allocation for a given class and prevents overbooking across concurrent requests.
9. **Persistence.** PostgreSQL uses a named Docker volume, so data survives container restarts and `docker compose down`.
10. **IDs.** Public IDs use `c_{integer}` and `h_{integer}` as illustrative formats; numeric IDs are accepted too.
11. **Frontend API address.** `VITE_API_URL` configures the API base URL at frontend build time. The compose configuration uses `http://localhost:8080` for the browser.
12. **Seat totals.** Admin creates a class with its capacity. Editing/deleting a class is not included because no endpoint is specified in the assignment.
