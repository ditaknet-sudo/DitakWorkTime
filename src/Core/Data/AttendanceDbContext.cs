using Ditak.Attendance.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ditak.Attendance.Core.Data;

public class AttendanceDbContext : DbContext
{
    public AttendanceDbContext(DbContextOptions<AttendanceDbContext> options) : base(options)
    {
    }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<AttendanceEvent> AttendanceEvents => Set<AttendanceEvent>();
    public DbSet<AttendanceDaySummary> AttendanceDaySummaries => Set<AttendanceDaySummary>();
    public DbSet<PresenceHint> PresenceHints => Set<PresenceHint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>(e =>
        {
            e.ToTable("companies");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Timezone).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<Site>(e =>
        {
            e.ToTable("sites");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Timezone).HasMaxLength(100).IsRequired();
            e.Property(x => x.AllowedCidrs).HasMaxLength(1000);
        });

        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("roles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            e.Property(x => x.PreferredLanguage).HasMaxLength(10).HasDefaultValue("en");
            e.Property(x => x.ThemePreference).HasMaxLength(20).HasDefaultValue("system");
        });

        modelBuilder.Entity<UserRole>(e =>
        {
            e.ToTable("user_roles");
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId);
        });

        modelBuilder.Entity<Employee>(e =>
        {
            e.ToTable("employees");
            e.HasKey(x => x.Id);
            e.Property(x => x.EmployeeCode).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.EmployeeCode).IsUnique();
            e.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Department).HasMaxLength(200);
            e.HasOne(x => x.User).WithOne(x => x.Employee).HasForeignKey<Employee>(x => x.UserId);
            e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId);
        });

        modelBuilder.Entity<AttendanceEvent>(e =>
        {
            e.ToTable("attendance_events");
            e.HasKey(x => x.Id);
            e.Property(x => x.ClientIp).HasMaxLength(64);
            e.Property(x => x.DeviceId).HasMaxLength(100);
            e.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.IdempotencyKey).IsUnique();
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasIndex(x => new { x.EmployeeId, x.OccurredAtUtc });
            e.HasOne(x => x.Employee).WithMany(x => x.AttendanceEvents).HasForeignKey(x => x.EmployeeId);
            e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId);
        });

        modelBuilder.Entity<AttendanceDaySummary>(e =>
        {
            e.ToTable("attendance_day_summaries");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EmployeeId, x.WorkDate }).IsUnique();
            e.HasOne(x => x.Employee).WithMany(x => x.DaySummaries).HasForeignKey(x => x.EmployeeId);
        });

        modelBuilder.Entity<PresenceHint>(e =>
        {
            e.ToTable("presence_hints");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.EmployeeId).IsUnique();
            e.Property(x => x.ClientIp).HasMaxLength(64).IsRequired();
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
            e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId);
        });
    }
}
