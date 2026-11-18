# Notifications and real-time push

TaskFlow creates notifications automatically when tasks and comments change, stores them, and pushes each one
to the recipient's open connections over SignalR. Clients read and manage them with the REST API.

## When a notification is created

| Event | Who is notified | Type | Example message |
|---|---|---|---|
| Task created with an assignee, `PUT` with a new assignee, `PATCH .../assignee` | the new assignee | `TaskAssigned` | Manager User assigned you "Write docs" |
| Status changed to `Done` (`PUT` or `PATCH .../status`) | the creator | `TaskCompleted` | Sam Taylor marked "Write docs" as done |
| `PUT` that changes something | the assignee | `TaskUpdated` | Manager User updated "Write docs" |
| `PATCH .../status` that changes the status | the assignee | `TaskUpdated` | Manager User moved "Write docs" to InProgress |
| Comment added | the creator and the assignee | `CommentAdded` | Regular User commented on "Write docs" |

- The person who made the change is never notified.
- Each user gets at most one notification per change. When several rules apply to the same person (e.g. they
  created the task and are assigned to it), the first row of the table wins.
- The notifications are saved in the same database transaction as the change. They are pushed only after the
  save succeeded; if a push fails it is logged, and the notification is still there for the REST API.
- Deleting a task keeps its notifications (history) and clears their `relatedTaskId`.
- `DeadlineApproaching` notifications come from the deadline reminder service (planned, Phase 6).

## REST API (always the caller's own notifications)

| Method | Route | Description |
|---|---|---|
| GET | `/api/notifications?unread=true&page=1&pageSize=20` | Newest first. `unread=true` only unread, `unread=false` only read, omitted all. `pageSize` 1-100. |
| GET | `/api/notifications/unread-count` | `{ "isSuccess": true, "data": 3 }` |
| PATCH | `/api/notifications/{id}/read` | Marks one as read (idempotent). 404 if it doesn't exist **or belongs to someone else**. |
| POST | `/api/notifications/read-all` | Marks all unread as read; `data` is how many changed. |

A notification (list item, PATCH response and push payload):

```json
{ "id": "019a…", "message": "Manager User assigned you \"Write docs\"", "type": "TaskAssigned",
  "isRead": false, "relatedTaskId": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "createdAt": "2026-11-18T09:15:02.123Z" }
```

## SignalR

- Hub: `/hubs/notifications` (requires a valid JWT).
- Server → client method: `ReceiveNotification(notification)` with the payload above.
- Authentication: browsers can't set headers on WebSocket requests, so the token goes in the `access_token`
  query string; the API accepts it there for `/hubs/*` paths only (`JwtBearerEvents.OnMessageReceived` in
  Program.cs).
- Users are matched by their id claim (`ClaimTypes.NameIdentifier`) with SignalR's default user id provider; the
  server sends with `Clients.User(userId)`, so every tab/device of that user receives it. There is no
  connection bookkeeping in the hub.
- Scale-out: with more than one API instance, add a backplane (Redis or Azure SignalR Service) so a push from one
  instance reaches connections on another.

### Minimal JavaScript client

```html
<script src="https://cdn.jsdelivr.net/npm/@microsoft/signalr@8.0.7/dist/browser/signalr.min.js"></script>
<script>
  const api = "https://localhost:7103";
  let token; // from POST /api/auth/login -> data.token

  async function start() {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${api}/hubs/notifications`, {
        accessTokenFactory: () => token,
        // The API's CORS policy currently allows any origin, which browsers refuse for credentialed requests.
        // The token is sent in the query string, so cookies aren't needed.
        withCredentials: false,
      })
      .withAutomaticReconnect()
      .build();

    connection.on("ReceiveNotification", (n) => {
      console.log(`[${n.type}] ${n.message}`, n);
      // e.g. prepend to the list and increment the unread badge
    });

    // Pushes are not replayed: after (re)connecting, fetch what was missed.
    connection.onreconnected(loadUnread);

    await connection.start();
    await loadUnread();
  }

  async function loadUnread() {
    const res = await fetch(`${api}/api/notifications?unread=true`, { headers: { Authorization: `Bearer ${token}` } });
    const { data } = await res.json();
    console.log(`${data.totalCount} unread`, data.items);
  }
</script>
```

With npm: `npm install @microsoft/signalr`, then `import * as signalR from "@microsoft/signalr";` and the same code.

### .NET client (as used by the integration tests)

```csharp
var connection = new HubConnectionBuilder()
    .WithUrl("https://localhost:7103/hubs/notifications", o => o.AccessTokenProvider = () => Task.FromResult<string?>(token))
    .WithAutomaticReconnect()
    .Build();
connection.On<NotificationDto>("ReceiveNotification", n => Console.WriteLine($"[{n.Type}] {n.Message}"));
await connection.StartAsync();
```

## Try it

1. Log in as Sam (`user2@taskflow.dev`) and connect a client with Sam's token.
2. Log in as the Manager and create a task assigned to Sam (`POST /api/tasks` with
   `"assignedToId": "44444444-4444-4444-4444-444444444444"`).
3. Sam's client logs `[TaskAssigned] Manager User assigned you "…"`; `GET /api/notifications` as Sam shows the
   same notification.
