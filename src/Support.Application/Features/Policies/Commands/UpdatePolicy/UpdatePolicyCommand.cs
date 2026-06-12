namespace Support.Application.Features.Policies.Commands.UpdatePolicy;

public class UpdatePolicyCommand
{
    public Guid PolicyId { get; set; }
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
}
