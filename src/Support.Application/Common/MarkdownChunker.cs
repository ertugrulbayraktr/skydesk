using System.Text.RegularExpressions;

namespace Support.Application.Common;

/// <summary>
/// Splits Markdown policy content into retrieval-friendly chunks:
/// heading-based sections first, then token-bounded sub-chunks with overlap so
/// no chunk exceeds the embedding sweet spot and boundary context isn't lost.
/// </summary>
public static class MarkdownChunker
{
    // ~500 tokens per chunk, ~50 tokens of overlap. Tokens are approximated as
    // chars/4 (close enough for English/Turkish prose; exactness doesn't matter
    // for sizing, only consistency does).
    public const int MaxTokensPerChunk = 500;
    public const int OverlapTokens = 50;
    private const int CharsPerToken = 4;

    public record ChunkResult(string SectionTitle, string Content, int Index);

    public static List<ChunkResult> Chunk(string markdown)
    {
        var sections = SplitByHeadings(markdown);
        var chunks = new List<ChunkResult>();
        var index = 0;

        foreach (var (title, content) in sections)
        {
            var parts = SplitByTokenBudget(content);
            for (var i = 0; i < parts.Count; i++)
            {
                var chunkTitle = parts.Count == 1 ? title : $"{title} ({i + 1}/{parts.Count})";
                chunks.Add(new ChunkResult(chunkTitle, parts[i], index++));
            }
        }

        return chunks;
    }

    private static List<(string Title, string Content)> SplitByHeadings(string markdown)
    {
        var lines = markdown.Split('\n');
        var sections = new List<(string, string)>();
        var currentTitle = "Introduction";
        var currentLines = new List<string>();

        void Flush()
        {
            var content = string.Join("\n", currentLines).Trim();
            if (content.Length > 0)
            {
                sections.Add((currentTitle, content));
            }
            currentLines.Clear();
        }

        foreach (var line in lines)
        {
            var headingMatch = Regex.Match(line, @"^#+\s+(.+)$");
            if (headingMatch.Success)
            {
                Flush();
                currentTitle = headingMatch.Groups[1].Value.Trim();
            }
            else
            {
                currentLines.Add(line);
            }
        }

        Flush();
        return sections;
    }

    private static List<string> SplitByTokenBudget(string content)
    {
        var maxChars = MaxTokensPerChunk * CharsPerToken;
        if (content.Length <= maxChars)
        {
            return new List<string> { content };
        }

        // Split on sentence/line boundaries, then pack greedily with overlap.
        var units = Regex.Split(content, @"(?<=[.!?])\s+|\n+")
            .Where(u => u.Trim().Length > 0)
            .Select(u => u.Trim())
            .ToList();

        var overlapChars = OverlapTokens * CharsPerToken;
        var parts = new List<string>();
        var current = new List<string>();
        var currentLength = 0;

        foreach (var unit in units)
        {
            if (currentLength + unit.Length > maxChars && current.Count > 0)
            {
                parts.Add(string.Join(" ", current));

                // Carry the tail of the previous part as overlap
                var overlap = new List<string>();
                var overlapLength = 0;
                for (var i = current.Count - 1; i >= 0 && overlapLength < overlapChars; i--)
                {
                    overlap.Insert(0, current[i]);
                    overlapLength += current[i].Length;
                }
                current = overlap;
                currentLength = overlapLength;
            }

            current.Add(unit);
            currentLength += unit.Length;
        }

        if (current.Count > 0)
        {
            parts.Add(string.Join(" ", current));
        }

        return parts;
    }
}
