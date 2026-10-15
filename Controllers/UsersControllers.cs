using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/users")]
[SessionAuth]
public class UsersController : ApiBase
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher<Account> _hasher = new();

    public UsersController(AppDbContext db) { _db = db; }

    /// <summary>View your own profile (Organizer or Participant).</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserResponse), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetMe()
    {
        var account = await _db.Accounts.FindAsync(UserId);
        if (account == null) return NotFound();
        return Ok(UserResponse.From(account));
    }

    /// <summary>Update your own profile. Only the fields you send are changed.</summary>
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> UpdateMe(UpdateProfileRequest req)
    {
        var account = await _db.Accounts.FindAsync(UserId);
        if (account == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(req.FirstName)) account.FirstName = req.FirstName.Trim();
        if (!string.IsNullOrWhiteSpace(req.LastName)) account.LastName = req.LastName.Trim();
        if (req.PhoneNumber != null) account.PhoneNumber = req.PhoneNumber;

        if (!string.IsNullOrWhiteSpace(req.Email))
        {
            var email = req.Email.Trim().ToLowerInvariant();
            if (!email.Contains('@')) return BadRequest(new { message = "Please enter a valid email." });
            if (await _db.Accounts.AnyAsync(a => a.Email == email && a.AccountId != account.AccountId))
                return Conflict(new { message = "That email is already used." });
            account.Email = email;
        }

        if (!string.IsNullOrEmpty(req.Password))
        {
            if (req.Password.Length < 8) return BadRequest(new { message = "Password must be at least 8 characters." });
            account.PasswordHash = _hasher.HashPassword(account, req.Password);
        }

        account.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(UserResponse.From(account));
    }
}