using Support.Application.Common;
using Xunit;

namespace Support.Application.Tests;

/// <summary>
/// Retrieval quality evaluation against a golden set: for each realistic
/// passenger question, the expected policy section must appear in the top-5
/// keyword-ranked chunks. Guards against regressions in chunking or scoring —
/// the part of a RAG system that silently degrades without measurement.
/// </summary>
public class RetrievalEvalTests
{
    // Same markdown the application seeds (kept in sync intentionally — if the
    // seed policy changes, the golden set must be revisited).
    private const string PolicyMarkdown = @"# Flight Cancellation and Refund Policy

## Overview
This document outlines our policies regarding flight cancellations and refunds.

## Cancellation by Passenger

### Refundable Tickets
- Full refund minus cancellation fee if cancelled 24+ hours before departure
- Cancellation fee: $50 for domestic, $100 for international
- Refund processed within 7-10 business days

### Non-Refundable Tickets
- No refund for cancellation
- May receive travel credit valid for 1 year
- $150 rebooking fee applies

## Cancellation by Airline

### Flight Cancelled
- Full refund regardless of ticket type
- Alternative flight offered at no extra cost
- Compensation for delays over 3 hours: $200-$600 depending on distance

### Flight Delayed
- Delay 2-3 hours: Meal vouchers provided
- Delay 3+ hours: Hotel accommodation if overnight
- Delay 4+ hours: Eligible for compensation

## Medical Emergencies
- Documentation required from licensed medical professional
- Refund or rebooking without fee
- Must provide notice within 48 hours of incident

## Weather-Related Cancellations
- Considered extraordinary circumstances
- Alternative flights offered
- No monetary compensation required by law
- May provide vouchers at airline's discretion

## How to Request Refund
1. Contact customer support via phone or email
2. Provide booking reference (PNR) and passenger details
3. Submit required documentation if applicable
4. Refund processed to original payment method

## Processing Time
- Credit card refunds: 7-10 business days
- Bank transfer: 10-15 business days
- Travel credit: Issued immediately";

    // (question, expected section-title prefix)
    private static readonly (string Query, string ExpectedSection)[] GoldenSet =
    {
        ("I want to cancel my refundable ticket, what fee applies?", "Refundable Tickets"),
        ("Can I get my money back on a non-refundable fare?", "Non-Refundable Tickets"),
        ("The airline cancelled my flight, am I entitled to compensation?", "Flight Cancelled"),
        ("My flight is delayed 4 hours, do I get meal vouchers or a hotel?", "Flight Delayed"),
        ("I have a medical emergency and cannot fly, can I rebook without a fee?", "Medical Emergencies"),
        ("Flight cancelled due to storm and bad weather, what are my rights?", "Weather-Related Cancellations"),
        ("How do I request a refund, what steps should I follow?", "How to Request Refund"),
        ("How long does a credit card refund take to process?", "Processing Time"),
        ("What is the rebooking fee for non-refundable tickets?", "Non-Refundable Tickets"),
        ("Will I get an alternative flight at no extra cost if my flight is cancelled?", "Flight Cancelled"),
        ("Is there hotel accommodation for an overnight delay?", "Flight Delayed"),
        ("Travel credit validity after cancelling a non-refundable booking", "Non-Refundable Tickets"),
    };

    private const int TopK = 5;
    private const double MinRecallAtK = 0.8;

    private static List<(string Title, double Score)> Rank(List<MarkdownChunker.ChunkResult> chunks, string query) =>
        chunks
            .Select(c => (c.SectionTitle, Score: KeywordScorer.Score(query, $"{c.SectionTitle}\n{c.Content}")))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(TopK)
            .ToList();

    [Fact]
    public void Recall_At_5_Meets_Threshold_On_Golden_Set()
    {
        var chunks = MarkdownChunker.Chunk(PolicyMarkdown);
        var hits = 0;
        var misses = new List<string>();

        foreach (var (query, expected) in GoldenSet)
        {
            var results = Rank(chunks, query);
            if (results.Any(r => r.Title.StartsWith(expected, StringComparison.OrdinalIgnoreCase)))
            {
                hits++;
            }
            else
            {
                misses.Add($"'{query}' → expected '{expected}', got [{string.Join(", ", results.Select(r => r.Title))}]");
            }
        }

        var recall = (double)hits / GoldenSet.Length;
        Assert.True(recall >= MinRecallAtK,
            $"recall@{TopK} = {recall:P0} (< {MinRecallAtK:P0}). Misses:\n{string.Join("\n", misses)}");
    }

    [Fact]
    public void Every_Query_Returns_At_Least_One_Result()
    {
        var chunks = MarkdownChunker.Chunk(PolicyMarkdown);

        foreach (var (query, _) in GoldenSet)
        {
            var results = Rank(chunks, query);
            Assert.True(results.Count > 0, $"no results for query: '{query}'");
        }
    }
}
