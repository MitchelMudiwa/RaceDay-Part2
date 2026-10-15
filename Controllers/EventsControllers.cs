using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/events")]
[SessionAuth]
public class EventsController : ApiBase
{
    private readonly AppDbContext _db;
    public EventsController(AppDbContext db) { _db = db; }

    private static string? NormalizeType(string? t) => t?.Trim().ToLowerInvariant() switch
    {
        "run" => "Run",
        "walk" => "Walk",
        "cycle" => "Cycle",
        _ => null
    };

    /// <summary>List all events (any logged-in user). Optional filter: ?eventType=run, walk or cycle.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EventResponse>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetAll([FromQuery] string? eventType)
    {
        var query = _db.Events.AsQueryable();
        if (!string.IsNullOrWhiteSpace(eventType))
        {
            var type = NormalizeType(eventType);
            if (type == null) return BadRequest(new { message = "eventType must be run, walk or cycle." });
            query = query.Where(e => e.EventType == type);
        }
        var list = await query.OrderBy(e => e.EventDate).ToListAsync();
        return Ok(list.Select(EventResponse.From));
    }

    /// <summary>View one event by its ID (any logged-in user).</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(EventResponse), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(string id)
    {
        var ev = await _db.Events.FindAsync(id);
        if (ev == null) return NotFound(new { message = "Event not found." });
        return Ok(EventResponse.From(ev));
    }

    /// <summary>Create an event (Organizer only). Event type must be run, walk or cycle.</summary>
    [HttpPost]
    [SessionAuth(Roles.Organizer)]
    [ProducesResponseType(typeof(EventResponse), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> Create(EventRequest req)
    {
        var type = NormalizeType(req.EventType);
        if (type == null) return BadRequest(new { message = "eventType must be run, walk or cycle." });
        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Location) || string.IsNullOrWhiteSpace(req.Distance))
            return BadRequest(new { message = "Name, location and distance are required." });
        if (req.EventDate == default) return BadRequest(new { message = "A valid eventDate is required." });

        var ev = new Event
        {
            EventId = Ids.New("EVT"),
            OrganizerId = UserId,
            Name = req.Name.Trim(),
            Description = req.Description,
            EventDate = req.EventDate,
            Location = req.Location.Trim(),
            Distance = req.Distance.Trim(),
            EventType = type
        };
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = ev.EventId }, EventResponse.From(ev));
    }

    /// <summary>Update your own event (Organizer only). Only the fields you send are changed.</summary>
    [HttpPut("{id}")]
    [SessionAuth(Roles.Organizer)]
    [ProducesResponseType(typeof(EventResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(string id, UpdateEventRequest req)
    {
        var ev = await _db.Events.FindAsync(id);
        if (ev == null) return NotFound(new { message = "Event not found." });
        if (ev.OrganizerId != UserId) return Forbidden("You can only change your own events.");

        if (req.EventType != null)
        {
            var type = NormalizeType(req.EventType);
            if (type == null) return BadRequest(new { message = "eventType must be run, walk or cycle." });
            ev.EventType = type;
        }
        if (!string.IsNullOrWhiteSpace(req.Name)) ev.Name = req.Name.Trim();
        if (req.Description != null) ev.Description = req.Description;
        if (req.EventDate.HasValue) ev.EventDate = req.EventDate.Value;
        if (!string.IsNullOrWhiteSpace(req.Location)) ev.Location = req.Location.Trim();
        if (!string.IsNullOrWhiteSpace(req.Distance)) ev.Distance = req.Distance.Trim();

        ev.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(EventResponse.From(ev));
    }

    /// <summary>Delete your own event (Organizer only). Not allowed if people are already enrolled.</summary>
    [HttpDelete("{id}")]
    [SessionAuth(Roles.Organizer)]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Delete(string id)
    {
        var ev = await _db.Events.FindAsync(id);
        if (ev == null) return NotFound(new { message = "Event not found." });
        if (ev.OrganizerId != UserId) return Forbidden("You can only delete your own events.");
        if (await _db.Enrollments.AnyAsync(e => e.EventId == id))
            return Conflict(new { message = "This event already has enrolments, so it cannot be deleted." });

        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}