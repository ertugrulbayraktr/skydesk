using System.Text.RegularExpressions;

namespace Support.Application.Services;

/// <summary>
/// Deterministic post-generation safety net for AI reply drafts. Prompt rules
/// alone are not a guarantee — this layer re-checks the output for the two
/// highest-impact failure modes and surfaces findings as risk flags so the
/// agent reviews before sending. It never blocks; humans stay in the loop.
/// </summary>
public static class DraftSafetyInspector
{
    // "refund of $250", "compensate you €100", "pay 500 TRY", "$250 refund" ...
    // Amount = "$250" / "€100" / "500 TRY" / "250 dollars" (symbol-first or digits-first)
    private const string Amount =
        @"((\$|€|£|₺|usd|eur|try|gbp)\s?\d[\d.,]*|\d[\d.,]*\s?(\$|€|£|₺|usd|eur|try|gbp|dollars?|euros?|lira))";

    private static readonly Regex MonetaryPromise = new(
        $@"(refund|compensat\w*|reimburs\w*|pay(?:ment)?|credit|iade|tazminat|öde\w*)\W{{0,40}}{Amount}|{Amount}\W{{0,40}}(refund|compensat\w*|reimburs\w*|credit|iade|tazminat)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private const int LeakWindowWords = 6;

    /// <summary>
    /// Returns additional risk flags for the draft. Empty list = no findings.
    /// </summary>
    public static List<string> Inspect(string draftText, IEnumerable<string> internalNoteContents)
    {
        var flags = new List<string>();

        if (MonetaryPromise.IsMatch(draftText))
        {
            flags.Add("SAFETY: Draft appears to promise a specific monetary amount — verify against policy before sending");
        }

        if (LeaksInternalContent(draftText, internalNoteContents))
        {
            flags.Add("SAFETY: Draft may contain internal note content — review carefully before sending");
        }

        return flags;
    }

    private static bool LeaksInternalContent(string draftText, IEnumerable<string> internalNoteContents)
    {
        var draftWords = Normalize(draftText);
        if (draftWords.Count < LeakWindowWords) return false;

        // Index draft word-windows for O(1) lookups per note window
        var draftWindows = new HashSet<string>();
        for (var i = 0; i <= draftWords.Count - LeakWindowWords; i++)
        {
            draftWindows.Add(string.Join(" ", draftWords.Skip(i).Take(LeakWindowWords)));
        }

        foreach (var note in internalNoteContents)
        {
            var noteWords = Normalize(note);
            for (var i = 0; i <= noteWords.Count - LeakWindowWords; i++)
            {
                if (draftWindows.Contains(string.Join(" ", noteWords.Skip(i).Take(LeakWindowWords))))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static List<string> Normalize(string text) =>
        Regex.Replace(text.ToLowerInvariant(), @"[^\p{L}\p{Nd}\s]", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
}
