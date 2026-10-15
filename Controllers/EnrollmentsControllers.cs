using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api")]
[SessionAuth]
public class EnrollmentsController : ApiBase
{
    private readonly AppDbContext _db;
    public EnrollmentsController(AppDbContext db) { _db = db; }

    private IQueryable<Enrollment> Full => _db.Enrollments
        .Include(e => e.Participant).Include(e => e.Event).Include(e => e.Category);

    /// <summary>Enrol yourself in an event by choosing one of its categories (Participant only).</summary>
    [HttpPost("events/{eventId}/enrolments")]
    [SessionAuth(Roles.Participant)]
    [ProducesResponseType(typeof(EnrollmentResponse), 201)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Enrol(string eventId, EnrolRequest req)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
            return NotFound(new { message = "Event not found." });

        var category = await _db.Categories.FirstOrDefaultAsync(c => c.CategoryId == req.CategoryId && c.EventId == eventId);
        if (category == null) return NotFound(new { message = "That category does not belong to this event." });

        if (await _db.Enrollments.AnyAsync(e => e.EventId == eventId && e.ParticipantId == UserId))
            return Conflict(new { message = "You are already enrolled in this event." });

        var enrollment = new Enrollment
        {
            EnrollmentId = Ids.New("ENR"),
            ParticipantId = UserId,
            EventId = eventId,
            CategoryId = category.CategoryId,
            Status = "Confirmed"
        };
        _db.Enrollments.Add(enrollment);
        await _db.SaveChangesAsync();

        var saved = await Full.FirstAsync(e => e.EnrollmentId == enrollment.EnrollmentId);
        return StatusCode(201, EnrollmentResponse.From(saved));
    }

    /// <summary>View all enrolments for one of your own events (Organizer only).</summary>
    [HttpGet("events/{eventId}/enrolments")]
    [SessionAuth(Roles.Organizer)]
    [ProducesResponseType(typeof(IEnumerable<EnrollmentResponse>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetForEvent(string eventId)
    {
        var ev = await _db.Events.FindAsync(eventId);
        if (ev == null) return NotFound(new { message = "Event not found." });
        if (ev.OrganizerId != UserId) return Forbidden("You can only see enrolments for your own events.");

        var list = await Full.Where(e => e.EventId == eventId).OrderBy(e => e.EnrolledAt).ToListAsync();
        return Ok(list.Select(EnrollmentResponse.From));
    }

    /// <summary>View your own enrolments across all events (Participant only).</summary>
    [HttpGet("enrolments/me")]
    [SessionAuth(Roles.Participant)]
    [ProducesResponseType(typeof(IEnumerable<EnrollmentResponse>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetMine()
    {
        var list = await Full.Where(e => e.ParticipantId == UserId).OrderBy(e => e.EnrolledAt).ToListAsync();
        return Ok(list.Select(EnrollmentResponse.From));
    }

    /// <summary>Cancel your own enrolment (Participant only). Not allowed once a result exists.</summary>
    [HttpDelete("enrolments/{id}")]
    [SessionAuth(Roles.Participant)]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Cancel(string id)
    {
        var enrollment = await _db.Enrollments.Include(e => e.Result).FirstOrDefaultAsync(e => e.EnrollmentId == id);
        if (enrollment == null) return NotFound(new { message = "Enrolment not found." });
        if (enrollment.ParticipantId != UserId) return Forbidden("You can only cancel your own enrolments.");
        if (enrollment.Result != null) return Conflict(new { message = "A result already exists for this enrolment." });

        _db.Enrollments.Remove(enrollment);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}