using MEMS.Models;
using Microsoft.AspNetCore.Identity;

namespace MEMS.Data;

/// <summary>
/// Admin account. Municipality Admins belong to exactly one tenant; Super Admins have no tenant.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public int? MunicipalityId { get; set; }

    public Municipality? Municipality { get; set; }
}
