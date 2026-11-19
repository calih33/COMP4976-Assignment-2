using System.ComponentModel.DataAnnotations;

namespace MEMS.Models;

public class Position
{
    public int PositionId { get; set; }

    [Required]
    [StringLength(50)]
    [Display(Name = "Position")]
    public string Name { get; set; } = string.Empty;

    [Range(1, 50)]
    [Display(Name = "Seats Available")]
    public int SeatsAvailable { get; set; } = 1;

    // Tenant key. For Municipality Admins the service sets it from the tenant context, never from the form.
    [Range(1, int.MaxValue, ErrorMessage = "The Municipality field is required.")]
    [Display(Name = "Municipality")]
    public int MunicipalityId { get; set; }

    public Municipality? Municipality { get; set; }

    public ICollection<Candidate> Candidates { get; set; } = new List<Candidate>();
}
