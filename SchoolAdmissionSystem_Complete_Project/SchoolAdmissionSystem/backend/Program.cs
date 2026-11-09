using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AdmissionDb>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")
        ?? "Host=localhost;Database=school_admission;Username=postgres;Password=postgres"));
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AdmissionDb>();
    var retries = 0;
    while (true)
    {
        try { await db.Database.EnsureCreatedAsync(); break; }
        catch when (++retries < 20) { await Task.Delay(2000); }
    }
}

var holdSeconds = int.TryParse(Environment.GetEnvironmentVariable("HOLD_DURATION_SECONDS"), out var h) && h > 0 ? h : 600;

async Task ExpireHolds(AdmissionDb db)
{
    var expired = await db.Holds.Where(x => x.Status == HoldStatus.HELD && x.ExpiresAt <= DateTimeOffset.UtcNow).ToListAsync();
    foreach (var item in expired) item.Status = HoldStatus.EXPIRED;
    if (expired.Count > 0) await db.SaveChangesAsync();
}

app.MapGet("/", () => Results.Ok(new { name = "School Admission API", docs = "/swagger" }));

app.MapPost("/classes", async (CreateClassRequest req, AdmissionDb db) =>
{
    if (string.IsNullOrWhiteSpace(req.Name) || req.TotalSeats < 1)
        return Results.BadRequest(new { message = "Enter a class name and a seat limit greater than zero." });
    var item = new AdmissionClass { Name = req.Name.Trim(), TotalSeats = req.TotalSeats };
    db.Classes.Add(item);
    await db.SaveChangesAsync();
    return Results.Created($"/classes/{item.Id}", new { id = $"c_{item.Id}", name = item.Name, totalSeats = item.TotalSeats });
});

app.MapGet("/classes", async (AdmissionDb db) =>
{
    await ExpireHolds(db);
    var rows = await db.Classes.OrderBy(x => x.CreatedAt).Select(x => new {
        id = $"c_{x.Id}", name = x.Name, totalSeats = x.TotalSeats,
        heldSeats = x.Holds.Count(h => h.Status == HoldStatus.HELD && h.ExpiresAt > DateTimeOffset.UtcNow),
        confirmedSeats = x.Holds.Count(h => h.Status == HoldStatus.CONFIRMED)
    }).ToListAsync();
    return Results.Ok(rows.Select(x => new { x.id, x.name, x.totalSeats, x.heldSeats, x.confirmedSeats,
        availableSeats = Math.Max(0, x.totalSeats - x.heldSeats - x.confirmedSeats) }));
});

app.MapGet("/classes/{classId}", async (string classId, AdmissionDb db) =>
{
    await ExpireHolds(db);
    if (!TryId(classId, out var id)) return Results.NotFound(new { message = "Class not found." });
    var x = await db.Classes.Include(c => c.Holds).FirstOrDefaultAsync(c => c.Id == id);
    if (x is null) return Results.NotFound(new { message = "Class not found." });
    var held = x.Holds.Count(h => h.Status == HoldStatus.HELD && h.ExpiresAt > DateTimeOffset.UtcNow);
    var confirmed = x.Holds.Count(h => h.Status == HoldStatus.CONFIRMED);
    return Results.Ok(new { id = $"c_{x.Id}", name = x.Name, totalSeats = x.TotalSeats,
        heldSeats = held, confirmedSeats = confirmed, availableSeats = Math.Max(0, x.TotalSeats-held-confirmed) });
});

app.MapPost("/classes/{classId}/holds", async (string classId, HttpRequest request, AdmissionDb db) =>
{
    var studentId = request.Headers["X-Student-Id"].FirstOrDefault();
    if (!ValidStudent(studentId)) return Results.BadRequest(new { message = "Enter a valid 17-digit birth registration number in X-Student-Id." });
    if (!TryId(classId, out var id)) return Results.NotFound(new { message = "Class not found." });

    // Lock the student first (one active hold globally), then the class (capacity protection).
    await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({studentId}))");
    var cls = await db.Classes.FromSqlInterpolated($"SELECT * FROM \"Classes\" WHERE \"Id\" = {id} FOR UPDATE").FirstOrDefaultAsync();
    if (cls is null) return Results.NotFound(new { message = "Class not found." });

    var now = DateTimeOffset.UtcNow;
    var expired = await db.Holds.Where(x => x.ClassId == id && x.Status == HoldStatus.HELD && x.ExpiresAt <= now).ToListAsync();
    foreach (var e in expired) e.Status = HoldStatus.EXPIRED;
    var activeForStudent = await db.Holds.AnyAsync(x => x.StudentId == studentId && x.Status == HoldStatus.HELD && x.ExpiresAt > now);
    if (activeForStudent) return Results.Conflict(new { message = "You already have an active seat reservation. Confirm or cancel it first." });

    var occupied = await db.Holds.CountAsync(x => x.ClassId == id &&
        (x.Status == HoldStatus.CONFIRMED || (x.Status == HoldStatus.HELD && x.ExpiresAt > now)));
    if (occupied >= cls.TotalSeats) return Results.Conflict(new { message = "No seats are currently available in this class." });

    var hold = new AdmissionHold { ClassId = id, StudentId = studentId!, Status = HoldStatus.HELD,
        ExpiresAt = now.AddSeconds(holdSeconds) };
    db.Holds.Add(hold);
    await db.SaveChangesAsync();
    await tx.CommitAsync();
    return Results.Created($"/holds/h_{hold.Id}", new { holdId = $"h_{hold.Id}", classId = $"c_{id}",
        studentId, status = "HELD", expiresAt = hold.ExpiresAt });
});

app.MapGet("/holds/{holdId}", async (string holdId, AdmissionDb db) =>
{
    await ExpireHolds(db);
    if (!TryId(holdId, out var id)) return Results.NotFound(new { message = "Reservation not found." });
    var h = await db.Holds.FirstOrDefaultAsync(x => x.Id == id);
    return h is null ? Results.NotFound(new { message = "Reservation not found." }) :
        Results.Ok(new { holdId = $"h_{h.Id}", classId = $"c_{h.ClassId}", status = h.Status.ToString(), expiresAt = h.ExpiresAt });
});

app.MapPost("/holds/{holdId}/confirm", async (string holdId, HttpRequest request, AdmissionDb db) =>
{
    var studentId = request.Headers["X-Student-Id"].FirstOrDefault();
    if (!ValidStudent(studentId)) return Results.BadRequest(new { message = "Student ID is required." });
    if (!TryId(holdId, out var id)) return Results.NotFound(new { message = "Reservation not found." });
    await using var tx = await db.Database.BeginTransactionAsync();
    var hold = await db.Holds.FirstOrDefaultAsync(x => x.Id == id);
    if (hold is null || hold.StudentId != studentId) return Results.NotFound(new { message = "Reservation not found for this student." });
    if (hold.Status == HoldStatus.CONFIRMED) return Results.Ok(new { holdId = $"h_{hold.Id}", status = "CONFIRMED" });
    if (hold.Status != HoldStatus.HELD || hold.ExpiresAt <= DateTimeOffset.UtcNow)
    {
        hold.Status = HoldStatus.EXPIRED; await db.SaveChangesAsync();
        return Results.Conflict(new { message = "This reservation has expired. Please apply again." });
    }
    hold.Status = HoldStatus.CONFIRMED; hold.ConfirmedAt = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync(); await tx.CommitAsync();
    return Results.Ok(new { holdId = $"h_{hold.Id}", status = "CONFIRMED" });
});

app.MapDelete("/holds/{holdId}", async (string holdId, HttpRequest request, AdmissionDb db) =>
{
    var studentId = request.Headers["X-Student-Id"].FirstOrDefault();
    if (!ValidStudent(studentId)) return Results.BadRequest(new { message = "Student ID is required." });
    if (!TryId(holdId, out var id)) return Results.NotFound();
    var hold = await db.Holds.FirstOrDefaultAsync(x => x.Id == id && x.StudentId == studentId);
    if (hold is null) return Results.NotFound(new { message = "Reservation not found." });
    if (hold.Status == HoldStatus.HELD) { hold.Status = HoldStatus.CANCELLED; await db.SaveChangesAsync(); }
    return Results.NoContent();
});

app.MapGet("/me/holds", async (HttpRequest request, AdmissionDb db) =>
{
    var studentId = request.Headers["X-Student-Id"].FirstOrDefault();
    if (!ValidStudent(studentId)) return Results.BadRequest(new { message = "Student ID is required." });
    await ExpireHolds(db);
    var rows = await db.Holds.Where(x => x.StudentId == studentId).OrderByDescending(x => x.CreatedAt)
        .Select(x => new { holdId = $"h_{x.Id}", classId = $"c_{x.ClassId}", className = x.Class.Name,
            status = x.Status.ToString(), expiresAt = x.ExpiresAt }).ToListAsync();
    return Results.Ok(rows);
});

app.Run();

static bool TryId(string raw, out int id)
{
    var value = raw.StartsWith("c_") || raw.StartsWith("h_") ? raw[2..] : raw;
    return int.TryParse(value, out id) && id > 0;
}
static bool ValidStudent(string? id) => !string.IsNullOrWhiteSpace(id) && id.Length == 17 && id.All(char.IsDigit);

public partial class Program { }
