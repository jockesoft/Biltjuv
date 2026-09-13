using Biltjuv.Web.Infrastructure.Persistence.Entities;

namespace Biltjuv.Web.Infrastructure.Persistence.Repositories;

public interface IEmailRepository
{
    Task AddAsync(EmailEntity email, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmailEntity>> GetPendingAsync(
        int maxCount,
        int maxAttempts,
        CancellationToken cancellationToken = default);

    Task MarkSentAsync(Guid id, CancellationToken cancellationToken = default);

    Task RecordFailedAttemptAsync(Guid id, CancellationToken cancellationToken = default);
}
