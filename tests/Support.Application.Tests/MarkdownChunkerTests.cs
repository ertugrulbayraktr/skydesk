using Support.Application.Common;
using Xunit;

namespace Support.Application.Tests;

public class MarkdownChunkerTests
{
    [Fact]
    public void Short_Sections_Produce_One_Chunk_Each()
    {
        var markdown = "# Policy\n\n## Refunds\nRefunds are processed in 7 days.\n\n## Baggage\nLost baggage must be reported within 24 hours.";

        var chunks = MarkdownChunker.Chunk(markdown);

        Assert.Equal(2, chunks.Count);
        Assert.Equal("Refunds", chunks[0].SectionTitle);
        Assert.Equal("Baggage", chunks[1].SectionTitle);
        Assert.Contains("7 days", chunks[0].Content);
    }

    [Fact]
    public void Content_Before_First_Heading_Becomes_Introduction()
    {
        var markdown = "This document describes our policies.\n\n## Refunds\nDetails here.";

        var chunks = MarkdownChunker.Chunk(markdown);

        Assert.Equal("Introduction", chunks[0].SectionTitle);
        Assert.Contains("describes our policies", chunks[0].Content);
    }

    [Fact]
    public void Long_Section_Is_Split_Into_SubChunks_Within_Token_Budget()
    {
        // Build a section far beyond the 500-token (~2000 char) budget
        var sentence = "Passengers affected by long delays may be entitled to meal vouchers and accommodation depending on the circumstances. ";
        var longBody = string.Concat(Enumerable.Repeat(sentence, 60)); // ~7000 chars
        var markdown = $"## Delay Compensation\n{longBody}";

        var chunks = MarkdownChunker.Chunk(markdown);

        Assert.True(chunks.Count > 1, "long section should split into multiple chunks");
        Assert.All(chunks, c =>
            Assert.True(c.Content.Length <= MarkdownChunker.MaxTokensPerChunk * 4 + 200,
                $"chunk exceeds token budget: {c.Content.Length} chars"));
        Assert.Equal($"Delay Compensation (1/{chunks.Count})", chunks[0].SectionTitle);
    }

    [Fact]
    public void SubChunks_Overlap_For_Boundary_Context()
    {
        var sentence = "Sentence number {0} carries unique marker token X{0}X for overlap testing purposes here. ";
        var body = string.Concat(Enumerable.Range(0, 80).Select(i => string.Format(sentence, i)));
        var markdown = $"## Overlap Section\n{body}";

        var chunks = MarkdownChunker.Chunk(markdown);

        Assert.True(chunks.Count > 1);
        // The tail of chunk N must reappear at the head of chunk N+1
        for (var i = 0; i < chunks.Count - 1; i++)
        {
            var tailWords = chunks[i].Content.Split(' ').TakeLast(5).ToArray();
            Assert.Contains(string.Join(" ", tailWords), chunks[i + 1].Content);
        }
    }

    [Fact]
    public void Chunk_Indexes_Are_Sequential()
    {
        var markdown = "## A\nshort\n## B\nshort\n## C\nshort";

        var chunks = MarkdownChunker.Chunk(markdown);

        Assert.Equal(new[] { 0, 1, 2 }, chunks.Select(c => c.Index).ToArray());
    }
}
