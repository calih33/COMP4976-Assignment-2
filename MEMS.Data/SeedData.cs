using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MEMS.Models;
using Microsoft.EntityFrameworkCore;

namespace MEMS.Data;

public static class SeedData
{
    static readonly DateTime election = new(2026, 10, 17);
    static readonly DateTime created = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    static readonly string[] priorities =
    {
        "affordable housing and responsible growth",
        "safer streets and better transit connections",
        "protecting parks, farmland and green space",
        "keeping property taxes predictable",
        "support for local businesses and jobs",
        "inclusive, well-funded public schools",
        "climate resilience and flood protection",
        "transparent, accessible local government",
    };

    public static async Task SeedMunicipalitiesAsync(ApplicationDbContext db)
    {
        db.CurrentTenantId = null;

        if (await db.Municipalities!.AnyAsync())
        {
            return;
        }

        db.Municipalities!.AddRange(
            BuildMunicipality("Burnaby", "burnaby",
                "One of the largest cities in Metro Vancouver, home to Metrotown, Burnaby Mountain and Simon Fraser University.",
                new()
                {
                    ["Mayor"] = (1, new[] { "Michael Leung", "Andrea Kovacs" }),
                    ["Councillor"] = (8, new[] { "Ravi Dhillon", "Caroline Price", "Ethan Wong", "Isabel Moreno" }),
                    ["School Trustee"] = (7, new[] { "Monica Ferreira", "Jordan Blake", "Wei Zhang" }),
                }),
            BuildMunicipality("Surrey", "surrey",
                "A fast-growing city south of the Fraser River with several town centres and a young, diverse population.",
                new()
                {
                    ["Mayor"] = (1, new[] { "Eleanor Vasquez", "Graham Whitfield" }),
                    ["Councillor"] = (8, new[] { "Priya Sandhu", "Marcus Bell", "Hannah O'Connor", "Daniel Okafor",
                                                 "Sophie Tremblay", "Kevin Nakamura", "Rachel Lindqvist", "Omar Haddad" }),
                    ["School Trustee"] = (6, new[] { "Laura Chen", "Brian McAllister", "Fatima Rahimi" }),
                }),
            BuildMunicipality("Richmond", "richmond",
                "An island city known for its dyke trails, summer night market and location next to the airport.",
                new()
                {
                    ["Mayor"] = (1, new[] { "Thomas Archer", "Natalie Dubois" }),
                    ["Councillor"] = (8, new[] { "Jasmine Gill", "Peter Kowalski", "Aiden Murphy", "Leah Thompson", "Victor Silva" }),
                    ["School Trustee"] = (7, new[] { "Grace Fontaine", "Samuel Park", "Irene Novak" }),
                }));

        await db.SaveChangesAsync();
    }

    static Municipality BuildMunicipality(
        string name,
        string slug,
        string description,
        Dictionary<string, (int Seats, string[] Candidates)> positions)
    {
        var municipality = new Municipality
        {
            Name = name,
            Slug = slug,
            Province = "British Columbia",
            ElectionDate = election,
            Description = description,
        };

        var priorityIndex = name.Length;
        foreach (var (positionName, (seats, candidateNames)) in positions)
        {
            var position = new Position { Name = positionName, SeatsAvailable = seats };

            foreach (var candidateName in candidateNames)
            {
                var priority = priorities[priorityIndex++ % priorities.Length];
                var handle = candidateName.ToLowerInvariant().Replace("'", "").Replace(' ', '.');

                position.Candidates.Add(new Candidate
                {
                    Name = candidateName,
                    Profile = $"{candidateName} is running for {positionName} in {name}. A long-time resident and community " +
                              $"volunteer, {candidateName.Split(' ')[0]} is campaigning on {priority}, and believes local " +
                              "decisions should be made in the open with residents at the table.",
                    Email = $"{handle}@example.com",
                    Website = priorityIndex % 2 == 0 ? $"https://example.com/{handle.Replace('.', '-')}" : null,
                    DateCreated = created,
                    Municipality = municipality,
                });
            }

            municipality.Positions.Add(position);
        }

        return municipality;
    }
}
