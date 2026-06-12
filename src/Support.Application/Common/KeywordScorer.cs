namespace Support.Application.Common;

/// <summary>
/// Lightweight keyword relevance scorer shared by the mock search service and
/// the hybrid (semantic + keyword) ranking in the embedding search service.
/// Returns a score normalized to 0..1.
/// </summary>
public static class KeywordScorer
{
    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "is", "are", "was", "were", "to", "of", "in", "on", "for",
        "and", "or", "my", "i", "me", "you", "your", "it", "this", "that", "with",
        "ve", "bir", "bu", "şu", "için", "ile", "mi", "mı", "da", "de",
    };

    public static List<string> Tokenize(string text) =>
        text.ToLowerInvariant()
            .Split(new[] { ' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?', '(', ')', '"', '\'' },
                StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 1 && !Stopwords.Contains(t))
            .ToList();

    /// <summary>
    /// Fraction of distinct query terms that occur in the document (0..1),
    /// weighted slightly by repeat occurrences.
    /// </summary>
    public static double Score(string query, string document)
    {
        var queryTerms = Tokenize(query).Distinct().ToList();
        if (queryTerms.Count == 0) return 0;

        var docLower = document.ToLowerInvariant();
        var matched = 0.0;

        foreach (var term in queryTerms)
        {
            var occurrences = CountOccurrences(docLower, term);
            if (occurrences > 0)
            {
                // First occurrence counts fully; repeats add diminishing weight
                matched += Math.Min(1.0 + (occurrences - 1) * 0.1, 1.5);
            }
        }

        return Math.Min(matched / queryTerms.Count, 1.0);
    }

    private static int CountOccurrences(string text, string term)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(term, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += term.Length;
        }
        return count;
    }
}
