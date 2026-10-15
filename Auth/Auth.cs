using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RaceDay.Api.Auth;

public static class SessionKeys
{
    public const string AccountId = "AccountId";
    public const string Role = "Role";
}

public static class Ids
{
    // e.g. EVT3F9A21BC (fits the text IDs used in the ERD)
    public static string New(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8].ToUpper();
}

/// <summary>
/// Checks the session. No session = 401. Wrong role = 403.
/// Use [SessionAuth] for any logged-in user, or [SessionAuth("Organizer")] for one role.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class SessionAuthAttribute : Attribute, IActionFilter
{
    private readonly string[] _roles;
    public SessionAuthAttribute(params string[] roles) { _roles = roles; }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        var session = context.HttpContext.Session;
        var id = session.GetString(SessionKeys.AccountId);
        var role = session.GetString(SessionKeys.Role);

        if (id == null)
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Please log in first." });
            return;
        }
        if (_roles.Length > 0 && (role == null || !_roles.Contains(role)))
        {
            context.Result = new ObjectResult(new { message = "Your role is not allowed to do this." }) { StatusCode = 403 };
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}

public abstract class ApiBase : ControllerBase
{
    protected string UserId => HttpContext.Session.GetString(SessionKeys.AccountId) ?? "";
    protected string UserRole => HttpContext.Session.GetString(SessionKeys.Role) ?? "";
    protected IActionResult Forbidden(string message) => StatusCode(403, new { message });
}