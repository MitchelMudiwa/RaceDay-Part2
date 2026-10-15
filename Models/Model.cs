using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaceDay.Api.Models;

public static class Roles
{
    public const string Organizer = "Organizer";
    public const string Participant = "Participant";
}

public class Account
{
    [Key, MaxLength(20)]
    public string AccountId { get; set; } = "";

    [Required, MaxLength(50)]
    public string FirstName { get; set; } = "";

    [Required, MaxLength(50)]
    public string LastName { get; set; } = "";

    [Required, MaxLength(100)]
    public string Email { get; set; } = "";

    [Required]
    public string PasswordHash { get; set; } = "";

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [Required, MaxLength(20)]
    public string Role { get; set; } = Roles.Participant;

    public string? ProfileImageUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<Event> Events { get; set; } = new();
    public List<Enrollment> Enrollments { get; set; } = new();
}

public class Event
{
    [Key, MaxLength(20)]
    public string EventId { get; set; } = "";

    [Required, MaxLength(20)]
    public string OrganizerId { get; set; } = "";

    [Required, MaxLength(100)]
    public string Name { get; set; } = "";

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime EventDate { get; set; }

    [Required, MaxLength(100)]
    public string Location { get; set; } = "";

    [Required, MaxLength(20)]
    public string Distance { get; set; } = "";

    [Required, MaxLength(20)]
    public string EventType { get; set; } = "Run"; // Run, Walk, Cycle

    public string? BannerImageUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Account? Organizer { get; set; }
    public EventRoute? Route { get; set; }
    public List<Category> Categories { get; set; } = new();
    public List<Enrollment> Enrollments { get; set; } = new();
}

public class EventRoute
{
    [Key, MaxLength(20)]
    public string RouteId { get; set; } = "";

    [Required, MaxLength(20)]
    public string EventId { get; set; } = "";

    [Required, MaxLength(100)]
    public string RouteName { get; set; } = "";

    public string? Description { get; set; }
    public string? MapUrl { get; set; }

    public Event? Event { get; set; }
}

public class Category
{
    [Key, MaxLength(20)]
    public string CategoryId { get; set; } = "";

    [Required, MaxLength(20)]
    public string EventId { get; set; } = "";

    [Required, MaxLength(50)]
    public string Name { get; set; } = "";

    [MaxLength(50)]
    public string? CategoryType { get; set; }

    public int MinAge { get; set; }
    public int MaxAge { get; set; }

    [MaxLength(20)]
    public string? Distance { get; set; }

    public Event? Event { get; set; }
    public List<Enrollment> Enrollments { get; set; } = new();
}

public class Enrollment
{
    [Key, MaxLength(20)]
    public string EnrollmentId { get; set; } = "";

    [Required, MaxLength(20)]
    public string ParticipantId { get; set; } = "";

    [Required, MaxLength(20)]
    public string EventId { get; set; } = "";

    [Required, MaxLength(20)]
    public string CategoryId { get; set; } = "";

    [Required, MaxLength(20)]
    public string Status { get; set; } = "Pending"; // Pending, Confirmed, Cancelled

    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;

    public Account? Participant { get; set; }
    public Event? Event { get; set; }
    public Category? Category { get; set; }
    public Result? Result { get; set; }
}

[Table("Results")]
public class Result
{
    [Key, MaxLength(20)]
    public string ResultId { get; set; } = "";

    [Required, MaxLength(20)]
    public string EnrollmentId { get; set; } = "";

    public TimeSpan FinishTime { get; set; }
    public int FinishPosition { get; set; }
    public bool IsPublished { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }

    public Enrollment? Enrollment { get; set; }
}