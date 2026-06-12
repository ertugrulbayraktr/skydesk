namespace Support.Application.Features.Policies.Commands.ArchivePolicy;

public class ArchivePolicyCommand
{
    public Guid PolicyId { get; set; }
    public Guid ArchivedByUserId { get; set; }
}
