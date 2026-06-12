using Microsoft.EntityFrameworkCore;
using Support.Application.Common;
using Support.Application.Interfaces;
using Support.Domain.Entities;
using Support.Domain.Enums;
using System.Text.RegularExpressions;

namespace Support.Application.Features.Policies.Commands.PublishPolicy;

public class PublishPolicyHandler
{
    private readonly IApplicationDbContext _context;
    private readonly IPolicySearchService _policySearchService;

    public PublishPolicyHandler(IApplicationDbContext context, IPolicySearchService policySearchService)
    {
        _context = context;
        _policySearchService = policySearchService;
    }

    public async Task<Result> Handle(PublishPolicyCommand request, CancellationToken cancellationToken)
    {
        var policy = await _context.PolicyDocuments
            .Include(p => p.Chunks)
            .FirstOrDefaultAsync(p => p.Id == request.PolicyId, cancellationToken);

        if (policy == null)
        {
            return Result.Failure("Policy not found", ErrorType.NotFound);
        }

        if (policy.Status != Domain.Enums.PolicyStatus.Draft)
        {
            return Result.Failure($"Only draft policies can be published. Current status: {policy.Status}", ErrorType.Conflict);
        }

        policy.Publish();

        // Remove old chunks
        _context.PolicyChunks.RemoveRange(policy.Chunks);

        // Heading-based sections, token-bounded with overlap (see MarkdownChunker)
        var chunks = MarkdownChunker.Chunk(policy.Content)
            .Select(c => new PolicyChunk(policy.Id, c.SectionTitle, c.Content, c.Index))
            .ToList();
        foreach (var chunk in chunks)
        {
            _context.PolicyChunks.Add(chunk);
        }

        // NOTE: TicketAuditEvent has an FK to Tickets — policy publishes are NOT
        // ticket events and must not be written there (a Guid.Empty TicketId
        // violates the FK on real SQL Server). Publish actions are traceable via
        // the policy's PublishedAt/Version fields and structured logs.

        await _context.SaveChangesAsync(cancellationToken);

        await _policySearchService.ReindexPolicyAsync(policy.Id, cancellationToken);

        return Result.Success();
    }
}
