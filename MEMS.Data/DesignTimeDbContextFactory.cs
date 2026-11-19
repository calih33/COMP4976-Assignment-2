using MEMS.Data.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MEMS.Data;

/// <summary>
/// Used only by <c>dotnet ef</c> to build the model for migrations. At runtime the connection string comes
/// from Aspire; this placeholder is never connected to.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=memsdb;Trusted_Connection=False;Encrypt=False")
            .Options;

        var tenant = new TenantContext();
        tenant.SetSystem();
        return new AppDbContext(options, tenant);
    }
}
