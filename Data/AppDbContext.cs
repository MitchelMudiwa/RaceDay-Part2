using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventRoute> Routes => Set<EventRoute>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Table names match the ERD
        b.Entity<Account>().ToTable("Account");
        b.Entity<Event>().ToTable("Event");
        b.Entity<EventRoute>().ToTable("Route");
        b.Entity<Category>().ToTable("Category");
        b.Entity<Enrollment>().ToTable("Enrollment");
        b.Entity<Result>().ToTable("Results");

        // Email must be unique
        b.Entity<Account>().HasIndex(a => a.Email).IsUnique();

        // Account 1 - many Event (organiser)
        b.Entity<Event>()
            .HasOne(e => e.Organizer).WithMany(a => a.Events)
            .HasForeignKey(e => e.OrganizerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Event 1 - 0..1 Route
        b.Entity<EventRoute>()
            .HasOne(r => r.Event).WithOne(e => e.Route)
            .HasForeignKey<EventRoute>(r => r.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        // Event 1 - many Category
        b.Entity<Category>()
            .HasOne(c => c.Event).WithMany(e => e.Categories)
            .HasForeignKey(c => c.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        // Enrollment links Participant, Event and Category
        // Restrict avoids SQL Server "multiple cascade paths" errors
        b.Entity<Enrollment>()
            .HasOne(en => en.Participant).WithMany(a => a.Enrollments)
            .HasForeignKey(en => en.ParticipantId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<Enrollment>()
            .HasOne(en => en.Event).WithMany(e => e.Enrollments)
            .HasForeignKey(en => en.EventId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<Enrollment>()
            .HasOne(en => en.Category).WithMany(c => c.Enrollments)
            .HasForeignKey(en => en.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // A participant can only enrol once per event
        b.Entity<Enrollment>()
            .HasIndex(en => new { en.ParticipantId, en.EventId }).IsUnique();

        // Enrollment 1 - 0..1 Result
        b.Entity<Result>()
            .HasOne(r => r.Enrollment).WithOne(en => en.Result)
            .HasForeignKey<Result>(r => r.EnrollmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}