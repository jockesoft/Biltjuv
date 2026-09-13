using Quartz;
using Biltjuv.Web.Services;

namespace Biltjuv.Web.Infrastructure.Timers;

/// <summary>
/// Drains the outbound mail queue. Scheduled once a minute in
/// <c>Program.cs</c>; each run hands off to
/// <see cref="MailService.SendPendingEmailsAsync"/>, which sends at most one
/// batch and bumps the retry count on any message that fails.
/// <see cref="DisallowConcurrentExecutionAttribute"/> keeps a slow SMTP round
/// trip from letting two runs send the same message twice.
/// </summary>
[DisallowConcurrentExecution]
public sealed class MailTimer(IMailService mailService, ILogger<MailTimer> logger) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await mailService.SendPendingEmailsAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never let an unhandled exception escape into the scheduler.
            logger.LogError(ex, "MailTimer: unexpected failure while sending pending emails.");
        }
    }
}
