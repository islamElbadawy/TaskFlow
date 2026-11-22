using System.Net;
using System.Net.Http.Json;
using TaskFlow.Infrastructure.Data.Seeding;
using TaskFlow.IntegrationTests.Infrastructure;

namespace TaskFlow.IntegrationTests;

/// <summary>Comment endpoints. Reads use the seeded comments; writes only touch comments the tests create.</summary>
[Collection(ApiCollection.Name)]
public sealed class CommentsTests(TaskFlowApiFactory factory)
{
    // Seeded comments on "Write docs" (see docs/DATABASE.md 3.3).
    private static readonly Guid WriteDocsTask = DataSeeder.TaskIds.WriteDocs;
    private static readonly Guid UserCommentOnWriteDocs = Guid.Parse("c0000000-0000-0000-0000-000000000003");
    private static readonly Guid ManagerCommentOnWriteDocs = Guid.Parse("c0000000-0000-0000-0000-000000000004");

    [Fact]
    public async Task List_returns_the_seeded_comments_oldest_first_with_author_names()
    {
        var client = await factory.LoginAsync(SeedUsers.User);

        var page = await ReadAsync<Page>(await client.GetAsync($"/api/tasks/{WriteDocsTask}/comments"));

        Assert.Equal(1, page.PageNumber);
        Assert.Equal(20, page.PageSize);
        Assert.True(page.TotalCount >= 2);
        Assert.Equal(UserCommentOnWriteDocs, page.Items[0].Id);
        Assert.Equal("Regular User", page.Items[0].UserName);
        Assert.Equal(ManagerCommentOnWriteDocs, page.Items[1].Id);
        Assert.Equal("Manager User", page.Items[1].UserName);
        var created = page.Items.Select(c => c.CreatedAt).ToList();
        Assert.Equal(created.Order().ToList(), created);

        // A non-admin who is neither creator nor assignee gets no comments; Admin sees them all.
        var outsiderPage = await ReadAsync<Page>(await (await factory.LoginAsync(SeedUsers.User2))
            .GetAsync($"/api/tasks/{WriteDocsTask}/comments"));
        Assert.Equal(0, outsiderPage.TotalCount);
        Assert.Empty(outsiderPage.Items);

        var adminPage = await ReadAsync<Page>(await (await factory.LoginAsync(SeedUsers.Admin))
            .GetAsync($"/api/tasks/{WriteDocsTask}/comments"));
        Assert.True(adminPage.TotalCount >= 2);
    }

    [Fact]
    public async Task List_pages_through_the_comments()
    {
        var manager = await factory.LoginAsync(SeedUsers.Manager);
        var taskId = await CreateTaskAsync(manager, "Test: paged comments");
        for (var i = 1; i <= 3; i++)
            await ReadAsync<CommentBody>(await manager.PostAsJsonAsync($"/api/tasks/{taskId}/comments", new { content = $"comment {i}" }));

        var page2 = await ReadAsync<Page>(await manager.GetAsync($"/api/tasks/{taskId}/comments?page=2&pageSize=2"));

        Assert.Equal(3, page2.TotalCount);
        Assert.Equal(2, page2.TotalPages);
        Assert.Equal("comment 3", Assert.Single(page2.Items).Content);
    }

    [Fact]
    public async Task List_for_an_unknown_task_is_404_and_bad_paging_is_400()
    {
        var client = await factory.LoginAsync(SeedUsers.User);

        await (await client.GetAsync($"/api/tasks/{Guid.NewGuid()}/comments"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "NotFound", "Task not found");

        var problem = await (await client.GetAsync($"/api/tasks/{WriteDocsTask}/comments?pageSize=500"))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest);
        Assert.Contains("PageSize", problem.Extensions["errors"]!.ToString());
    }

    [Fact]
    public async Task Any_user_can_comment_the_author_comes_from_the_token_and_the_count_goes_up()
    {
        var manager = await factory.LoginAsync(SeedUsers.Manager);
        var taskId = await CreateTaskAsync(manager, "Test: comment count");
        var user2 = await factory.LoginAsync(SeedUsers.User2);

        var comment = await ReadAsync<CommentBody>(await user2.PostAsJsonAsync($"/api/tasks/{taskId}/comments",
            new { content = "  Looks good to me  ", userId = DataSeeder.UserIds.Admin }));

        Assert.Equal("Looks good to me", comment.Content);
        Assert.Equal(DataSeeder.UserIds.User2, comment.UserId);
        Assert.Equal("Sam Taylor", comment.UserName);
        Assert.Equal(taskId, comment.TaskId);
        Assert.Null(comment.UpdatedAt);

        var task = await ReadAsync<TaskCount>(await user2.GetAsync($"/api/tasks/{taskId}"));
        Assert.Equal(1, task.CommentsCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_content_is_400(string content)
    {
        var client = await factory.LoginAsync(SeedUsers.User);

        var problem = await (await client.PostAsJsonAsync($"/api/tasks/{WriteDocsTask}/comments", new { content }))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest);
        Assert.Contains("Content", problem.Extensions["errors"]!.ToString());
    }

    [Fact]
    public async Task Content_over_1000_characters_is_400_and_exactly_1000_is_fine()
    {
        var manager = await factory.LoginAsync(SeedUsers.Manager);
        var taskId = await CreateTaskAsync(manager, "Test: comment length");

        await (await manager.PostAsJsonAsync($"/api/tasks/{taskId}/comments", new { content = new string('x', 1001) }))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest);

        var comment = await ReadAsync<CommentBody>(
            await manager.PostAsJsonAsync($"/api/tasks/{taskId}/comments", new { content = new string('x', 1000) }));
        Assert.Equal(1000, comment.Content.Length);
    }

    [Fact]
    public async Task Commenting_on_an_unknown_task_is_404()
    {
        var client = await factory.LoginAsync(SeedUsers.User);

        await (await client.PostAsJsonAsync($"/api/tasks/{Guid.NewGuid()}/comments", new { content = "hello?" }))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "NotFound", "Task not found");
    }

    [Fact]
    public async Task Only_the_author_can_edit_even_an_admin_gets_403()
    {
        var user = await factory.LoginAsync(SeedUsers.User);
        var comment = await ReadAsync<CommentBody>(
            await user.PostAsJsonAsync($"/api/tasks/{WriteDocsTask}/comments", new { content = "Test: original" }));

        try
        {
            foreach (var other in new[] { SeedUsers.User2, SeedUsers.Manager, SeedUsers.Admin })
            {
                var client = await factory.LoginAsync(other);
                await (await client.PutAsJsonAsync($"/api/comments/{comment.Id}", new { content = "Test: rewritten" }))
                    .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Forbidden", "Only the author can edit this comment.");
            }

            var edited = await ReadAsync<CommentBody>(
                await user.PutAsJsonAsync($"/api/comments/{comment.Id}", new { content = "Test: edited" }));
            Assert.Equal("Test: edited", edited.Content);
            Assert.NotNull(edited.UpdatedAt);
            Assert.Equal(comment.CreatedAt, edited.CreatedAt);
        }
        finally
        {
            await user.DeleteAsync($"/api/comments/{comment.Id}");
        }
    }

    [Fact]
    public async Task Author_or_admin_can_delete_others_get_403()
    {
        var manager = await factory.LoginAsync(SeedUsers.Manager);
        var taskId = await CreateTaskAsync(manager, "Test: delete comments");
        var user = await factory.LoginAsync(SeedUsers.User);
        var mine = await ReadAsync<CommentBody>(await user.PostAsJsonAsync($"/api/tasks/{taskId}/comments", new { content = "mine" }));
        var forAdmin = await ReadAsync<CommentBody>(await user.PostAsJsonAsync($"/api/tasks/{taskId}/comments", new { content = "moderated" }));

        // Not the author: neither another user nor a Manager (even the task's creator) may delete it.
        foreach (var other in new[] { SeedUsers.User2, SeedUsers.Manager })
            await (await (await factory.LoginAsync(other)).DeleteAsync($"/api/comments/{mine.Id}"))
                .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Forbidden", "Only the author or an Admin can delete this comment.");

        Assert.Equal(HttpStatusCode.OK, (await user.DeleteAsync($"/api/comments/{mine.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await factory.LoginAsync(SeedUsers.Admin)).DeleteAsync($"/api/comments/{forAdmin.Id}")).StatusCode);

        var page = await ReadAsync<Page>(await user.GetAsync($"/api/tasks/{taskId}/comments"));
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(0, (await ReadAsync<TaskCount>(await user.GetAsync($"/api/tasks/{taskId}"))).CommentsCount);
    }

    [Fact]
    public async Task Unknown_comment_is_404_for_edit_and_delete()
    {
        var admin = await factory.LoginAsync(SeedUsers.Admin);
        var id = Guid.NewGuid();

        await (await admin.PutAsJsonAsync($"/api/comments/{id}", new { content = "x" }))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "NotFound", "Comment not found");
        await (await admin.DeleteAsync($"/api/comments/{id}"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "NotFound", "Comment not found");
    }

    [Fact]
    public async Task Seeded_comment_cannot_be_edited_by_someone_else()
    {
        var user2 = await factory.LoginAsync(SeedUsers.User2);

        await (await user2.PutAsJsonAsync($"/api/comments/{ManagerCommentOnWriteDocs}", new { content = "Test: nope" }))
            .ShouldBeProblemAsync(HttpStatusCode.Forbidden);
    }

    // ---- helpers ----

    private sealed record CommentBody(Guid Id, string Content, Guid TaskId, Guid UserId, string UserName,
        DateTime CreatedAt, DateTime? UpdatedAt);

    private sealed record Page(List<CommentBody> Items, int PageNumber, int PageSize, int TotalCount, int TotalPages);

    private sealed record TaskCount(Guid Id, int CommentsCount);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResult<T>>())!.Data!;
    }

    private static async Task<Guid> CreateTaskAsync(HttpClient client, string title) =>
        (await ReadAsync<TaskCount>(await client.PostAsJsonAsync("/api/tasks",
            new { title, description = "", priority = "Medium" }))).Id;
}
