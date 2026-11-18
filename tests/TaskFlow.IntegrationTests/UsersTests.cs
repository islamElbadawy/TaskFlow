using System.Net;
using System.Net.Http.Json;
using TaskFlow.Infrastructure.Data.Seeding;
using TaskFlow.IntegrationTests.Infrastructure;

namespace TaskFlow.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class UsersTests(TaskFlowApiFactory factory)
{
    [Fact]
    public async Task A_user_cannot_update_an_admin_403()
    {
        var client = await factory.LoginAsync(SeedUsers.User);

        var response = await client.PutAsJsonAsync($"/api/users/{DataSeeder.UserIds.Admin}",
            new { firstName = "Hacked", lastName = "Admin", email = SeedUsers.Admin });

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Forbidden");
    }

    [Fact]
    public async Task Unknown_user_is_404_problem_details()
    {
        var client = await factory.LoginAsync(SeedUsers.Admin);

        var response = await client.GetAsync($"/api/users/{Guid.NewGuid()}");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "NotFound", "User not found");
    }

    [Fact]
    public async Task Demoting_the_last_admin_is_409()
    {
        var client = await factory.LoginAsync(SeedUsers.Admin);

        var response = await client.PutAsJsonAsync($"/api/users/{DataSeeder.UserIds.Admin}/role",
            new { newRole = "User" });

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "Conflict", "Cannot demote the last active Admin");
    }

    [Fact]
    public async Task Deleting_a_user_who_created_tasks_is_409()
    {
        var client = await factory.LoginAsync(SeedUsers.Admin);

        var response = await client.DeleteAsync($"/api/users/{DataSeeder.UserIds.Manager}");

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "Conflict", "Cannot delete user with created tasks");
    }
}
