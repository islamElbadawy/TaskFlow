using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Data.Context;
using TaskFlow.Infrastructure.Data.Seeding;
using TaskFlow.IntegrationTests.Infrastructure;

namespace TaskFlow.IntegrationTests;

/// <summary>Security fixes: password and role changes revoke existing tokens (security stamp), the last active Admin
/// can't be demoted or deleted, and change-password is rate-limited.</summary>
[Collection(ApiCollection.Name)]
public sealed class TokenRevocationTests(TaskFlowApiFactory factory)
{
    private static readonly Guid AdminId = DataSeeder.UserIds.Admin;

    // ---- security stamp ----

    [Fact]
    public async Task Changing_the_password_revokes_every_existing_token()
    {
        var user = await factory.RegisterAsync("Petra");
        var otherSession = await factory.LoginAsync(user.Email, TestUser.Password);

        var changed = await user.Client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = TestUser.Password, newPassword = "Brand-N3w-P@ss" });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await user.Client.GetAsync("/api/users/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await otherSession.GetAsync("/api/users/me")).StatusCode);

        var fresh = await factory.LoginAsync(user.Email, "Brand-N3w-P@ss");
        Assert.Equal(HttpStatusCode.OK, (await fresh.GetAsync("/api/users/me")).StatusCode);
    }

    [Fact]
    public async Task A_role_change_revokes_the_users_tokens_and_the_new_role_applies_after_login()
    {
        var admin = await factory.LoginAsync(SeedUsers.Admin);
        var user = await factory.RegisterAsync("Rolf");

        Assert.Equal(HttpStatusCode.OK, (await SetRoleAsync(admin, user.Id, "Manager")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await user.Client.GetAsync("/api/users/me")).StatusCode);
        var relogged = await factory.CreateAnonymousClient().PostAsJsonAsync("/api/auth/login",
            new { email = user.Email, password = TestUser.Password });
        Assert.Equal("Manager", (await relogged.Content.ReadFromJsonAsync<ApiResult<LoginData>>())!.Data!.User.Role);
    }

    [Fact]
    public async Task A_demoted_admin_cannot_use_their_old_token_to_promote_themselves_back()
    {
        var admin = await factory.LoginAsync(SeedUsers.Admin);
        var user = await factory.RegisterAsync("Dmitri");
        Assert.Equal(HttpStatusCode.OK, (await SetRoleAsync(admin, user.Id, "Admin")).StatusCode);
        var asAdmin = await factory.LoginAsync(user.Email, TestUser.Password);

        try
        {
            Assert.Equal(HttpStatusCode.OK, (await SetRoleAsync(admin, user.Id, "User")).StatusCode);

            var selfPromotion = await SetRoleAsync(asAdmin, user.Id, "Admin");

            Assert.Equal(HttpStatusCode.Unauthorized, selfPromotion.StatusCode);
            Assert.Equal(UserRole.User, await RoleInDatabaseAsync(user.Id));
        }
        finally
        {
            await SetRoleInDatabaseAsync(user.Id, UserRole.User);
        }
    }

    [Fact]
    public async Task Setting_the_same_role_again_keeps_the_users_tokens()
    {
        var admin = await factory.LoginAsync(SeedUsers.Admin);
        var user = await factory.RegisterAsync("Sam");

        Assert.Equal(HttpStatusCode.OK, (await SetRoleAsync(admin, user.Id, "User")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await user.Client.GetAsync("/api/users/me")).StatusCode);
    }

    [Fact]
    public async Task A_validly_signed_token_without_a_security_stamp_is_rejected()
    {
        var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenWithoutStamp(AdminId));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);
    }

    // ---- last active Admin ----

    [Fact]
    public async Task The_last_active_admin_cannot_be_demoted_even_when_a_deactivated_admin_exists_409()
    {
        var admin = await factory.LoginAsync(SeedUsers.Admin);
        var dormant = await factory.RegisterAsync("Dormant");
        await SetRoleInDatabaseAsync(dormant.Id, UserRole.Admin, isActive: false);

        try
        {
            var response = await SetRoleAsync(admin, AdminId, "User");

            await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "Conflict", "Cannot demote the last active Admin");
            Assert.Equal(UserRole.Admin, await RoleInDatabaseAsync(AdminId));
        }
        finally
        {
            await DeleteInDatabaseAsync(dormant.Id);
        }
    }

    [Fact]
    public async Task The_last_active_admin_cannot_delete_their_own_account_409()
    {
        var admin = await factory.LoginAsync(SeedUsers.Admin);

        var response = await admin.DeleteAsync($"/api/users/{AdminId}");

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "Conflict", "Cannot delete the last active Admin");
    }

    [Fact]
    public async Task An_admin_can_be_deleted_while_another_active_admin_remains()
    {
        var admin = await factory.LoginAsync(SeedUsers.Admin);
        var second = await factory.RegisterAsync("Second");
        await SetRoleInDatabaseAsync(second.Id, UserRole.Admin);

        var response = await admin.DeleteAsync($"/api/users/{second.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- rate limit ----

    [Fact]
    public async Task Change_password_shares_the_auth_rate_limit()
    {
        await using var limited = factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:Auth:PermitLimit", "3"));
        var client = limited.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var user = await factory.RegisterAsync("Guesser");   // registered on the shared host, outside this window
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        var wrongGuess = new { currentPassword = "wrong-guess", newPassword = "Another-P@ss1" };

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/change-password", wrongGuess)).StatusCode);

        var rejected = await client.PostAsJsonAsync("/api/auth/change-password", wrongGuess);
        await rejected.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "TooManyRequests", "Too many attempts. Try again later.");
    }

    // ---- helpers ----

    private static Task<HttpResponseMessage> SetRoleAsync(HttpClient client, Guid userId, string newRole) =>
        client.PutAsJsonAsync($"/api/users/{userId}/role", new { newRole });

    private string TokenWithoutStamp(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["JwtSettings:Secret"]!));
        var token = new JwtSecurityTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, nameof(UserRole.Admin))
            ]),
            Expires = DateTime.UtcNow.AddMinutes(5),
            Issuer = config["JwtSettings:Issuer"],
            Audience = config["JwtSettings:Audience"],
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature)
        });
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<UserRole> RoleInDatabaseAsync(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Users.Where(u => u.Id == userId).Select(u => u.Role).SingleAsync();
    }

    private async Task SetRoleInDatabaseAsync(Guid userId, UserRole role, bool isActive = true)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, role).SetProperty(u => u.IsActive, isActive));
    }

    private async Task DeleteInDatabaseAsync(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Set<User>().Where(u => u.Id == userId).ExecuteDeleteAsync();
    }
}
