namespace Biltjuv.Web.Infrastructure.Mail;

/// <summary>
/// Bound from the <c>Mail</c> configuration section. SMTP credentials belong in
/// user-secrets (dev) or environment variables (prod), never in appsettings.
/// </summary>
public sealed class MailOptions
{
    public const string SectionName = "Mail";

    /// <summary>Envelope/from address every queued message is sent as.</summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>Optional friendly name shown alongside <see cref="FromAddress"/>.</summary>
    public string? FromDisplayName { get; set; }

    /// <summary>SMTP host — only used outside Development.</summary>
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public bool EnableSsl { get; set; } = true;

    /// <summary>
    /// In Development, messages are dropped here as <c>.eml</c> files instead of
    /// talking to a real SMTP server. Defaults to a <c>mail-pickup</c> folder
    /// under the content root when left unset.
    /// </summary>
    public string? PickupDirectory { get; set; }

    /// <summary>How many messages a single timer run will attempt.</summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>
    /// A message that has failed this many times is left in the table but no
    /// longer retried, so a permanently bad address can't wedge the queue.
    /// </summary>
    public int MaxSendAttempts { get; set; } = 5;
}
