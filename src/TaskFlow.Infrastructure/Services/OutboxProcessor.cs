using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Common.Email;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Settings;
using TaskFlow.Application.DTOs;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Data.Context;
using TaskFlow.Infrastructure.Services.Email;

namespace TaskFlow.Infrastructure.Services;

/// <summary>
/// Delivers persisted outbox messages for both Email (with exponential backoff retry and DeadLetter)
/// and Push (session-open flush for active users), using PostgreSQL <c>FOR UPDATE SKIP LOCKED</c> row claiming.
/// </summary>
public sealed class OutboxProcessor : IOutboxProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _db;
    private readonly IRealTimeNotifier _notifier;
    private readonly IEmailSender _emailSender;
    private readonly IOptions<SmtpSettings> _smtpSettings;
    private readonly IOptions<OutboxSettings> _outboxSettings;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        ApplicationDbContext db,
        IRealTimeNotifier notifier,
        IEmailSender emailSender,
        IOptions<SmtpSettings> smtpSettings,
        IOptions<OutboxSettings> outboxSettings,
        ILogger<OutboxProcessor> logger)
    {
        _db = db;
        _notifier = notifier;
        _emailSender = emailSender;
        _smtpSettings = smtpSettings;
        _outboxSettings = outboxSettings;
        _logger = logger;
    }

    public async Task<int> ProcessDueEmailsAsync(CancellationToken cancellationToken = default)
    {
        if (!_smtpSettings.Value.Enabled)
            return 0;

        var utcNow = DateTime.UtcNow;
        var batchSize = _outboxSettings.Value.BatchSize;

        var claimed = await ClaimDueEmailsAsync(utcNow, batchSize, cancellationToken);
        if (claimed.Count == 0)
            return 0;

        var sentCount = 0;
        for (var i = 0; i < claimed.Count; i++)
        {
            try
            {
                if (await DeliverEmailRowAsync(claimed[i], cancellationToken))
                    sentCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Reset any remaining claimed rows so they don't stay stuck in Processing.
                for (var j = i + 1; j < claimed.Count; j++)
                    claimed[j].Status = OutboxStatus.Pending;
                await _db.SaveChangesAsync(CancellationToken.None);
                throw;
            }
        }

        return sentCount;
    }

    public async Task<bool> TrySendEmailByIdAsync(Guid outboxMessageId, CancellationToken cancellationToken = default)
    {
        if (!_smtpSettings.Value.Enabled)
            return false;

        var claimed = await _db.OutboxMessages
            .Where(m => m.Id == outboxMessageId && m.Channel == OutboxChannel.Email && m.Status == OutboxStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, OutboxStatus.Processing), cancellationToken);

        if (claimed == 0)
            return false;

        var row = await _db.OutboxMessages.FindAsync([outboxMessageId], cancellationToken);
        if (row is null)
            return false;

        // Ensure tracked entity reflects Status = Processing written by ExecuteUpdateAsync.
        await _db.Entry(row).ReloadAsync(cancellationToken);

        return await DeliverEmailRowAsync(row, cancellationToken);
    }

    public async Task<int> FlushPendingPushForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var isActive = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => (bool?)u.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        if (isActive != true)
            return 0;

        var batchSize = _outboxSettings.Value.BatchSize;
        var claimed = await ClaimPendingPushForUserAsync(userId, batchSize, cancellationToken);
        if (claimed.Count == 0)
            return 0;

        var sentCount = 0;
        for (var i = 0; i < claimed.Count; i++)
        {
            var row = claimed[i];
            try
            {
                var dto = JsonSerializer.Deserialize<NotificationDto>(row.PayloadJson, JsonOptions);
                if (dto is null)
                {
                    row.RecordFailure("Push outbox payload deserialized to null.", DateTime.UtcNow,
                        TimeSpan.FromMinutes(_outboxSettings.Value.BaseRetryDelayMinutes));
                    continue;
                }

                await _notifier.SendAsync(userId, dto);
                row.MarkSent(DateTime.UtcNow);
                sentCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Reset this row and all remaining claimed rows so none stay stuck in Processing.
                for (var j = i; j < claimed.Count; j++)
                    claimed[j].Status = OutboxStatus.Pending;
                await _db.SaveChangesAsync(CancellationToken.None);
                throw;
            }
            catch (Exception ex)
            {
                // Leave Pending so the user's next open session retries the push.
                row.Status = OutboxStatus.Pending;
                row.LastError = TruncateError(ex.Message);
                _logger.LogWarning(ex,
                    "Failed to flush push outbox {OutboxMessageId} for user {UserId}; left Pending",
                    row.Id, userId);
            }
        }

        await _db.SaveChangesAsync(CancellationToken.None);
        return sentCount;
    }

    public async Task<int> RecoverStuckMessagesAsync(CancellationToken cancellationToken = default)
    {
        var staleBefore = DateTime.UtcNow.AddMinutes(-5);
        return await _db.OutboxMessages
            .Where(m => m.Status == OutboxStatus.Processing && (m.UpdatedAt ?? m.CreatedAt) < staleBefore)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, OutboxStatus.Pending)
                .SetProperty(m => m.UpdatedAt, DateTime.UtcNow),
                cancellationToken);
    }

    public async Task<int> CleanupSentMessagesAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-_outboxSettings.Value.RetentionDays);
        return await _db.OutboxMessages
            .Where(m => m.Status == OutboxStatus.Sent && m.ProcessedAt != null && m.ProcessedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<List<OutboxMessage>> ClaimDueEmailsAsync(DateTime utcNow, int batchSize, CancellationToken cancellationToken)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async ct =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var rows = await _db.OutboxMessages
                .FromSqlInterpolated($"""
                    SELECT * FROM outbox_messages
                    WHERE channel = {nameof(OutboxChannel.Email)}
                      AND status = {nameof(OutboxStatus.Pending)}
                      AND next_attempt_at <= {utcNow}
                    ORDER BY next_attempt_at, id
                    LIMIT {batchSize}
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(ct);

            if (rows.Count == 0)
            {
                await tx.CommitAsync(ct);
                return rows;
            }

            foreach (var row in rows)
                row.Status = OutboxStatus.Processing;

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return rows;
        }, cancellationToken);
    }

    private async Task<List<OutboxMessage>> ClaimPendingPushForUserAsync(Guid userId, int batchSize, CancellationToken cancellationToken)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async ct =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var rows = await _db.OutboxMessages
                .FromSqlInterpolated($"""
                    SELECT * FROM outbox_messages
                    WHERE recipient_user_id = {userId}
                      AND channel = {nameof(OutboxChannel.Push)}
                      AND status = {nameof(OutboxStatus.Pending)}
                    ORDER BY created_at, id
                    LIMIT {batchSize}
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(ct);

            if (rows.Count == 0)
            {
                await tx.CommitAsync(ct);
                return rows;
            }

            foreach (var row in rows)
                row.Status = OutboxStatus.Processing;

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return rows;
        }, cancellationToken);
    }

    private async Task<bool> DeliverEmailRowAsync(OutboxMessage row, CancellationToken cancellationToken)
    {
        var baseDelay = TimeSpan.FromMinutes(_outboxSettings.Value.BaseRetryDelayMinutes);

        try
        {
            var message = JsonSerializer.Deserialize<EmailMessage>(row.PayloadJson, JsonOptions);
            if (message is null)
            {
                row.RecordFailure("Email outbox payload deserialized to null.", DateTime.UtcNow, baseDelay);
                await _db.SaveChangesAsync(cancellationToken);
                return false;
            }

            await _emailSender.SendAsync(message, cancellationToken);
            row.MarkSent(DateTime.UtcNow);
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            row.Status = OutboxStatus.Pending;
            await _db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            row.RecordFailure(ex.Message, DateTime.UtcNow, baseDelay);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(ex,
                "Outbox email {OutboxMessageId} delivery failed (attempt {Attempt}/{MaxAttempts}, status {Status})",
                row.Id, row.AttemptCount, row.MaxAttempts, row.Status);
            return false;
        }
    }

    private static string TruncateError(string error) =>
        error.Length > OutboxMessage.MaxErrorLength ? error[..OutboxMessage.MaxErrorLength] : error;
}
