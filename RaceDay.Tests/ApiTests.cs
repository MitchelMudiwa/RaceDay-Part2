using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RaceDay.Tests;

public class ApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public ApiTests(ApiFactory factory) { _factory = factory; }

    // ---------- helpers ----------
    private HttpClient NewClient() => _factory.CreateClient(
        new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private static string NewEmail() => $"{Guid.NewGuid():N}@test.com";

    private static async Task<string> Prop(HttpResponseMessage r, string name)
    {
        var json = await r.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty(name).GetString()!;
    }

    // Registers and logs in. The client keeps the session cookie.
    private async Task<(HttpClient Client, string Id)> LoggedIn(string role)
    {
        var client = NewClient();
        var email = NewEmail();
        var reg = await client.PostAsJsonAsync("/api/auth/register",
            new { firstName = "Test", lastName = role, email, password = "Password123", role });
        reg.EnsureSuccessStatusCode();
        var id = await Prop(reg, "id");
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password123" });
        login.EnsureSuccessStatusCode();
        return (client, id);
    }

    private static async Task<string> MakeEvent(HttpClient organizer)
    {
        var r = await organizer.PostAsJsonAsync("/api/events", new
        {
            name = "Test 10K",
            description = "A test race",
            eventDate = DateTime.UtcNow.AddDays(30),
            location = "Cape Town",
            distance = "10km",
            eventType = "run"
        });
        r.EnsureSuccessStatusCode();
        return await Prop(r, "id");
    }

    private static async Task<string> MakeCategory(HttpClient organizer, string eventId)
    {
        var r = await organizer.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new { name = "Senior", categoryType = "Age", minAge = 18, maxAge = 40, distance = "10km" });
        r.EnsureSuccessStatusCode();
        return await Prop(r, "id");
    }

    // ---------- registration and login ----------
    [Fact]
    public async Task Register_ValidDetails_Returns201()
    {
        var r = await NewClient().PostAsJsonAsync("/api/auth/register",
            new { firstName = "Ann", lastName = "Lee", email = NewEmail(), password = "Password123", role = "Participant" });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = NewClient();
        var body = new { firstName = "Ann", lastName = "Lee", email = NewEmail(), password = "Password123", role = "Participant" };
        await client.PostAsJsonAsync("/api/auth/register", body);
        var again = await client.PostAsJsonAsync("/api/auth/register", body);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidRole_Returns400()
    {
        var r = await NewClient().PostAsJsonAsync("/api/auth/register",
            new { firstName = "Ann", lastName = "Lee", email = NewEmail(), password = "Password123", role = "Admin" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Login_CorrectPassword_Returns200()
    {
        var (client, _) = await LoggedIn("Participant");
        var me = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = NewClient();
        var email = NewEmail();
        await client.PostAsJsonAsync("/api/auth/register",
            new { firstName = "Ann", lastName = "Lee", email, password = "Password123", role = "Participant" });
        var r = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPassword" });
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    // ---------- not logged in ----------
    [Fact]
    public async Task Profile_NotLoggedIn_Returns401()
    {
        var r = await NewClient().GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Events_NotLoggedIn_Returns401()
    {
        var r = await NewClient().GetAsync("/api/events");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsSession()
    {
        var (client, _) = await LoggedIn("Participant");
        await client.PostAsync("/api/auth/logout", null);
        var r = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    // ---------- event management ----------
    [Fact]
    public async Task Organizer_CanCreateEvent_Returns201()
    {
        var (org, _) = await LoggedIn("Organizer");
        var r = await org.PostAsJsonAsync("/api/events", new
        {
            name = "Night Run",
            eventDate = DateTime.UtcNow.AddDays(10),
            location = "Durban",
            distance = "5km",
            eventType = "run"
        });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
    }

    [Fact]
    public async Task Organizer_CreateEvent_BadType_Returns400()
    {
        var (org, _) = await LoggedIn("Organizer");
        var r = await org.PostAsJsonAsync("/api/events", new
        {
            name = "Swim",
            eventDate = DateTime.UtcNow.AddDays(10),
            location = "Durban",
            distance = "1km",
            eventType = "swim"
        });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Organizer_CanUpdateOwnEvent_Returns200()
    {
        var (org, _) = await LoggedIn("Organizer");
        var eventId = await MakeEvent(org);
        var r = await org.PutAsJsonAsync($"/api/events/{eventId}", new { name = "Renamed Race" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("Renamed Race", await Prop(r, "name"));
    }

    [Fact]
    public async Task Organizer_CannotUpdateAnotherOrganizersEvent_Returns403()
    {
        var (owner, _) = await LoggedIn("Organizer");
        var (other, _) = await LoggedIn("Organizer");
        var eventId = await MakeEvent(owner);
        var r = await other.PutAsJsonAsync($"/api/events/{eventId}", new { name = "Hijacked" });
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task Organizer_CanDeleteOwnEvent_Returns204()
    {
        var (org, _) = await LoggedIn("Organizer");
        var eventId = await MakeEvent(org);
        var r = await org.DeleteAsync($"/api/events/{eventId}");
        Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
    }

    [Fact]
    public async Task Participant_CanViewEvents_Returns200()
    {
        var (org, _) = await LoggedIn("Organizer");
        await MakeEvent(org);
        var (part, _) = await LoggedIn("Participant");
        var r = await part.GetAsync("/api/events");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    // ---------- roles are enforced ----------
    [Fact]
    public async Task Participant_CannotCreateEvent_Returns403()
    {
        var (part, _) = await LoggedIn("Participant");
        var r = await part.PostAsJsonAsync("/api/events", new
        {
            name = "Nope",
            eventDate = DateTime.UtcNow.AddDays(10),
            location = "X",
            distance = "5km",
            eventType = "run"
        });
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCreateCategory_Returns403()
    {
        var (org, _) = await LoggedIn("Organizer");
        var eventId = await MakeEvent(org);
        var (part, _) = await LoggedIn("Participant");
        var r = await part.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new { name = "Hack", minAge = 1, maxAge = 99 });
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task Organizer_CannotEnrol_Returns403()
    {
        var (org, _) = await LoggedIn("Organizer");
        var eventId = await MakeEvent(org);
        var catId = await MakeCategory(org, eventId);
        var r = await org.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId = catId });
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCaptureResults_Returns403()
    {
        var (org, _) = await LoggedIn("Organizer");
        var eventId = await MakeEvent(org);
        var (part, partId) = await LoggedIn("Participant");
        var r = await part.PostAsJsonAsync($"/api/events/{eventId}/results",
            new { results = new[] { new { participantId = partId, finishTime = "00:40:00", position = 1 } } });
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    // ---------- enrolments ----------
    [Fact]
    public async Task Participant_CanEnrol_AndItIsRecorded()
    {
        var (org, _) = await LoggedIn("Organizer");
        var eventId = await MakeEvent(org);
        var catId = await MakeCategory(org, eventId);
        var (part, partId) = await LoggedIn("Participant");

        var r = await part.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId = catId });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);

        // Read the response only once
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(partId, body.GetProperty("participantId").GetString());
        Assert.Equal(catId, body.GetProperty("categoryId").GetString());

        var mine = await part.GetFromJsonAsync<JsonElement>("/api/enrolments/me");
        Assert.Equal(1, mine.GetArrayLength());
    }

    [Fact]
    public async Task Participant_EnrolTwice_Returns409()
    {
        var (org, _) = await LoggedIn("Organizer");
        var eventId = await MakeEvent(org);
        var catId = await MakeCategory(org, eventId);
        var (part, _) = await LoggedIn("Participant");
        await part.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId = catId });
        var again = await part.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId = catId });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Participant_EnrolWithCategoryFromAnotherEvent_Returns404()
    {
        var (org, _) = await LoggedIn("Organizer");
        var eventA = await MakeEvent(org);
        var eventB = await MakeEvent(org);
        var catB = await MakeCategory(org, eventB);
        var (part, _) = await LoggedIn("Participant");
        var r = await part.PostAsJsonAsync($"/api/events/{eventA}/enrolments", new { categoryId = catB });
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    [Fact]
    public async Task Organizer_CanViewEnrolmentsForOwnEvent()
    {
        var (org, _) = await LoggedIn("Organizer");
        var eventId = await MakeEvent(org);
        var catId = await MakeCategory(org, eventId);
        var (part, _) = await LoggedIn("Participant");
        await part.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId = catId });

        var r = await org.GetAsync($"/api/events/{eventId}/enrolments");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(1, (await r.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength());
    }

    [Fact]
    public async Task Participant_CannotViewEventEnrolments_Returns403()
    {
        var (org, _) = await LoggedIn("Organizer");
        var eventId = await MakeEvent(org);
        var (part, _) = await LoggedIn("Participant");
        var r = await part.GetAsync($"/api/events/{eventId}/enrolments");
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    // ---------- results ----------
    [Fact]
    public async Task Organizer_CapturesResults_ParticipantSeesOwn()
    {
        var (org, _) = await LoggedIn("Organizer");
        var eventId = await MakeEvent(org);
        var catId = await MakeCategory(org, eventId);
        var (part, partId) = await LoggedIn("Participant");
        await part.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId = catId });

        var capture = await org.PostAsJsonAsync($"/api/events/{eventId}/results",
            new { results = new[] { new { participantId = partId, finishTime = "00:42:15", position = 1 } } });
        Assert.Equal(HttpStatusCode.Created, capture.StatusCode);

        var mine = await part.GetFromJsonAsync<JsonElement>("/api/results/me");
        Assert.Equal(1, mine.GetArrayLength());
        Assert.Equal("00:42:15", mine[0].GetProperty("finishTime").GetString());
    }

    [Fact]
    public async Task Organizer_CaptureResults_ForNonEnrolled_Returns400()
    {
        var (org, _) = await LoggedIn("Organizer");
        var eventId = await MakeEvent(org);
        var (_, strangerId) = await LoggedIn("Participant");
        var r = await org.PostAsJsonAsync($"/api/events/{eventId}/results",
            new { results = new[] { new { participantId = strangerId, finishTime = "00:42:15", position = 1 } } });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }
}