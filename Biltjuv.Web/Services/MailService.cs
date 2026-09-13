using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Microsoft.Extensions.Options;
using Biltjuv.Web.Infrastructure.Mail;
using Biltjuv.Web.Infrastructure.Persistence.Entities;
using Biltjuv.Web.Infrastructure.Persistence.Repositories;

namespace Biltjuv.Web.Services;

/// <summary>
/// Owns the outbound mail queue: <see cref="QueueEmailAsync"/> persists a message
/// for later delivery, and <see cref="SendPendingEmailsAsync"/> — driven by the
/// <c>MailTimer</c> Quartz job — drains the queue over SMTP.
/// </summary>
public sealed class MailService(
    IEmailRepository emailRepository,
    IOptions<MailOptions> options,
    IHostEnvironment environment,
    ILogger<MailService> logger) : IMailService
{
    private readonly MailOptions _options = options.Value;

    /// <summary>Default priority for <see cref="QueueEmailAsync"/> — lower is sent first.</summary>
    public const int NormalPriority = 0;

    public Task QueueEmailAsync(
        string to,
        string subject,
        string body,
        string? htmlBody = null,
        int priority = NormalPriority,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        var email = new EmailEntity
        {
            Id = Guid.NewGuid(),
            Priority = priority,
            ToAddress = to,
            Subject = subject,
            Body = body ?? string.Empty,
            HtmlBody = string.IsNullOrWhiteSpace(htmlBody) ? null : htmlBody
        };

        logger.LogInformation(
            "Queued email {EmailId} to {To} (subject {Subject}, priority {Priority}).",
            email.Id, to, subject, priority);

        return emailRepository.AddAsync(email, cancellationToken);
    }

    public async Task SendPendingEmailsAsync(CancellationToken cancellationToken = default)
    {
        var pending = await emailRepository.GetPendingAsync(
            _options.BatchSize, _options.MaxSendAttempts, cancellationToken);

        if (pending.Count == 0)
        {
            logger.LogDebug("MailService: no pending emails to send.");
            return;
        }

        logger.LogInformation("MailService: sending {Count} pending email(s).", pending.Count);

        using var client = CreateSmtpClient();
        var sent = 0;

        foreach (var email in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var message = BuildMessage(email);
                await client.SendMailAsync(message, cancellationToken);

                await emailRepository.MarkSentAsync(email.Id, cancellationToken);
                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One bad message shouldn't abort the batch — bump its attempt
                // count and move on; it'll be retried next run.
                await emailRepository.RecordFailedAttemptAsync(email.Id, CancellationToken.None);
                logger.LogWarning(
                    ex,
                    "MailService: failed to send email {EmailId} to {To} (attempt {Attempt}/{Max}).",
                    email.Id, email.ToAddress, email.SendAttempts + 1, _options.MaxSendAttempts);
            }
        }

        logger.LogInformation("MailService: sent {Sent}/{Total} email(s).", sent, pending.Count);
    }

    private MailMessage BuildMessage(EmailEntity email)
    {
        var message = new MailMessage
        {
            From = string.IsNullOrWhiteSpace(_options.FromDisplayName)
                ? new MailAddress(_options.FromAddress)
                : new MailAddress(_options.FromAddress, _options.FromDisplayName),
            Subject = email.Subject,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8
        };

        message.To.Add(email.ToAddress);

        if (string.IsNullOrWhiteSpace(email.HtmlBody))
        {
            message.Body = email.Body;
            message.IsBodyHtml = false;
            return message;
        }

        // multipart/alternative: least-preferred part (plain text) first so
        // clients that support HTML pick the last one.
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            email.Body, Encoding.UTF8, MediaTypeNames.Text.Plain));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            email.HtmlBody, Encoding.UTF8, MediaTypeNames.Text.Html));

        return message;
    }

    private SmtpClient CreateSmtpClient()
    {
        // In Development, write .eml files to a pickup folder instead of relying
        // on a real SMTP server being reachable from a dev machine.
        if (environment.IsDevelopment())
        {
            var pickupPath = string.IsNullOrWhiteSpace(_options.PickupDirectory)
                ? Path.Combine(environment.ContentRootPath, "mail-pickup")
                : _options.PickupDirectory;

            Directory.CreateDirectory(pickupPath);

            return new SmtpClient
            {
                DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                PickupDirectoryLocation = pickupPath
            };
        }

        var client = new SmtpClient
        {
            Host = _options.Host,
            Port = _options.Port,
            EnableSsl = _options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);

        return client;
    }
}
