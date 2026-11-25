using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Commands.Users.SetUserActiveCommand;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces;
using TaskFlow.Infrastructure.Data.Context;
using TaskFlow.Infrastructure.Data.Seeding;
using TaskFlow.IntegrationTests.Infrastructure;

namespace TaskFlow.IntegrationTests;

/// <summary>PATCH /api/users/{id}/active, isActive in user reads, and the per-request check that rejects tokens of
/// inactive or deleted users. Tests that touch seeded accounts restore them in a finally block.</summary>
[Collection(ApiCollection.Name)]
public sealed class UserActivationTests(TaskFlowApiFactory factory)
{
    private static readonly Guid AdminId = DataSeeder.UserIds.Admin;
    private static readonly Guid InactiveId = DataSeeder.UserIds.Inactive;

    // ---- rules and errors ----

    [Fact]
    public async Task Only_an_admin_may_change_activation_403()
    {
        var target = await factory.RegisterAsync("Paula");
        var manager = await factory.LoginAsync(SeedUsers.Manager);

        var response = await SetActiveAsync(manager, target.Id, false);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden);
        Assert.True((await GetUserAsync(manager, target.Id)).IsActive);
    }

    [Fact]
    public async Task Unknown_user_is_404()
    {
        var admin = await factory.LoginAsync(SeedUsers.Admin);

        var response = await SetActiveAsync(admin, Guid.NewGuid(), false);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "NotFound", "User not found");
    }

    [Fact]
    public async Task Missing_isActive_is_400()
    {
        var admin = await factory.LoginAsync(SeedUsers.Admin);
        var target = await factory.RegisterAsync("Quinn");

        var response = await admin.PatchAsJsonAsync($"/api/users/{target.Id}/active", new { });

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest);
        Assert.Contains("IsActive", problem.Extensions["errors"]!.ToString());
    }

    [Fact]
    public async Task An_admin_cannot_deactivate_themselves_409()
    {
        var admin = await factory.LoginAsync(SeedUsers.Admin);

        var response = await SetActiveAsync(admin, AdminId, false);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "Conflict", "You cannot deactivate your own account");
    }

    [Fact]
    public async Task The_last_active_admin_cannot_be_deactivated_409()
    {
        // Through the API the acting Admin is always a second active Admin (and a demoted Admin's token is revoked),
        // so the rule is a safety net for races. Run the handler directly with an actor that isn't an active Admin.
        using var scope = factory.Services.CreateScope();
        var handler = new SetUserActiveCommandHandler(scope.ServiceProvider.GetRequiredService<IUnitOfWork>(),
            new FixedCurrentUser(Guid.NewGuid(), UserRole.Admin));

        var result = await handler.HandleAsync(new SetUserActiveCommand { UserId = AdminId, IsActive = false });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal(SetUserActiveCommandHandler.LastActiveAdminMessage, result.ErrorMessage);
        Assert.True((await GetUserAsync(await factory.LoginAsync(SeedUsers.User), AdminId)).IsActive);
    }

    private sealed record FixedCurrentUser(Guid? UserId, UserRole? Role) : ICurrentUserService;

    // ---- deactivate / reactivate ----

    [Fact]
    public async Task Deactivation_blocks_login_and_existing_tokens_and_reactivation_restores_both()
    {
        var admin = await factory.LoginAsync(SeedUsers.Admin);
        var user = await factory.RegisterAsync("Dora");
        Assert.Equal(HttpStatusCode.OK, (await user.Client.GetAsync("/api/users/me")).StatusCode);

        var deactivated = await ReadAsync<UserBody>(await SetActiveAsync(admin, user.Id, false));
        Assert.False(deactivated.IsActive);
        Assert.False((await ReadAsync<UserBody>(await SetActiveAsync(admin, user.Id, false))).IsActive);   // idempotent

        await (await LoginAsync(user)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Unauthorized", "Invalid email or password");
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.Client.GetAsync("/api/users/me")).StatusCode);   // old, unexpired token
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.Client.GetAsync("/api/tasks")).StatusCode);

        var reactivated = await ReadAsync<UserBody>(await SetActiveAsync(admin, user.Id, true));
        Assert.True(reactivated.IsActive);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(user)).StatusCode);
        // Pre-deactivation tokens stay permanently revoked even after reactivation (must log in again).
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.Client.GetAsync("/api/users/me")).StatusCode);
    }

    [Fact]
    public async Task Seeded_inactive_user_can_be_activated_and_deactivated_again()
    {
        var admin = await factory.LoginAsync(SeedUsers.Admin);
        Assert.False((await GetUserAsync(admin, InactiveId)).IsActive);

        try
        {
            Assert.True((await ReadAsync<UserBody>(await SetActiveAsync(admin, InactiveId, true))).IsActive);
            var inactive = await factory.LoginAsync(SeedUsers.Inactive);   // throws unless the login succeeds
            Assert.Equal(HttpStatusCode.OK, (await inactive.GetAsync("/api/users/me")).StatusCode);

            Assert.False((await ReadAsync<UserBody>(await SetActiveAsync(admin, InactiveId, false))).IsActive);
            var login = await factory.CreateAnonymousClient().PostAsJsonAsync("/api/auth/login",
                new { email = SeedUsers.Inactive, password = DataSeeder.DefaultPassword });
            Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await inactive.GetAsync("/api/users/me")).StatusCode);
        }
        finally
        {
            await SetActiveInDatabaseAsync(InactiveId, false);
        }
    }

    [Fact]
    public async Task Token_of_a_deleted_user_is_rejected()
    {
        var user = await factory.RegisterAsync("Gwen");
        Assert.Equal(HttpStatusCode.OK, (await user.Client.DeleteAsync($"/api/users/{user.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await user.Client.GetAsync("/api/users/me")).StatusCode);
    }

    [Fact]
    public async Task Deactivated_user_cannot_connect_to_the_notification_hub()
    {
        var admin = await factory.LoginAsync(SeedUsers.Admin);
        var user = await factory.RegisterAsync("Hank");
        await SetActiveAsync(admin, user.Id, false);

        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "hubs/notifications"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(user.Token);
            })
            .Build();

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
        await connection.DisposeAsync();
    }

    // ---- reads ----

    [Fact]
    public async Task User_reads_show_isActive_and_the_list_filters_by_it()
    {
        var client = await factory.LoginAsync(SeedUsers.User);

        Assert.False((await GetUserAsync(client, InactiveId)).IsActive);
        Assert.True((await GetUserAsync(client, AdminId)).IsActive);

        var all = await ReadAsync<List<UserBody>>(await client.GetAsync("/api/users"));
        var inactiveOnly = await ReadAsync<List<UserBody>>(await client.GetAsync("/api/users?isActive=false"));
        var activeOnly = await ReadAsync<List<UserBody>>(await client.GetAsync("/api/users?isActive=true"));

        Assert.Contains(inactiveOnly, u => u.Id == InactiveId);
        Assert.All(inactiveOnly, u => Assert.False(u.IsActive));
        Assert.Contains(activeOnly, u => u.Id == AdminId);
        Assert.All(activeOnly, u => Assert.True(u.IsActive));
        Assert.Equal(all.Count, inactiveOnly.Count + activeOnly.Count);
    }

    // ---- helpers ----

    private sealed record UserBody(Guid Id, string FullName, string Email, string Role, bool IsActive);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResult<T>>())!.Data!;
    }

    private static Task<HttpResponseMessage> SetActiveAsync(HttpClient client, Guid userId, bool isActive) =>
        client.PatchAsJsonAsync($"/api/users/{userId}/active", new { isActive });

    private static async Task<UserBody> GetUserAsync(HttpClient client, Guid userId) =>
        await ReadAsync<UserBody>(await client.GetAsync($"/api/users/{userId}"));

    private Task<HttpResponseMessage> LoginAsync(TestUser user) =>
        factory.CreateAnonymousClient().PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = TestUser.Password });

    /// <summary>Restores a seeded account directly, so a failing assertion can't leave it changed for other tests.</summary>
    private async Task SetActiveInDatabaseAsync(Guid userId, bool isActive)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, isActive));
    }
}
