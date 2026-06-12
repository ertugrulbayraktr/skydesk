using Microsoft.EntityFrameworkCore;
using Support.Application.Common;
using Support.Application.Interfaces;

namespace Support.Application.Features.Policies.Commands.ArchivePolicy;

/// <summary>
/// Archives a published policy. Chunks are kept for history but automatically
/// drop out of retrieval because search filters on Status == Published.
/// </summary>
public class ArchivePolicyHandler
{
    private readonly IApplicationDbContext _context;

    public ArchivePolicyHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(ArchivePolicyCommand request, CancellationToken cancellationToken)
    {
        var policy = await _context.PolicyDocuments
            .FirstOrDefaultAsync(p => p.Id == request.PolicyId, cancellationToken);

        if (policy == null)
        {
            return Result.Failure("Policy not found", ErrorType.NotFound);
        }

        try
        {
            policy.Archive();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message, ErrorType.Conflict);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
