namespace Biltjuv.Web.Infrastructure.Persistence.Entities;

/// <summary>
/// A single outbound email queued for delivery. Rows are written by
/// <c>MailService.QueueEmailAsync</c> and drained by the <c>MailTimer</c> Quartz
/// job — a lightweight transactional-outbox table, not a full mail log.
/// </summary>
public sealed class EmailEntity
{
    public Guid Id { get; set; }

    /// <summary>Lower value is sent first; use <c>0</c> for "normal".</summary>
    public int Priority { get; set; }

    /// <summary>One or more recipients, comma-separated.</summary>
    public string ToAddress { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    /// <summary>Plain-text body. Always set — it's the fallback part of the message.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Optional HTML body. When present the message is sent as
    /// <c>multipart/alternative</c> with <see cref="Body"/> as the text part.
    /// </summary>
    public string? HtmlBody { get; set; }

    /// <summary>Incremented every time delivery is attempted and fails.</summary>
    public int SendAttempts { get; set; }

    /// <summary>Set once the message has been handed to SMTP; <c>null</c> while pending.</summary>
    public DateTime? SentUtc { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }
}
