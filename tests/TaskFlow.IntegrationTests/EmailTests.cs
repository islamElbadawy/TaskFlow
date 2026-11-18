using System.Net;
using System.Net.Http.Json;
using TaskFlow.IntegrationTests.Infrastructure;

namespace TaskFlow.IntegrationTests;

/// <summary>Which events send email (the test API records emails in <see cref="FakeEmailService"/>).</summary>
[Collection(ApiCollection.Name)]
public sealed class EmailTests(TaskFlowApiFactory factory)
{
    [Fact]
    public async Task Assigning_a_task_emails_the_assignee_with_text_and_encoded_html()
    {
        var creator = await factory.RegisterAsync("Ally");
        var assignee = await factory.RegisterAsync("Ben");
        const string title = "Test: <b>bold</b> & \"quoted\"";

        await CreateTaskAsync(creator, title, assignee.Id);

        var email = Assert.Single(factory.Emails.SentTo(assignee.Email));
        Assert.Equal($"New task: {title}", email.Subject);
        Assert.Equal("Ben Tester", email.ToName);
        Assert.Contains($"Ally Tester assigned you \"{title}\"", email.TextBody);
        Assert.Contains("Due: no due date", email.TextBody);
        Assert.Contains("Test: &lt;b&gt;bold&lt;/b&gt; &amp; &quot;quoted&quot;", email.HtmlBody);
        Assert.DoesNotContain("<b>bold</b>", email.HtmlBody);
        Assert.Empty(factory.Emails.SentTo(creator.Email));
    }

    [Fact]
    public async Task Only_assignments_send_email_not_updates_comments_or_self_assignment()
    {
        var creator = await factory.RegisterAsync("Cleo");
        var assignee = await factory.RegisterAsync("Dex");

        // Assigning yourself: no notification, so no email.
        var taskId = await CreateTaskAsync(creator, "Test: email rules", creator.Id);
        Assert.Empty(factory.Emails.SentTo(creator.Email));

        var assigned = await creator.Client.PatchAsJsonAsync($"/api/tasks/{taskId}/assignee", new { assignedToId = assignee.Id });
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        Assert.Single(factory.Emails.SentTo(assignee.Email));

        // TaskUpdated and CommentAdded notifications are in-app only.
        Assert.Equal(HttpStatusCode.OK,
            (await creator.Client.PatchAsJsonAsync($"/api/tasks/{taskId}/status", new { status = "InProgress" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await creator.Client.PostAsJsonAsync($"/api/tasks/{taskId}/comments", new { content = "Any news?" })).StatusCode);

        Assert.Single(factory.Emails.SentTo(assignee.Email));
        Assert.Empty(factory.Emails.SentTo(creator.Email));
    }

    private sealed record IdOnly(Guid Id);

    private static async Task<Guid> CreateTaskAsync(TestUser creator, string title, Guid? assignedToId)
    {
        var response = await creator.Client.PostAsJsonAsync("/api/tasks",
            new { title, description = "", priority = "Medium", dueDate = (DateTime?)null, assignedToId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResult<IdOnly>>())!.Data!.Id;
    }
}
