using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TaskFlow.Infrastructure.Data.Context;

/// <summary>
/// Used only by the <c>dotnet ef</c> tools. It builds the context without starting the API host, so creating
/// a migration or a SQL script needs neither the JWT secret nor a running database.
/// <c>dotnet ef database update</c> does connect: set TASKFLOW_DB_CONNECTION to a full connection string
/// (including the password) or pass <c>--connection</c>.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    private const string DefaultConnection = "Host=localhost;Port=5433;Database=taskflow;Username=taskflow";

    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("TASKFLOW_DB_CONNECTION") ?? DefaultConnection;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        ApplicationDbContext.Configure(options, connectionString);
        return new ApplicationDbContext(options.Options);
    }
}
