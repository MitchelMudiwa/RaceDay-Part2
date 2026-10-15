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
public class CategoriesController : ApiBase
{
    private readonly AppDbContext _db;
    public CategoriesController(AppDbContext db) { _db = db; }

    /// <summary>List all categories for an event (any logged-in user).</summary>
    [HttpGet("events/{eventId}/categories")]
    [ProducesResponseType(typeof(IEnumerable<CategoryResponse>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetForEvent(string eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
            return NotFound(new { message = "Event not found." });

        var list = await _db.Categories.Where(c => c.EventId == eventId).OrderBy(c => c.MinAge).ToListAsync();
        return Ok(list.Select(CategoryResponse.From));
    }

    /// <summary>Create a category (age or distance) for your own event (Organizer only).</summary>
    [HttpPost("events/{eventId}/categories")]
    [SessionAuth(Roles.Organizer)]
    [ProducesResponseType(typeof(CategoryResponse), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Create(string eventId, CategoryRequest req)
    {
        var ev = await _db.Events.FindAsync(eventId);
        if (ev == null) return NotFound(new { message = "Event not found." });
        if (ev.OrganizerId != UserId) return Forbidden("You can only add categories to your own events.");
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { message = "Name is required." });
        if (req.MinAge < 0 || req.MaxAge < req.MinAge) return BadRequest(new { message = "Ages are not valid." });

        var cat = new Category
        {
            CategoryId = Ids.New("CAT"),
            EventId = eventId,
            Name = req.Name.Trim(),
            CategoryType = req.CategoryType,
            MinAge = req.MinAge,
            MaxAge = req.MaxAge,
            Distance = req.Distance
        };
        _db.Categories.Add(cat);
        await _db.SaveChangesAsync();
        return StatusCode(201, CategoryResponse.From(cat));
    }

    /// <summary>Update a category on your own event (Organizer only).</summary>
    [HttpPut("categories/{id}")]
    [SessionAuth(Roles.Organizer)]
    [ProducesResponseType(typeof(CategoryResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(string id, UpdateCategoryRequest req)
    {
        var cat = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (cat == null) return NotFound(new { message = "Category not found." });
        if (cat.Event!.OrganizerId != UserId) return Forbidden("You can only change categories on your own events.");

        if (!string.IsNullOrWhiteSpace(req.Name)) cat.Name = req.Name.Trim();
        if (req.CategoryType != null) cat.CategoryType = req.CategoryType;
        if (req.MinAge.HasValue) cat.MinAge = req.MinAge.Value;
        if (req.MaxAge.HasValue) cat.MaxAge = req.MaxAge.Value;
        if (req.Distance != null) cat.Distance = req.Distance;
        if (cat.MinAge < 0 || cat.MaxAge < cat.MinAge) return BadRequest(new { message = "Ages are not valid." });

        await _db.SaveChangesAsync();
        return Ok(CategoryResponse.From(cat));
    }

    /// <summary>Delete a category on your own event (Organizer only). Not allowed if people are enrolled in it.</summary>
    [HttpDelete("categories/{id}")]
    [SessionAuth(Roles.Organizer)]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Delete(string id)
    {
        var cat = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (cat == null) return NotFound(new { message = "Category not found." });
        if (cat.Event!.OrganizerId != UserId) return Forbidden("You can only delete categories on your own events.");
        if (await _db.Enrollments.AnyAsync(e => e.CategoryId == id))
            return Conflict(new { message = "People are enrolled in this category, so it cannot be deleted." });

        _db.Categories.Remove(cat);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}