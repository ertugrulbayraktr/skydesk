using Microsoft.EntityFrameworkCore;
using Support.Application.Common;
using Support.Application.Interfaces;

namespace Support.Application.Features.Auth.Commands.RefreshToken;

public class RefreshTokenHandler
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly TimeProvider _timeProvider;

    public RefreshTokenHandler(
        IApplicationDbContext context,
        IJwtTokenService jwtTokenService,
        TimeProvider timeProvider)
    {
        _context = context;
        _jwtTokenService = jwtTokenService;
        _timeProvider = timeProvider;
    }

    public async Task<Result<RefreshTokenResult>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var tokenHash = RefreshTokenGenerator.Hash(request.RefreshToken);

        var storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (storedToken == null || !storedToken.IsActive(now))
        {
            return Result<RefreshTokenResult>.Failure("Invalid or expired refresh token", ErrorType.Unauthorized);
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == storedToken.UserId, cancellationToken);

        if (user == null || !user.IsActive)
        {
            return Result<RefreshTokenResult>.Failure("Invalid or expired refresh token", ErrorType.Unauthorized);
        }

        // Rotation: the presented token is single-use
        storedToken.Revoke(now);

        var rawRefreshToken = RefreshTokenGenerator.Generate();
        _context.RefreshTokens.Add(new Domain.Entities.RefreshToken(
            user.Id,
            RefreshTokenGenerator.Hash(rawRefreshToken),
            now.Add(RefreshTokenGenerator.Lifetime)));

        await _context.SaveChangesAsync(cancellationToken);

        return Result<RefreshTokenResult>.Success(new RefreshTokenResult
        {
            Token = _jwtTokenService.GenerateToken(user),
            RefreshToken = rawRefreshToken
        });
    }
}
