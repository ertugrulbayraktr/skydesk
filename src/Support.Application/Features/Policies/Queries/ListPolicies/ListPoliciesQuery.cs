using Support.Domain.Enums;

namespace Support.Application.Features.Policies.Queries.ListPolicies;

public class ListPoliciesQuery
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class ListPoliciesResult
{
    public List<PolicySummaryDto> Policies { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}

public class PolicySummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public PolicyStatus Status { get; set; }
    public int Version { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int ChunkCount { get; set; }
}
