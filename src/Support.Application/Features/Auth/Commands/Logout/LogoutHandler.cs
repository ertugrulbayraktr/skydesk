using Microsoft.EntityFrameworkCore;
using Support.Application.Common;
using Support.Application.Interfaces;

namespace Support.Application.Features.Auth.Commands.Logout;

public class LogoutHandler
{
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public LogoutHandler(IApplicationDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = RefreshTokenGenerator.Hash(request.RefreshToken);

        var storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        // Logout is idempotent: unknown/already-revoked tokens still succeed
        if (storedToken != null)
        {
            storedToken.Revoke(_timeProvider.GetUtcNow().UtcDateTime);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
