# RaceDay API (Part 2)

## What it is
A RESTful ASP.NET Core Web API for running race events. Organizers create events and categories and capture results. Participants enrol in events and view their results.

## The two roles
- **Organizer:** create, update and delete their own events and categories; view enrolments for their events; capture and correct results.
- **Participant:** view events and categories; enrol in an event under a category; view and cancel their own enrolments; view their own results.

Both roles can register, log in, log out, and view or update their own profile.

## How login works
Session-based. After `POST /api/auth/login` the server stores the user ID and role in the session and sends a session cookie. Passwords are hashed (ASP.NET `PasswordHasher`) and never stored in plain text.

## Setup and run
1. Install Visual Studio, the .NET 10 SDK and SQL Server LocalDB.
2. Clone the repo and open the solution.
3. In `appsettings.json`, check `ConnectionStrings:DefaultConnection` points to your SQL Server.
4. In Package Manager Console run: `Update-Database`
5. Run the project using the **https** profile. Swagger opens at `/swagger`.
6. Run the tests with `dotnet test` or Test Explorer.

## Differences from the Part 1 plan
- Login uses a session cookie, not a token.
- Register takes `firstName` and `lastName` (matches the ERD) plus `password`.
- Profile route is `/api/users/me`; "my enrolments" is `/api/enrolments/me`.
- Added `POST /api/auth/logout`.
- The Route table has no endpoints in this part.
- EF Core creates PascalCase column names (e.g. `FirstName`) instead of `First_Name`. The Part 2 database is built code-first and is separate from the Part 1 SQL script.
- Event type is limited to run, walk or cycle (the seed data's "Marathon" is not accepted).

## CI/CD
![Green build](docs/green-build.png)

