using Support.Application.Services;
using Xunit;

namespace Support.Application.Tests;

public class DraftSafetyInspectorTests
{
    [Theory]
    [InlineData("We will refund $250 to your card within 7 days.")]
    [InlineData("You will be compensated €600 under EU261.")]
    [InlineData("We can pay 500 TRY as a goodwill gesture.")]
    [InlineData("A $100 refund has been approved for you.")]
    public void Flags_Specific_Monetary_Promises(string draft)
    {
        var flags = DraftSafetyInspector.Inspect(draft, Array.Empty<string>());

        Assert.Contains(flags, f => f.Contains("monetary amount", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("You may be eligible for compensation; our team will review your case.")]
    [InlineData("Refunds are processed to the original payment method.")]
    public void Does_Not_Flag_Safe_Compensation_Language(string draft)
    {
        var flags = DraftSafetyInspector.Inspect(draft, Array.Empty<string>());

        Assert.DoesNotContain(flags, f => f.Contains("monetary amount", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Flags_Internal_Note_Leakage()
    {
        var internalNote = "Customer credit card verification failed twice do not offer credit payment options";
        var draft = "Dear customer, please note that your credit card verification failed twice do not worry, we will assist you.";

        var flags = DraftSafetyInspector.Inspect(draft, new[] { internalNote });

        Assert.Contains(flags, f => f.Contains("internal note", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Does_Not_Flag_When_No_Overlap_With_Internal_Notes()
    {
        var internalNote = "Escalate to supervisor if the passenger mentions legal action again";
        var draft = "Dear customer, your refund request has been received and is being reviewed by our team.";

        var flags = DraftSafetyInspector.Inspect(draft, new[] { internalNote });

        Assert.Empty(flags);
    }

    [Fact]
    public void Clean_Draft_Produces_No_Flags()
    {
        var draft = "Thank you for contacting us. We have located your baggage and it will be delivered tomorrow.";

        var flags = DraftSafetyInspector.Inspect(draft, Array.Empty<string>());

        Assert.Empty(flags);
    }
}
