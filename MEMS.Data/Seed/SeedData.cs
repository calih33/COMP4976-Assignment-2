using MEMS.Data.Tenancy;
using MEMS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MEMS.Data.Seed;

/// <summary>
/// Applies migrations, then seeds tenants, positions, candidates, roles and admin users.
/// Idempotent: every tenant is looked up by slug and every user by user name, so it is safe on every startup
/// and adds anything missing to an existing database.
/// </summary>
public static class SeedData
{
    // Seeded accounts only (assignment requirement). Never reused for real users.
    public const string SeedPassword = "P@$$w0rd";

    private static readonly DateOnly ElectionDate = new(2026, 10, 17);
    private static readonly DateTime SeedCreated = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    private sealed record TenantSeed(
        string Name,
        string Slug,
        string ThemeColor,
        string Description,
        string AdminUserName,
        Dictionary<string, (int Seats, string[] Candidates)> Positions);

    private static readonly TenantSeed[] Tenants =
    [
        new("Burnaby", "burnaby", "#1d4e89",
            "One of the largest cities in Metro Vancouver, home to Metrotown, Burnaby Mountain and Simon Fraser University.",
            "burnabyadmin",
            new()
            {
                ["Mayor"] = (1, ["Michael Leung", "Andrea Kovacs"]),
                ["Councillor"] = (8, ["Ravi Dhillon", "Caroline Price", "Ethan Wong", "Isabel Moreno"]),
                ["School Trustee"] = (7, ["Monica Ferreira", "Jordan Blake", "Wei Zhang"]),
            }),
        new("Surrey", "surrey", "#00754a",
            "A fast-growing city of town centres, farmland and parks south of the Fraser River.",
            "surreyadmin",
            new()
            {
                ["Mayor"] = (1, ["Thomas Archer", "Natalie Dubois"]),
                ["Councillor"] = (8, ["Jasmine Gill", "Peter Kowalski", "Aiden Murphy", "Leah Thompson", "Victor Silva"]),
                ["School Trustee"] = (6, ["Grace Fontaine", "Samuel Park", "Irene Novak"]),
            }),
        new("Richmond", "richmond", "#7b2d8b",
            "An island city at the mouth of the Fraser River, known for Steveston village, its dikes and its night markets.",
            "richmondadmin",
            new()
            {
                ["Mayor"] = (1, ["Helen Tsang", "Arjun Mehta"]),
                ["Councillor"] = (8, ["Olivia Grant", "Benjamin Cho", "Nadia Petrova", "Lucas Fernandes"]),
                ["School Trustee"] = (7, ["Emily Watson", "Hiroshi Tanaka", "Zara Ahmed"]),
            }),
        // Kept from Assignment 1 so the required "pm" user still has its municipality.
        new("Pitt Meadows", "pitt-meadows", "#8a5a00",
            "A small city on the north bank of the Fraser River, known for its farmland, dikes and regional airport.",
            "pm",
            new()
            {
                ["Mayor"] = (1, ["Eleanor Vasquez", "Graham Whitfield"]),
                ["Councillor"] = (6, ["Priya Sandhu", "Marcus Bell", "Hannah O'Connor", "Daniel Okafor",
                                      "Sophie Tremblay", "Kevin Nakamura", "Rachel Lindqvist", "Omar Haddad"]),
                ["School Trustee"] = (2, ["Laura Chen", "Brian McAllister", "Fatima Rahimi"]),
            }),
    ];

    private static readonly string[] Priorities =
    [
        "affordable housing and responsible growth",
        "safer streets and better transit connections",
        "protecting parks, farmland and green space",
        "keeping property taxes predictable",
        "support for local businesses and jobs",
        "inclusive, well-funded public schools",
        "climate resilience and flood protection",
        "transparent, accessible local government",
    ];

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        // Seeding works across all tenants, so it runs with system access.
        provider.GetRequiredService<TenantContext>().SetSystem();

        var db = provider.GetRequiredService<AppDbContext>();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SeedData));

        await db.Database.MigrateAsync(ct);

        foreach (var seed in Tenants)
        {
            await SeedTenantAsync(db, seed, logger, ct);
        }

        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { AppRoles.SuperAdmin, AppRoles.MunicipalityAdmin })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        await EnsureUserAsync(userManager, "super", AppRoles.SuperAdmin, municipalityId: null, logger);

        foreach (var seed in Tenants)
        {
            var municipalityId = await db.Municipalities.Where(m => m.Slug == seed.Slug).Select(m => m.MunicipalityId).SingleAsync(ct);
            await EnsureUserAsync(userManager, seed.AdminUserName, AppRoles.MunicipalityAdmin, municipalityId, logger);
        }
    }

    private static async Task SeedTenantAsync(AppDbContext db, TenantSeed seed, ILogger logger, CancellationToken ct)
    {
        if (await db.Municipalities.AnyAsync(m => m.Slug == seed.Slug, ct))
        {
            return;
        }

        var municipality = new Municipality
        {
            Name = seed.Name,
            Slug = seed.Slug,
            Province = "British Columbia",
            ElectionDate = ElectionDate,
            Description = seed.Description,
            ThemeColor = seed.ThemeColor,
        };

        var priorityIndex = seed.Name.Length;
        foreach (var (positionName, (seats, candidateNames)) in seed.Positions)
        {
            var position = new Position { Name = positionName, SeatsAvailable = seats, Municipality = municipality };

            foreach (var candidateName in candidateNames)
            {
                var priority = Priorities[priorityIndex++ % Priorities.Length];
                var handle = candidateName.ToLowerInvariant().Replace("'", "").Replace(' ', '.');

                position.Candidates.Add(new Candidate
                {
                    Name = candidateName,
                    Municipality = municipality,
                    Profile = $"{candidateName} is running for {positionName} in {seed.Name}. A long-time resident and community " +
                              $"volunteer, {candidateName.Split(' ')[0]} is campaigning on {priority}, and believes local " +
                              "decisions should be made in the open with residents at the table.",
                    Email = $"{handle}@example.com",
                    Website = priorityIndex % 2 == 0 ? $"https://example.com/{handle.Replace('.', '-')}" : null,
                    DateCreated = SeedCreated,
                });
            }

            municipality.Positions.Add(position);
        }

        db.Municipalities.Add(municipality);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeded tenant {Slug} with {PositionCount} positions and {CandidateCount} candidates",
            seed.Slug, municipality.Positions.Count, municipality.Positions.Sum(p => p.Candidates.Count));
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager, string userName, string role, int? municipalityId, ILogger logger)
    {
        var user = await userManager.FindByNameAsync(userName);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = userName,
                Email = $"{userName}@elections.local",
                EmailConfirmed = true,
                MunicipalityId = municipalityId,
            };

            var result = await userManager.CreateAsync(user, SeedPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not seed user '{userName}': {string.Join("; ", result.Errors.Select(e => e.Description))}");
            }

            logger.LogInformation("Seeded user {UserName} ({Role})", userName, role);
        }
        else if (user.MunicipalityId != municipalityId)
        {
            user.MunicipalityId = municipalityId;
            await userManager.UpdateAsync(user);
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }
}
