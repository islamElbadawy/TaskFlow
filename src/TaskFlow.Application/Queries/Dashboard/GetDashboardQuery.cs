using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs.Dashboard;

namespace TaskFlow.Application.Queries.Dashboard;

/// <summary>GET /api/dashboard: task and notification counts for the caller (from the token).</summary>
public class GetDashboardQuery : IQuery<Result<DashboardDto>>
{
}
