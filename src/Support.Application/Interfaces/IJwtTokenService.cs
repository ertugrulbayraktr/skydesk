using Support.Domain.Entities;

namespace Support.Application.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(User user);

    /// <summary>
    /// Token for a provisioned passenger account: carries the real user Guid
    /// in NameIdentifier plus the verified PNR as a custom claim.
    /// </summary>
    string GeneratePassengerToken(User user, string pnr);
}
