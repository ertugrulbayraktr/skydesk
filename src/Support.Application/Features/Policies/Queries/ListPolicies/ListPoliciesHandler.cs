using Microsoft.EntityFrameworkCore;
using Support.Application.Common;
using Support.Application.Interfaces;

namespace Support.Application.Features.Policies.Queries.ListPolicies;

public class ListPoliciesHandler
{
    private readonly IApplicationDbContext _context;

    public ListPoliciesHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ListPoliciesResult>> Handle(ListPoliciesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.PolicyDocuments.AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        var policies = await query
            .OrderByDescending(p => p.UpdatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new PolicySummaryDto
            {
                Id = p.Id,
                Title = p.Title,
                Status = p.Status,
                Version = p.Version,
                PublishedAt = p.PublishedAt,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                ChunkCount = p.Chunks.Count
            })
            .ToListAsync(cancellationToken);

        return Result<ListPoliciesResult>.Success(new ListPoliciesResult
        {
            Policies = policies,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        });
    }
}
