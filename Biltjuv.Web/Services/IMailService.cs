namespace Biltjuv.Web.Services;

/// <summary>
/// The outbound mail queue. Callers <see cref="QueueEmailAsync"/> a message and
/// return immediately; the <c>MailTimer</c> Quartz job later calls
/// <see cref="SendPendingEmailsAsync"/> to deliver it.
/// </summary>
public interface IMailService
{
    /// <param name="body">Plain-text body. Always required — it's the fallback part.</param>
    /// <param name="htmlBody">
    /// Optional HTML body. When given, the message is sent as
    /// <c>multipart/alternative</c> with <paramref name="body"/> as the text part.
    /// </param>
    Task QueueEmailAsync(
        string to,
        string subject,
        string body,
        string? htmlBody = null,
        int priority = MailService.NormalPriority,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends up to <c>MailOptions.BatchSize</c> pending messages. A failed send is
    /// logged and its attempt count bumped; the message is retried on a later run
    /// until it succeeds or hits <c>MailOptions.MaxSendAttempts</c>.
    /// </summary>
    Task SendPendingEmailsAsync(CancellationToken cancellationToken = default);
}
