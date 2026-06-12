using System.Text.Json;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Support.Application.Interfaces;
using Support.Application.Models;
using Support.Domain.Enums;

namespace Support.Infrastructure.Services;

public class GeminiEmbeddingPolicySearchService : IPolicySearchService
{
    // One Gemini client per process (the service itself is scoped because of DbContext)
    private static Client? _sharedClient;
    private static readonly object ClientLock = new();

    private const string VectorCacheKey = "policy-chunk-vectors";

    private readonly IApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly Client _client;
    private readonly string _embeddingModel;
    private readonly ILogger<GeminiEmbeddingPolicySearchService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GeminiEmbeddingPolicySearchService(
        IApplicationDbContext context,
        IConfiguration configuration,
        IMemoryCache cache,
        ILogger<GeminiEmbeddingPolicySearchService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;

        var apiKey = configuration["Gemini:ApiKey"]
            ?? throw new InvalidOperationException("Gemini API key is not configured.");

        _embeddingModel = configuration["Gemini:EmbeddingModel"] ?? "text-embedding-004";

        lock (ClientLock)
        {
            _client = _sharedClient ??= new Client(apiKey: apiKey);
        }
    }

    public async Task<List<PolicyCitation>> SearchAsync(
        string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        var chunks = await _context.PolicyChunks
            .Include(c => c.PolicyDocument)
            .Where(c => c.PolicyDocument.Status == PolicyStatus.Published)
            .ToListAsync(cancellationToken);

        var indexedChunks = chunks.Where(c => c.EmbeddingVector != null).ToList();

        if (!indexedChunks.Any())
        {
            _logger.LogWarning("No indexed policy chunks found, falling back to keyword search");
            return KeywordFallbackSearch(chunks, query, topK);
        }

        try
        {
            var queryEmbedding = await GenerateEmbeddingAsync(query, cancellationToken);

            // Deserialized vectors are cached per chunk to avoid re-parsing JSON on every search
            var vectors = _cache.GetOrCreate(VectorCacheKey, entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromMinutes(30);
                return new Dictionary<Guid, float[]>();
            })!;

            // Hybrid ranking: dense (cosine) similarity catches paraphrases,
            // sparse (keyword) score anchors exact terminology — combined they
            // outperform either alone. Weights favor the semantic signal.
            const double semanticWeight = 0.7;
            const double keywordWeight = 0.3;
            const double minScore = 0.25;

            var scored = indexedChunks.Select(chunk =>
            {
                if (!vectors.TryGetValue(chunk.Id, out var chunkVector))
                {
                    chunkVector = JsonSerializer.Deserialize<float[]>(chunk.EmbeddingVector!, JsonOptions)!;
                    lock (vectors) { vectors[chunk.Id] = chunkVector; }
                }
                var semantic = CosineSimilarity(queryEmbedding, chunkVector);
                var keyword = Application.Common.KeywordScorer.Score(query, $"{chunk.SectionTitle}\n{chunk.Content}");
                return new { Chunk = chunk, Score = semanticWeight * semantic + keywordWeight * keyword };
            })
            .Where(x => x.Score > minScore)
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

            return scored;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Embedding search failed, falling back to keyword search");
            return KeywordFallbackSearch(chunks, query, topK);
        }
    }

    public async Task ReindexPolicyAsync(Guid policyDocumentId, CancellationToken cancellationToken = default)
    {
        var chunks = await _context.PolicyChunks
            .Where(c => c.PolicyDocumentId == policyDocumentId)
            .ToListAsync(cancellationToken);

        if (!chunks.Any())
        {
            _logger.LogWarning("No chunks found for policy {PolicyId}", policyDocumentId);
            return;
        }

        _logger.LogInformation("Generating embeddings for {Count} chunks of policy {PolicyId}",
            chunks.Count, policyDocumentId);

        foreach (var chunk in chunks)
        {
            try
            {
                var embedding = await GenerateEmbeddingAsync(chunk.Content, cancellationToken);
                var vectorJson = JsonSerializer.Serialize(embedding);
                chunk.SetEmbedding(vectorJson);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate embedding for chunk {ChunkId}", chunk.Id);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        _cache.Remove(VectorCacheKey); // invalidate cached vectors after reindex
        _logger.LogInformation("Embedding indexing completed for policy {PolicyId}", policyDocumentId);
    }

    private async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        var response = await _client.Models.EmbedContentAsync(
            model: _embeddingModel,
            contents: text,
            cancellationToken: cancellationToken);

        return response.Embeddings![0].Values!.Select(v => (float)v).ToArray();
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        double dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        var magnitude = Math.Sqrt(magA) * Math.Sqrt(magB);
        return magnitude == 0 ? 0 : dot / magnitude;
    }

    private static List<PolicyCitation> KeywordFallbackSearch(
        List<Domain.Entities.PolicyChunk> chunks, string query, int topK)
    {
        return chunks.Select(chunk => new
        {
            Chunk = chunk,
            Score = Application.Common.KeywordScorer.Score(query, $"{chunk.SectionTitle}\n{chunk.Content}")
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
}
