using Microsoft.Extensions.Options;
using Biltjuv.Web.Infrastructure.Game;

namespace Biltjuv.Web.Services.Game;

public sealed class LevelService(IOptions<LevelOptions> options) : ILevelService
{
    private readonly LevelOptions _options = options.Value;

    public int GetLevel(int respect)
    {
        if (respect <= 0)
            return 1;

        return 1 + respect / _options.RespectPerLevel;
    }
}
