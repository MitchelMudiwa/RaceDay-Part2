using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ApiBase
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher<Account> _hasher = new();

    public AuthController(AppDbContext db) { _db = db; }

    /// <summary>Register a new account as an Organizer or a Participant. The password is hashed before saving.</summary>
    /// <remarks>Role must be "Organizer" or "Participant". Password must be at least 8 characters.</remarks>
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserResponse), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        string? role = null;
        if (string.Equals(req.Role?.Trim(), Roles.Organizer, StringComparison.OrdinalIgnoreCase)) role = Roles.Organizer;
        if (string.Equals(req.Role?.Trim(), Roles.Participant, StringComparison.OrdinalIgnoreCase)) role = Roles.Participant;
        if (role == null) return BadRequest(new { message = "Role must be 'Organizer' or 'Participant'." });

        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        if (!email.Contains('@')) return BadRequest(new { message = "Please enter a valid email." });
        if ((req.Password ?? "").Length < 8) return BadRequest(new { message = "Password must be at least 8 characters." });
        if (string.IsNullOrWhiteSpace(req.FirstName) || string.IsNullOrWhiteSpace(req.LastName))
            return BadRequest(new { message = "First name and last name are required." });

        if (await _db.Accounts.AnyAsync(a => a.Email == email))
            return Conflict(new { message = "An account with this email already exists." });

        var account = new Account
        {
            AccountId = Ids.New("ACC"),
            FirstName = req.FirstName.Trim(),
            LastName = req.LastName.Trim(),
            Email = email,
            PhoneNumber = req.PhoneNumber,
            Role = role
        };
        account.PasswordHash = _hasher.HashPassword(account, req.Password!);

        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();
        return StatusCode(201, UserResponse.From(account));
    }

    /// <summary>Log in with email and password. Starts a session that remembers your user and role.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(UserResponse), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Email == email);

        if (account == null ||
            _hasher.VerifyHashedPassword(account, account.PasswordHash, req.Password ?? "") == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Wrong email or password." });

        HttpContext.Session.Clear();
        HttpContext.Session.SetString(SessionKeys.AccountId, account.AccountId);
        HttpContext.Session.SetString(SessionKeys.Role, account.Role);
        return Ok(UserResponse.From(account));
    }

    /// <summary>Log out and end the session.</summary>
    [HttpPost("logout")]
    [SessionAuth]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return NoContent();
    }
}