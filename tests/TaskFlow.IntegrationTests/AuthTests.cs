using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Infrastructure.Data.Context;
using TaskFlow.IntegrationTests.Infrastructure;

namespace TaskFlow.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AuthTests(TaskFlowApiFactory factory)
{
    [Fact]
    public void Api_uses_the_test_container_database()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Equal(factory.ConnectionString, db.Database.GetConnectionString());
        Assert.Contains("taskflow_tests", db.Database.GetConnectionString());
    }

    [Theory]
    [InlineData(SeedUsers.Admin, "Admin")]
    [InlineData(SeedUsers.Manager, "Manager")]
    [InlineData(SeedUsers.User, "User")]
    public async Task Login_returns_a_token_and_the_role(string email, string role)
    {
        var client = factory.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email, password = TaskFlow.Infrastructure.Data.Seeding.DataSeeder.DefaultPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<LoginData>>();
        Assert.True(body!.IsSuccess);
        Assert.False(string.IsNullOrEmpty(body.Data!.Token));
        Assert.Equal(email, body.Data.User.Email);
        Assert.Equal(role, body.Data.User.Role);
    }

    [Fact]
    public async Task Login_of_an_inactive_user_is_401_with_the_generic_message()
    {
        var client = factory.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = SeedUsers.Inactive, password = TaskFlow.Infrastructure.Data.Seeding.DataSeeder.DefaultPassword });

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Unauthorized", "Invalid email or password");
    }

    [Fact]
    public async Task Login_with_a_wrong_password_is_401_with_the_same_message()
    {
        var client = factory.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = SeedUsers.User, password = "not-the-password" });

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Unauthorized", "Invalid email or password");
    }

    [Fact]
    public async Task Registering_an_existing_email_is_409()
    {
        var client = factory.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            firstName = "Copy",
            lastName = "Cat",
            email = SeedUsers.User.ToUpperInvariant(),
            password = "Secret123!"
        });

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "Conflict", "Email already registered");
    }

    [Fact]
    public async Task Change_password_with_a_wrong_current_password_is_400()
    {
        var client = await factory.LoginAsync(SeedUsers.User2);

        var response = await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "wrong-password", newPassword = "Another123!" });

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "Validation", "Current password is incorrect.");
    }

    [Fact]
    public async Task Request_without_a_token_is_401_problem_details()
    {
        var client = factory.CreateAnonymousClient();

        var response = await client.GetAsync("/api/users/me");

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized);
    }
}
