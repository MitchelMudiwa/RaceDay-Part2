using System.Globalization;
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
public class ResultsController : ApiBase
{
    private readonly AppDbContext _db;
    public ResultsController(AppDbContext db) { _db = db; }

    private IQueryable<Result> Full => _db.Results
        .Include(r => r.Enrollment).ThenInclude(e => e!.Participant)
        .Include(r => r.Enrollment).ThenInclude(e => e!.Event)
        .Include(r => r.Enrollment).ThenInclude(e => e!.Category);

    private static bool TryTime(string? text, out TimeSpan time) =>
        TimeSpan.TryParseExact(text, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out time);

    /// <summary>Capture finish times and positions for participants of your own event (Organizer only).</summary>
    /// <remarks>finishTime format is HH:MM:SS. Each participant must be enrolled in the event.</remarks>
    [HttpPost("events/{eventId}/results")]
    [SessionAuth(Roles.Organizer)]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Capture(string eventId, CaptureResultsRequest req)
    {
        var ev = await _db.Events.FindAsync(eventId);
        if (ev == null) return NotFound(new { message = "Event not found." });
        if (ev.OrganizerId != UserId) return Forbidden("You can only capture results for your own events.");
        if (req.Results == null || req.Results.Count == 0) return BadRequest(new { message = "Send at least one result." });

        var toAdd = new List<Result>();
        foreach (var item in req.Results)
        {
            if (!TryTime(item.FinishTime, out var time))
                return BadRequest(new { message = $"finishTime '{item.FinishTime}' must look like HH:MM:SS." });
            if (item.Position < 1)
                return BadRequest(new { message = "Position must be 1 or higher." });

            var enrollment = await _db.Enrollments.Include(e => e.Result)
                .FirstOrDefaultAsync(e => e.EventId == eventId && e.ParticipantId == item.ParticipantId && e.Status != "Cancelled");
            if (enrollment == null)
                return BadRequest(new { message = $"Participant {item.ParticipantId} is not enrolled in this event." });
            if (enrollment.Result != null || toAdd.Any(r => r.EnrollmentId == enrollment.EnrollmentId))
                return Conflict(new { message = $"Participant {item.ParticipantId} already has a result." });

            toAdd.Add(new Result
            {
                ResultId = Ids.New("RES"),
                EnrollmentId = enrollment.EnrollmentId,
                FinishTime = time,
                FinishPosition = item.Position,
                IsPublished = true,
                PublishedAt = DateTime.UtcNow
            });
        }

        _db.Results.AddRange(toAdd);
        await _db.SaveChangesAsync();

        var ids = toAdd.Select(r => r.ResultId).ToList();
        var saved = await Full.Where(r => ids.Contains(r.ResultId)).ToListAsync();
        return StatusCode(201, saved.Select(ResultResponse.From));
    }

    /// <summary>Correct one result on your own event (Organizer only).</summary>
    [HttpPut("results/{id}")]
    [SessionAuth(Roles.Organizer)]
    [ProducesResponseType(typeof(ResultResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(string id, UpdateResultRequest req)
    {
        var result = await Full.FirstOrDefaultAsync(r => r.ResultId == id);
        if (result == null) return NotFound(new { message = "Result not found." });
        if (result.Enrollment!.Event!.OrganizerId != UserId) return Forbidden("You can only change results for your own events.");

        if (req.FinishTime != null)
        {
            if (!TryTime(req.FinishTime, out var time)) return BadRequest(new { message = "finishTime must look like HH:MM:SS." });
            result.FinishTime = time;
        }
        if (req.Position.HasValue)
        {
            if (req.Position.Value < 1) return BadRequest(new { message = "Position must be 1 or higher." });
            result.FinishPosition = req.Position.Value;
        }

        await _db.SaveChangesAsync();
        return Ok(ResultResponse.From(result));
    }

    /// <summary>View results for an event. Organizers see everyone; Participants only see their own.</summary>
    [HttpGet("events/{eventId}/results")]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetForEvent(string eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
            return NotFound(new { message = "Event not found." });

        var query = Full.Where(r => r.Enrollment!.EventId == eventId);
        if (UserRole == Roles.Participant)
            query = query.Where(r => r.Enrollment!.ParticipantId == UserId);

        var list = await query.OrderBy(r => r.FinishPosition).ToListAsync();
        return Ok(list.Select(ResultResponse.From));
    }

    /// <summary>View your own results across all events (Participant only).</summary>
    [HttpGet("results/me")]
    [SessionAuth(Roles.Participant)]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetMine()
    {
        var list = await Full.Where(r => r.Enrollment!.ParticipantId == UserId)
            .OrderBy(r => r.Enrollment!.EnrolledAt).ToListAsync();
        return Ok(list.Select(ResultResponse.From));
    }
}