namespace Ditak.Attendance.Core.Entities;

public class Company
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Timezone { get; set; } = "UTC";
    public string? LogoPath { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Site
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Timezone { get; set; } = "UTC";
    /// <summary>Comma-separated CIDRs used only for presence hints.</summary>
    public string? AllowedCidrs { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Role
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PreferredLanguage { get; set; } = "en";
    public string ThemePreference { get; set; } = "system";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Employee? Employee { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

public class UserRole
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
}

public class Employee
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    public Guid? SiteId { get; set; }
    public Site? Site { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<AttendanceEvent> AttendanceEvents { get; set; } = new List<AttendanceEvent>();
    public ICollection<AttendanceDaySummary> DaySummaries { get; set; } = new List<AttendanceDaySummary>();
}

public enum AttendanceEventType
{
    In = 1,
    Out = 2
}

public enum AttendanceEventSource
{
    Web = 1,
    Qr = 2,
    Manual = 3,
    Network = 4
}

public class AttendanceEvent
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public Guid? SiteId { get; set; }
    public Site? Site { get; set; }
    public AttendanceEventType EventType { get; set; }
    public AttendanceEventSource Source { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string? ClientIp { get; set; }
    public string? DeviceId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class AttendanceDaySummary
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DateOnly WorkDate { get; set; }
    public int WorkedMinutes { get; set; }
    public bool HasOpenShift { get; set; }
    public DateTimeOffset CalculatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public class PresenceHint
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public Guid? SiteId { get; set; }
    public Site? Site { get; set; }
    public string ClientIp { get; set; } = string.Empty;
    public DateTimeOffset LastSeenAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
