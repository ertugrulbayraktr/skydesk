using Microsoft.EntityFrameworkCore;
using Support.Application.Common;
using Support.Application.Interfaces;
using Support.Domain.Entities;

namespace Support.Application.Features.Auth.Commands.Login;

public class LoginHandler
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly TimeProvider _timeProvider;

    public LoginHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        TimeProvider timeProvider)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _timeProvider = timeProvider;
    }

    public async Task<Result<LoginResult>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (user == null || !user.IsActive || string.IsNullOrEmpty(user.PasswordHash))
        {
            return Result<LoginResult>.Failure("Invalid credentials", ErrorType.Unauthorized);
        }

        if (user.IsLockedOut(now))
        {
            return Result<LoginResult>.Failure("Account is temporarily locked due to repeated failed logins. Try again later.", ErrorType.Unauthorized);
        }

        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            user.RegisterFailedLogin(now);
            await _context.SaveChangesAsync(cancellationToken);
            return Result<LoginResult>.Failure("Invalid credentials", ErrorType.Unauthorized);
        }

        user.ResetLockout();

        var rawRefreshToken = RefreshTokenGenerator.Generate();
        _context.RefreshTokens.Add(new Domain.Entities.RefreshToken(
            user.Id,
            RefreshTokenGenerator.Hash(rawRefreshToken),
            now.Add(RefreshTokenGenerator.Lifetime)));

        await _context.SaveChangesAsync(cancellationToken);

        var token = _jwtTokenService.GenerateToken(user);

        return Result<LoginResult>.Success(new LoginResult
        {
            Token = token,
            RefreshToken = rawRefreshToken,
            UserName = user.FullName,
            Role = user.Role.ToString()
        });
    }
}
