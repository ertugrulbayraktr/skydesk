namespace Support.Application.Features.Auth.Commands.RefreshToken;

public class RefreshTokenCommand
{
    public string RefreshToken { get; set; } = null!;
}

public class RefreshTokenResult
{
    public string Token { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
}
