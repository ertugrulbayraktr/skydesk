using Microsoft.EntityFrameworkCore;
using Support.Application.Common;
using Support.Application.Interfaces;
using Support.Application.Models;
using Support.Domain.Enums;

namespace Support.Infrastructure.Services;

/// <summary>
/// Keyword-only search used when no Gemini API key is configured. Shares the
/// scorer with the hybrid embedding service so ranking behavior is consistent.
/// </summary>
public class PolicySearchService : IPolicySearchService
{
    private readonly IApplicationDbContext _context;

    public PolicySearchService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PolicyCitation>> SearchAsync(string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        var chunks = await _context.PolicyChunks
            .Include(c => c.PolicyDocument)
            .Where(c => c.PolicyDocument.Status == PolicyStatus.Published)
            .ToListAsync(cancellationToken);

        return chunks
            .Select(chunk => new
            {
                Chunk = chunk,
                Score = KeywordScorer.Score(query, $"{chunk.SectionTitle}\n{chunk.Content}")
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => new PolicyCitation
            {
                PolicyId = x.Chunk.PolicyDocumentId,
                SectionTitle = x.Chunk.SectionTitle,
                Content = x.Chunk.Content,
                Score = x.Score
            })
            .ToList();
    }

    public async Task ReindexPolicyAsync(Guid policyDocumentId, CancellationToken cancellationToken = default)
    {
        // Keyword search needs no separate index; chunks are stored at publish time
        await Task.CompletedTask;
    }
}
