using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Infrastructure.Data.Seeding;
using Testcontainers.PostgreSql;

namespace TaskFlow.IntegrationTests.Infrastructure;

/// <summary>
/// Runs the real API in memory against a throw-away PostgreSQL 17 container (never the development database).
/// The environment is Development, so Program.cs applies the migrations and inserts the seed data on start.
/// One instance is shared by every test class in the <see cref="ApiCollection"/>.
/// </summary>
public sealed class TaskFlowApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Test-only signing key (at least 32 bytes); never used outside this process.
    private const string TestJwtSecret = "integration-tests-only-signing-key-0123456789abcdef";

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("taskflow_tests")
        .WithUsername("taskflow")
        .WithPassword("taskflow-tests")
        .Build();

    public string ConnectionString => _database.GetConnectionString();

    /// <summary>Every email the API "sent" (the SMTP queue is replaced by this fake).</summary>
    public FakeEmailService Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // UseSetting values reach Program.cs as command-line style settings, so they win over user-secrets and
        // appsettings even for values Program.cs reads before Build() (the connection string and the JWT key).
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = ConnectionString,
            ["JwtSettings:Secret"] = TestJwtSecret,
            ["Seed:ResetDatabase"] = "false",
            ["Seed:RunSmokeTestAndExit"] = "false",
            // Tests run the reminder logic themselves; a background run would add notifications at random moments.
            ["Reminders:Enabled"] = "false"
        };
        foreach (var (key, value) in settings)
            builder.UseSetting(key, value);

        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(settings));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(Emails);
        });
    }

    public async Task InitializeAsync() => await _database.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }

    /// <summary>An anonymous client.</summary>
    public HttpClient CreateAnonymousClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });

    /// <summary>Logs in a seeded (or freshly registered) user and returns a client that sends their token.</summary>
    public async Task<HttpClient> LoginAsync(string email, string password = DataSeeder.DefaultPassword)
    {
        var client = CreateAnonymousClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResult<LoginData>>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Data!.Token);
        return client;
    }

    /// <summary>Registers a fresh account (role User) and logs it in, for tests that need users nobody else touches.</summary>
    public async Task<TestUser> RegisterAsync(string firstName, string lastName = "Tester")
    {
        const string password = TestUser.Password;
        var email = $"test.{Guid.NewGuid():N}@taskflow.test";
        var anonymous = CreateAnonymousClient();
        var registered = await anonymous.PostAsJsonAsync("/api/auth/register", new { firstName, lastName, email, password });
        registered.EnsureSuccessStatusCode();

        var login = await anonymous.PostAsJsonAsync("/api/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        var data = (await login.Content.ReadFromJsonAsync<ApiResult<LoginData>>())!.Data!;

        var client = CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.Token);
        return new TestUser(data.User.Id, $"{firstName} {lastName}", data.Token, client, email);
    }
}

/// <summary>A registered test account: its id, full name, JWT, a client that sends the JWT, and its email
/// (the password is <see cref="Password"/>).</summary>
public sealed record TestUser(Guid Id, string FullName, string Token, HttpClient Client, string Email)
{
    public const string Password = "Test-P@ssw0rd";
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<TaskFlowApiFactory>
{
    public const string Name = "TaskFlow API";
}

/// <summary>The success body shape: <c>{ isSuccess, data }</c>.</summary>
public sealed record ApiResult<T>(bool IsSuccess, T? Data);

public sealed record LoginData(string Token, UserData User, DateTime ExpiresAt);

public sealed record UserData(Guid Id, string FirstName, string LastName, string Email, string Role);

/// <summary>Seeded accounts (see DataSeeder and docs/DATABASE.md).</summary>
public static class SeedUsers
{
    public const string Admin = "admin@taskflow.dev";
    public const string Manager = "manager@taskflow.dev";
    public const string User = "user@taskflow.dev";
    public const string User2 = "user2@taskflow.dev";
    public const string Inactive = "inactive@taskflow.dev";
}
