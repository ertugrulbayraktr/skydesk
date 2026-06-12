using Microsoft.EntityFrameworkCore;
using Support.Application.Common;
using Support.Application.Interfaces;

namespace Support.Application.Features.Policies.Commands.UpdatePolicy;

public class UpdatePolicyHandler
{
    private readonly IApplicationDbContext _context;

    public UpdatePolicyHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(UpdatePolicyCommand request, CancellationToken cancellationToken)
    {
        var policy = await _context.PolicyDocuments
            .FirstOrDefaultAsync(p => p.Id == request.PolicyId, cancellationToken);

        if (policy == null)
        {
            return Result.Failure("Policy not found", ErrorType.NotFound);
        }

        try
        {
            policy.UpdateTitle(request.Title);
            policy.UpdateContent(request.Content);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message, ErrorType.Conflict);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
