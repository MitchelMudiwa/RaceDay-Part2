using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

// ---------- Auth and profile ----------
public record RegisterRequest(string FirstName, string LastName, string Email, string Password, string Role, string? PhoneNumber = null);
public record LoginRequest(string Email, string Password);
public record UpdateProfileRequest(string? FirstName, string? LastName, string? Email, string? PhoneNumber, string? Password);

public record UserResponse(string Id, string FirstName, string LastName, string Email, string? PhoneNumber, string Role, DateTime CreatedAt)
{
    public static UserResponse From(Account a) =>
        new(a.AccountId, a.FirstName, a.LastName, a.Email, a.PhoneNumber, a.Role, a.CreatedAt);
}

// ---------- Events ----------
public record EventRequest(string Name, string? Description, DateTime EventDate, string Location, string Distance, string EventType);
public record UpdateEventRequest(string? Name, string? Description, DateTime? EventDate, string? Location, string? Distance, string? EventType);

public record EventResponse(string Id, string Name, string? Description, DateTime EventDate, string Location, string Distance, string EventType, string OrganizerId)
{
    public static EventResponse From(Event e) =>
        new(e.EventId, e.Name, e.Description, e.EventDate, e.Location, e.Distance, e.EventType, e.OrganizerId);
}

// ---------- Categories ----------
public record CategoryRequest(string Name, string? CategoryType, int MinAge, int MaxAge, string? Distance);
public record UpdateCategoryRequest(string? Name, string? CategoryType, int? MinAge, int? MaxAge, string? Distance);

public record CategoryResponse(string Id, string EventId, string Name, string? CategoryType, int MinAge, int MaxAge, string? Distance)
{
    public static CategoryResponse From(Category c) =>
        new(c.CategoryId, c.EventId, c.Name, c.CategoryType, c.MinAge, c.MaxAge, c.Distance);
}

// ---------- Enrolments ----------
public record EnrolRequest(string CategoryId);

public record EnrollmentResponse(string Id, string EventId, string EventName, string ParticipantId, string ParticipantName,
    string CategoryId, string CategoryName, string Status, DateTime EnrolledAt)
{
    public static EnrollmentResponse From(Enrollment e) =>
        new(e.EnrollmentId, e.EventId, e.Event?.Name ?? "", e.ParticipantId,
            e.Participant == null ? "" : e.Participant.FirstName + " " + e.Participant.LastName,
            e.CategoryId, e.Category?.Name ?? "", e.Status, e.EnrolledAt);
}

// ---------- Results ----------
public record ResultItem(string ParticipantId, string FinishTime, int Position);
public record CaptureResultsRequest(List<ResultItem> Results);
public record UpdateResultRequest(string? FinishTime, int? Position);

public record ResultResponse(string Id, string EventId, string EventName, string ParticipantId, string ParticipantName,
    string CategoryId, string CategoryName, string FinishTime, int Position)
{
    public static ResultResponse From(Result r)
    {
        var en = r.Enrollment!;
        return new(r.ResultId, en.EventId, en.Event?.Name ?? "", en.ParticipantId,
            en.Participant == null ? "" : en.Participant.FirstName + " " + en.Participant.LastName,
            en.CategoryId, en.Category?.Name ?? "", r.FinishTime.ToString(@"hh\:mm\:ss"), r.FinishPosition);
    }
}