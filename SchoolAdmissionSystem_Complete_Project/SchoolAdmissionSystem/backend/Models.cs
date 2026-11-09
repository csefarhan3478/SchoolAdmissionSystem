using Microsoft.EntityFrameworkCore;

namespace SchoolAdmission.Api;

public enum HoldStatus { HELD, CONFIRMED, EXPIRED, CANCELLED }

public sealed class AdmissionClass
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int TotalSeats { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<AdmissionHold> Holds { get; set; } = [];
}

public sealed class AdmissionHold
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public AdmissionClass Class { get; set; } = null!;
    public string StudentId { get; set; } = "";
    public HoldStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
}

public sealed record CreateClassRequest(string Name, int TotalSeats);

public sealed class AdmissionDb(DbContextOptions<AdmissionDb> options) : DbContext(options)
{
    public DbSet<AdmissionClass> Classes => Set<AdmissionClass>();
    public DbSet<AdmissionHold> Holds => Set<AdmissionHold>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<AdmissionClass>().HasIndex(x => x.Name).IsUnique();
        model.Entity<AdmissionHold>().HasOne(x => x.Class).WithMany(x => x.Holds).HasForeignKey(x => x.ClassId);
        model.Entity<AdmissionHold>().Property(x => x.Status).HasConversion<string>();
        model.Entity<AdmissionHold>().HasIndex(x => new { x.StudentId, x.Status });
        model.Entity<AdmissionHold>().HasIndex(x => new { x.ClassId, x.Status, x.ExpiresAt });
    }
}
