namespace Support.Application.Features.Tickets.Queries.GetDashboardStats;

public class GetDashboardStatsQuery
{
}

public class DashboardStatsDto
{
    public int TotalTickets { get; set; }
    public int OpenTickets { get; set; }
    public int SlaAtRiskCount { get; set; }
    public int SlaBreachedCount { get; set; }
    public double? AvgFirstResponseMinutes { get; set; }
    public int DraftFeedbackAccepted { get; set; }
    public int DraftFeedbackRejected { get; set; }
    public Dictionary<string, int> ByState { get; set; } = new();
    public Dictionary<string, int> ByCategory { get; set; } = new();
    public Dictionary<string, int> ByPriority { get; set; } = new();
    public List<DailyCountDto> Last7Days { get; set; } = new();
}

public class DailyCountDto
{
    public string Date { get; set; } = null!; // yyyy-MM-dd
    public int Count { get; set; }
}
