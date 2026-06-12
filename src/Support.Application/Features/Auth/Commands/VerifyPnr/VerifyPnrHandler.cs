using Microsoft.EntityFrameworkCore;
using Support.Application.Common;
using Support.Application.Interfaces;
using Support.Domain.Entities;
using Support.Domain.Enums;

namespace Support.Application.Features.Auth.Commands.VerifyPnr;

public class VerifyPnrHandler
{
    private readonly IApplicationDbContext _context;
    private readonly IReservationProvider _reservationProvider;
    private readonly IJwtTokenService _jwtTokenService;

    public VerifyPnrHandler(
        IApplicationDbContext context,
        IReservationProvider reservationProvider,
        IJwtTokenService jwtTokenService)
    {
        _context = context;
        _reservationProvider = reservationProvider;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<Result<VerifyPnrResult>> Handle(VerifyPnrCommand request, CancellationToken cancellationToken)
    {
        var reservation = await _reservationProvider.GetReservationAsync(request.PNR, request.LastName, cancellationToken);

        if (reservation == null)
        {
            return Result<VerifyPnrResult>.Failure("Invalid PNR or last name", ErrorType.Unauthorized);
        }

        var passenger = reservation.Passengers.FirstOrDefault(p =>
            p.LastName.Equals(request.LastName, StringComparison.OrdinalIgnoreCase));

        if (passenger == null)
        {
            return Result<VerifyPnrResult>.Failure("Passenger not found in reservation", ErrorType.Unauthorized);
        }

        // Provision-on-verify: every passenger gets a real User row so all
        // Guid-based ownership/audit logic works for them.
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == passenger.Email, cancellationToken);

        if (user == null)
        {
            user = User.CreatePassenger(passenger.Email, $"{passenger.FirstName} {passenger.LastName}", reservation.PNR);
            _context.Users.Add(user);
        }
        else
        {
            if (user.Role != Role.Passenger)
            {
                // Staff must use email/password login, not the PNR flow
                return Result<VerifyPnrResult>.Failure("Invalid PNR or last name", ErrorType.Unauthorized);
            }
            user.UpdateLastKnownPNR(reservation.PNR);
        }

        await _context.SaveChangesAsync(cancellationToken);

        var token = _jwtTokenService.GeneratePassengerToken(user, reservation.PNR);

        return Result<VerifyPnrResult>.Success(new VerifyPnrResult
        {
            Token = token,
            PassengerEmail = passenger.Email
        });
    }
}
