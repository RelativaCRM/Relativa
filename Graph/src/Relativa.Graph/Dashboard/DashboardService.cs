using Microsoft.EntityFrameworkCore;
using Relativa.Graph.Dashboard.Dto;
using Relativa.Graph.Data;
using Relativa.Graph.ML;

namespace Relativa.Graph.Dashboard;

public sealed class DashboardService(GraphQueryDbContext db, IMlScoringClient mlClient) : IDashboardService
{
    private const string ViewAnalytics    = "view_analytics";
    private const string ViewBasicStats   = "view_basic_stats";
    private const string ManageOrgSettings = "manage_org_settings";

    // ──────────────────────────────────────────────────────────────────────────
    // Permission helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the org-level permission set for the user.
    /// </summary>
    private async Task<HashSet<string>> GetOrgPermissionsAsync(
        int userId, int organizationId, CancellationToken ct)
    {
        return await db.UserRoleOrganizations
            .Where(uro => uro.UserId == userId
                          && uro.OrganizationId == organizationId
                          && !uro.IsArchived)
            .SelectMany(uro => uro.Role.RolePermissions.Select(rp => rp.Permission.Name))
            .ToHashSetAsync(ct);
    }

    /// <summary>
    /// Resolves workspace IDs the user can access and the access level string.
    /// Levels: "full_org" (manage_org_settings), "full" (view_analytics), "basic" (view_basic_stats).
    /// Throws ForbiddenAccessException if no access at all.
    /// </summary>
    private async Task<(List<int> wsIds, string accessLevel)> GetAccessContextAsync(
        int userId, int organizationId, CancellationToken ct)
    {
        var orgPerms = await GetOrgPermissionsAsync(userId, organizationId, ct);

        if (orgPerms.Contains(ManageOrgSettings))
        {
            var allWsIds = await db.Workspaces
                .Where(ws => ws.OrganizationId == organizationId && !ws.IsArchived)
                .Select(ws => ws.Id)
                .ToListAsync(ct);
            return (allWsIds, "full_org");
        }

        // view_analytics in any workspace of this org
        var analyticsWsIds = await db.UserRoleWorkspaces
            .Where(urw => urw.UserId == userId
                          && !urw.IsArchived
                          && !urw.Workspace.IsArchived
                          && urw.Workspace.OrganizationId == organizationId)
            .SelectMany(urw => urw.Role.RolePermissions
                .Where(rp => rp.Permission.Name == ViewAnalytics)
                .Select(_ => urw.WorkspaceId))
            .Distinct()
            .ToListAsync(ct);

        if (analyticsWsIds.Count > 0)
            return (analyticsWsIds, "full");

        // view_basic_stats in any workspace of this org
        var basicWsIds = await db.UserRoleWorkspaces
            .Where(urw => urw.UserId == userId
                          && !urw.IsArchived
                          && !urw.Workspace.IsArchived
                          && urw.Workspace.OrganizationId == organizationId)
            .SelectMany(urw => urw.Role.RolePermissions
                .Where(rp => rp.Permission.Name == ViewBasicStats)
                .Select(_ => urw.WorkspaceId))
            .Distinct()
            .ToListAsync(ct);

        if (basicWsIds.Count > 0)
            return (basicWsIds, "basic");

        throw new ForbiddenAccessException(
            "User does not have any dashboard access permissions in this organization.");
    }

    /// <summary>
    /// Returns workspace IDs for endpoints that require view_analytics (pipeline, risk, trends, top).
    /// Org owners bypass the workspace-level check.
    /// </summary>
    private async Task<List<int>> GetAnalyticsWorkspaceIdsAsync(
        int userId, int organizationId, CancellationToken ct)
    {
        var orgPerms = await GetOrgPermissionsAsync(userId, organizationId, ct);

        if (orgPerms.Contains(ManageOrgSettings))
        {
            var allWsIds = await db.Workspaces
                .Where(ws => ws.OrganizationId == organizationId && !ws.IsArchived)
                .Select(ws => ws.Id)
                .ToListAsync(ct);

            if (allWsIds.Count == 0)
                throw new ForbiddenAccessException("No workspaces found in this organization.");

            return allWsIds;
        }

        var wsIds = await db.UserRoleWorkspaces
            .Where(urw => urw.UserId == userId
                          && !urw.IsArchived
                          && !urw.Workspace.IsArchived
                          && urw.Workspace.OrganizationId == organizationId)
            .SelectMany(urw => urw.Role.RolePermissions
                .Where(rp => rp.Permission.Name == ViewAnalytics)
                .Select(_ => urw.WorkspaceId))
            .Distinct()
            .ToListAsync(ct);

        if (wsIds.Count == 0)
            throw new ForbiddenAccessException(
                "User does not have view_analytics permission in any workspace.");

        return wsIds;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Shared EAV helpers
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<List<int>> GetEntityIdsByTypeAsync(
        List<int> wsIds, string typeName, CancellationToken ct)
    {
        return await db.EntityWorkspaces
            .Where(ew => wsIds.Contains(ew.WorkspaceId)
                         && !ew.Entity.IsArchived
                         && ew.Entity.EntityType.Name == typeName)
            .Select(ew => ew.EntityId)
            .Distinct()
            .ToListAsync(ct);
    }

    private async Task<Dictionary<int, Dictionary<string, string?>>> GetPropertyValuesAsync(
        List<int> entityIds, IEnumerable<string> propertyNames, CancellationToken ct)
    {
        if (entityIds.Count == 0)
            return new Dictionary<int, Dictionary<string, string?>>();

        var propSet = propertyNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rows = await db.EntityPropertyValues
            .Where(epv => entityIds.Contains(epv.EntityId)
                          && propSet.Contains(epv.Property.Name))
            .Select(epv => new
            {
                epv.EntityId,
                epv.Property.Name,
                epv.ValueString,
                epv.ValueInt,
                epv.ValueDecimal,
                epv.ValueBool,
                epv.ValueDate
            })
            .ToListAsync(ct);

        var result = new Dictionary<int, Dictionary<string, string?>>();
        foreach (var row in rows)
        {
            if (!result.TryGetValue(row.EntityId, out var props))
            {
                props = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                result[row.EntityId] = props;
            }

            props[row.Name] = row.ValueString
                ?? row.ValueInt?.ToString()
                ?? row.ValueDecimal?.ToString()
                ?? (row.ValueBool.HasValue ? row.ValueBool.Value.ToString() : null)
                ?? row.ValueDate?.ToString("yyyy-MM-dd");
        }

        return result;
    }

    private async Task<Dictionary<int, decimal?>> GetDecimalPropertyAsync(
        List<int> entityIds, string propertyName, CancellationToken ct)
    {
        if (entityIds.Count == 0)
            return new Dictionary<int, decimal?>();

        return await db.EntityPropertyValues
            .Where(epv => entityIds.Contains(epv.EntityId)
                          && epv.Property.Name == propertyName
                          && epv.ValueDecimal != null)
            .ToDictionaryAsync(epv => epv.EntityId, epv => epv.ValueDecimal, ct);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Summary
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<DashboardSummaryDto> GetSummaryAsync(
        int userId, int organizationId, CancellationToken ct)
    {
        var (wsIds, accessLevel) = await GetAccessContextAsync(userId, organizationId, ct);
        var totalWorkspaces  = wsIds.Count;
        var activeWorkspaces = totalWorkspaces;

        if (accessLevel == "basic")
        {
            var basicDealIds   = await GetEntityIdsByTypeAsync(wsIds, "deal",   ct);
            var basicClientIds = await GetEntityIdsByTypeAsync(wsIds, "client", ct);
            return new DashboardSummaryDto(
                basicDealIds.Count, 0, 0, 0, 0, 0, 0,
                basicClientIds.Count, 0, 0, 0,
                totalWorkspaces, activeWorkspaces, "basic");
        }

        var dealIds   = await GetEntityIdsByTypeAsync(wsIds, "deal",   ct);
        var clientIds = await GetEntityIdsByTypeAsync(wsIds, "client", ct);
        var taskIds   = await GetEntityIdsByTypeAsync(wsIds, "task",   ct);

        var dealProps   = await GetPropertyValuesAsync(dealIds,   ["status", "deal_value", "expected_close"], ct);
        var clientProps = await GetPropertyValuesAsync(clientIds, ["client_status"],                          ct);
        var taskProps   = await GetPropertyValuesAsync(taskIds,   ["task_status", "due_date"],                ct);

        var today          = DateOnly.FromDateTime(DateTime.UtcNow);
        var thisMonthStart = new DateOnly(today.Year, today.Month, 1);
        var nextMonthStart = thisMonthStart.AddMonths(1);

        int totalDeals = dealIds.Count, wonDeals = 0, lostDeals = 0, openDeals = 0;
        int dealsClosingThisMonth = 0;
        decimal totalDealValue = 0;

        foreach (var id in dealIds)
        {
            var p      = dealProps.GetValueOrDefault(id);
            var status = p?.GetValueOrDefault("status") ?? "";
            var value  = decimal.TryParse(p?.GetValueOrDefault("deal_value"), out var dv) ? dv : 0m;
            totalDealValue += value;

            switch (status.ToLowerInvariant())
            {
                case "closed":  wonDeals++;  break;
                case "revoked": lostDeals++; break;
                default:        openDeals++; break;
            }

            var rawClose = p?.GetValueOrDefault("expected_close");
            if (rawClose is not null && DateOnly.TryParse(rawClose, out var closeDate)
                && closeDate >= thisMonthStart && closeDate < nextMonthStart)
                dealsClosingThisMonth++;
        }

        int totalClients = clientIds.Count, activeClients = 0;
        foreach (var id in clientIds)
        {
            if (string.Equals(clientProps.GetValueOrDefault(id)?.GetValueOrDefault("client_status"),
                "active", StringComparison.OrdinalIgnoreCase))
                activeClients++;
        }

        int tasksOverdue = 0;
        foreach (var id in taskIds)
        {
            var p          = taskProps.GetValueOrDefault(id);
            var taskStatus = p?.GetValueOrDefault("task_status") ?? "";
            if (string.Equals(taskStatus, "done", StringComparison.OrdinalIgnoreCase)) continue;
            var rawDue = p?.GetValueOrDefault("due_date");
            if (rawDue is not null && DateOnly.TryParse(rawDue, out var due) && due < today)
                tasksOverdue++;
        }

        var closedOrLost = wonDeals + lostDeals;
        var winRate      = closedOrLost > 0 ? (double)wonDeals / closedOrLost : 0.0;
        var avgDealSize  = totalDeals > 0 ? totalDealValue / totalDeals : 0m;

        return new DashboardSummaryDto(
            totalDeals, openDeals, totalDealValue,
            wonDeals, lostDeals, Math.Round(winRate, 4),
            Math.Round(avgDealSize, 2),
            totalClients, activeClients,
            dealsClosingThisMonth, tasksOverdue,
            totalWorkspaces, activeWorkspaces, accessLevel);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Pipeline
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<PipelineDto> GetPipelineAsync(
        int userId, int organizationId, CancellationToken ct)
    {
        var wsIds = await GetAnalyticsWorkspaceIdsAsync(userId, organizationId, ct);
        var dealIds = await GetEntityIdsByTypeAsync(wsIds, "deal", ct);

        var dealProps = await GetPropertyValuesAsync(dealIds,
            ["deal_stage", "status", "deal_value"], ct);

        var stageOrder = new[] { "Prospecting", "Qualification", "Proposal", "Negotiation" };
        var stageGroups = new Dictionary<string, (int count, decimal value)>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in stageOrder) stageGroups[s] = (0, 0);

        var statusBreakdown = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["opened"] = 0, ["pending"] = 0, ["closed"] = 0, ["revoked"] = 0
        };

        int wonDeals = 0, lostDeals = 0;

        foreach (var id in dealIds)
        {
            var p = dealProps.GetValueOrDefault(id);
            var stage = p?.GetValueOrDefault("deal_stage") ?? "";
            var status = p?.GetValueOrDefault("status") ?? "opened";
            var rawValue = p?.GetValueOrDefault("deal_value");
            var value = decimal.TryParse(rawValue, out var dv) ? dv : 0m;

            if (!string.IsNullOrEmpty(stage) && stageGroups.TryGetValue(stage, out var sg))
                stageGroups[stage] = (sg.count + 1, sg.value + value);

            var statusKey = status.ToLowerInvariant();
            if (statusBreakdown.ContainsKey(statusKey))
                statusBreakdown[statusKey]++;
            else
                statusBreakdown["opened"]++;

            if (statusKey == "closed") wonDeals++;
            if (statusKey == "revoked") lostDeals++;
        }

        var totalCount = dealIds.Count;
        var stages = stageOrder.Select(s =>
        {
            var (count, value) = stageGroups[s];
            return new PipelineStageDto(s, count, value,
                totalCount > 0 ? Math.Round((double)count / totalCount, 4) : 0.0);
        }).ToList();

        var closedOrLost = wonDeals + lostDeals;
        var conversionRate = closedOrLost > 0 ? Math.Round((double)wonDeals / closedOrLost, 4) : 0.0;

        // Avg days to close estimated as avg gap between today and expected_close for closed deals
        var closedDealIds = dealIds
            .Where(id => (dealProps.GetValueOrDefault(id)?.GetValueOrDefault("status") ?? "") == "closed")
            .ToList();
        var closedProps = await GetPropertyValuesAsync(closedDealIds, ["expected_close"], ct);
        var today2 = DateOnly.FromDateTime(DateTime.UtcNow);
        double totalDaysToClose = 0;
        int closedCount = 0;
        foreach (var id in closedDealIds)
        {
            var raw = closedProps.GetValueOrDefault(id)?.GetValueOrDefault("expected_close");
            if (raw is not null && DateOnly.TryParse(raw, out var closeDate))
            {
                var diff = Math.Abs((today2.ToDateTime(TimeOnly.MinValue) - closeDate.ToDateTime(TimeOnly.MinValue)).TotalDays);
                totalDaysToClose += diff;
                closedCount++;
            }
        }
        var avgDaysToClose = closedCount > 0 ? Math.Round(totalDaysToClose / closedCount, 1) : 0.0;

        return new PipelineDto(stages, statusBreakdown, conversionRate, avgDaysToClose);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Risk distribution
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<RiskDistributionDto> GetRiskDistributionAsync(
        int userId, int organizationId, CancellationToken ct)
    {
        var wsIds = await GetAnalyticsWorkspaceIdsAsync(userId, organizationId, ct);
        var dealIds = await GetEntityIdsByTypeAsync(wsIds, "deal", ct);

        var dealProps = await GetPropertyValuesAsync(dealIds,
            ["title", "deal_value", "status"], ct);

        // Only score open/pending deals
        var activeDealIds = dealIds
            .Where(id =>
            {
                var status = dealProps.GetValueOrDefault(id)?.GetValueOrDefault("status") ?? "";
                return status is "opened" or "pending" or "";
            })
            .ToList();

        var mlScores = await mlClient.ScoreBatchAsync(activeDealIds, ct);

        // deal → client name
        var dealClientRels = await db.EntityRelationships
            .Where(er => dealIds.Contains(er.SourceEntityId)
                         && er.RelationshipType.Name == "deal_client")
            .Select(er => new { er.SourceEntityId, er.TargetEntityId })
            .ToListAsync(ct);

        var clientIds = dealClientRels.Select(r => r.TargetEntityId).Distinct().ToList();
        var clientProps = await GetPropertyValuesAsync(clientIds,
            ["company_name", "name", "first_name"], ct);

        var dealClientMap = dealClientRels.ToDictionary(r => r.SourceEntityId, r => r.TargetEntityId);

        var items = new List<RiskItemDto>();
        var high = new List<(int id, decimal value)>();
        var medium = new List<(int id, decimal value)>();
        var low = new List<(int id, decimal value)>();

        foreach (var id in activeDealIds)
        {
            if (!mlScores.TryGetValue(id, out var score) || score.ClosureScore is null)
                continue;

            var p = dealProps.GetValueOrDefault(id);
            var title = p?.GetValueOrDefault("title") ?? $"Deal #{id}";
            var rawValue = p?.GetValueOrDefault("deal_value");
            var value = decimal.TryParse(rawValue, out var dv) ? dv : 0m;

            string? clientName = null;
            if (dealClientMap.TryGetValue(id, out var clientId))
            {
                var cp = clientProps.GetValueOrDefault(clientId);
                clientName = cp?.GetValueOrDefault("company_name")
                    ?? cp?.GetValueOrDefault("name")
                    ?? cp?.GetValueOrDefault("first_name");
            }

            var bucket = score.ClosureScore.Value < 0.4 ? "high"
                       : score.ClosureScore.Value <= 0.7 ? "medium"
                       : "low";

            items.Add(new RiskItemDto(id, title,
                Math.Round(score.ClosureScore.Value, 4), value, bucket, clientName));

            switch (bucket)
            {
                case "high":   high.Add((id, value)); break;
                case "medium": medium.Add((id, value)); break;
                case "low":    low.Add((id, value)); break;
            }
        }

        var totalScored = items.Count;

        RiskBucketDto MakeBucket(List<(int, decimal value)> list) => new(
            list.Count,
            list.Aggregate(0m, (s, r) => s + r.value),
            totalScored > 0 ? Math.Round((double)list.Count / totalScored, 4) : 0.0
        );

        var distribution = new Dictionary<string, RiskBucketDto>
        {
            ["high"]   = MakeBucket(high),
            ["medium"] = MakeBucket(medium),
            ["low"]    = MakeBucket(low),
        };

        return new RiskDistributionDto(distribution,
            items.OrderBy(i => i.Score).ToList());
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Trends (6 rolling months)
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<TrendsDto> GetTrendsAsync(
        int userId, int organizationId, CancellationToken ct)
    {
        var wsIds = await GetAnalyticsWorkspaceIdsAsync(userId, organizationId, ct);
        var dealIds = await GetEntityIdsByTypeAsync(wsIds, "deal", ct);

        var dealProps = await GetPropertyValuesAsync(dealIds,
            ["status", "deal_value", "expected_close"], ct);

        var now = DateTime.UtcNow;
        var months = Enumerable.Range(0, 6)
            .Select(i => now.AddMonths(-5 + i))
            .Select(d => new DateOnly(d.Year, d.Month, 1))
            .ToList();

        // Mutable state per month — all grouped by expected_close month
        var newDealsCounts  = months.ToDictionary(m => m, _ => 0);
        var closedWonCounts = months.ToDictionary(m => m, _ => 0);
        var closedLostCounts = months.ToDictionary(m => m, _ => 0);
        var wonRevenues     = months.ToDictionary(m => m, _ => 0m);
        var activeValues    = months.ToDictionary(m => m, _ => 0m);

        var windowStart = months[0];
        var windowEnd   = months[^1].AddMonths(1);

        foreach (var id in dealIds)
        {
            var p = dealProps.GetValueOrDefault(id);
            var status = (p?.GetValueOrDefault("status") ?? "").ToLowerInvariant();
            var rawValue = p?.GetValueOrDefault("deal_value");
            var value = decimal.TryParse(rawValue, out var dv) ? dv : 0m;

            // All metrics grouped by expected_close month
            var rawClose = p?.GetValueOrDefault("expected_close");
            if (rawClose is null || !DateOnly.TryParse(rawClose, out var closeDate))
                continue;

            var closeMonth = new DateOnly(closeDate.Year, closeDate.Month, 1);
            if (closeMonth < windowStart || closeMonth >= windowEnd)
                continue;

            // Every deal with an expected_close in this window is a "pipeline deal" for that month
            newDealsCounts[closeMonth]++;

            if (status == "closed")
            {
                closedWonCounts[closeMonth]++;
                wonRevenues[closeMonth] += value;
            }
            else if (status == "revoked")
            {
                closedLostCounts[closeMonth]++;
            }
            else
            {
                // opened / pending → contributes to active pipeline value
                activeValues[closeMonth] += value;
            }
        }

        var result = months.Select(m => new TrendsMonthDto(
            Label: m.ToString("MMM yyyy"),
            NewDeals: newDealsCounts[m],
            ClosedWon: closedWonCounts[m],
            ClosedLost: closedLostCounts[m],
            WonRevenue: wonRevenues[m],
            ActiveValue: activeValues[m]
        )).ToList();

        return new TrendsDto(result);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Top entities
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<TopEntitiesDto> GetTopEntitiesAsync(
        int userId, int organizationId, CancellationToken ct)
    {
        var wsIds = await GetAnalyticsWorkspaceIdsAsync(userId, organizationId, ct);
        var dealIds = await GetEntityIdsByTypeAsync(wsIds, "deal", ct);
        var clientIds = await GetEntityIdsByTypeAsync(wsIds, "client", ct);

        var dealProps = await GetPropertyValuesAsync(dealIds,
            ["title", "deal_value", "deal_stage", "priority", "status"], ct);
        var clientProps = await GetPropertyValuesAsync(clientIds,
            ["company_name", "name", "industry", "client_lifetime_value", "client_status"], ct);

        // Top 10 deals by value
        var top10DealIds = dealIds
            .Select(id =>
            {
                var rawV = dealProps.GetValueOrDefault(id)?.GetValueOrDefault("deal_value");
                var val = decimal.TryParse(rawV, out var dv) ? dv : 0m;
                return (id, val);
            })
            .OrderByDescending(x => x.val)
            .Take(10)
            .Select(x => x.id)
            .ToList();

        var mlScores = await mlClient.ScoreBatchAsync(top10DealIds, ct);

        var dealClientRels = await db.EntityRelationships
            .Where(er => top10DealIds.Contains(er.SourceEntityId)
                         && er.RelationshipType.Name == "deal_client")
            .Select(er => new { er.SourceEntityId, er.TargetEntityId })
            .ToListAsync(ct);

        var relClientIds = dealClientRels.Select(r => r.TargetEntityId).Distinct().ToList();
        var relClientProps = await GetPropertyValuesAsync(relClientIds, ["company_name", "name"], ct);
        var dealClientMap = dealClientRels.ToDictionary(r => r.SourceEntityId, r => r.TargetEntityId);

        var topDeals = top10DealIds.Select(id =>
        {
            var p = dealProps.GetValueOrDefault(id);
            var rawV = p?.GetValueOrDefault("deal_value");
            var val = decimal.TryParse(rawV, out var dv) ? dv : 0m;

            string? clientName = null;
            if (dealClientMap.TryGetValue(id, out var cid))
            {
                var cp = relClientProps.GetValueOrDefault(cid);
                clientName = cp?.GetValueOrDefault("company_name") ?? cp?.GetValueOrDefault("name");
            }

            var score = mlScores.TryGetValue(id, out var s) ? s.ClosureScore : null;

            return new TopDealDto(
                id,
                p?.GetValueOrDefault("title") ?? $"Deal #{id}",
                val,
                p?.GetValueOrDefault("deal_stage"),
                score.HasValue ? Math.Round(score.Value, 4) : null,
                clientName,
                p?.GetValueOrDefault("priority")
            );
        }).ToList();

        // Top 10 clients by lifetime value
        var top10ClientIds = clientIds
            .Select(id =>
            {
                var rawLtv = clientProps.GetValueOrDefault(id)?.GetValueOrDefault("client_lifetime_value");
                var ltv = decimal.TryParse(rawLtv, out var dv) ? dv : 0m;
                return (id, ltv);
            })
            .OrderByDescending(x => x.ltv)
            .Take(10)
            .Select(x => x.id)
            .ToList();

        // Active deal count per client + avg ML score
        var clientDealRels = await db.EntityRelationships
            .Where(er => dealIds.Contains(er.SourceEntityId)
                         && top10ClientIds.Contains(er.TargetEntityId)
                         && er.RelationshipType.Name == "deal_client")
            .Select(er => new { er.SourceEntityId, er.TargetEntityId })
            .ToListAsync(ct);

        // Get ML scores for all deals linked to top clients
        var allLinkedDealIds = clientDealRels.Select(r => r.SourceEntityId).Distinct().ToList();
        var allMlScores = await mlClient.ScoreBatchAsync(allLinkedDealIds, ct);

        var clientDealMap = clientDealRels
            .GroupBy(r => r.TargetEntityId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.SourceEntityId).ToList());

        var topClients = top10ClientIds.Select(id =>
        {
            var p = clientProps.GetValueOrDefault(id);
            var rawLtv = p?.GetValueOrDefault("client_lifetime_value");
            var ltv = decimal.TryParse(rawLtv, out var dv) ? dv : 0m;

            var linkedDeals = clientDealMap.GetValueOrDefault(id, []);
            var activeDeals = linkedDeals.Count(did =>
            {
                var s = dealProps.GetValueOrDefault(did)?.GetValueOrDefault("status") ?? "";
                return s is "opened" or "pending";
            });

            double? avgScore = null;
            var scores = linkedDeals
                .Where(did => allMlScores.TryGetValue(did, out var ms) && ms.ClosureScore.HasValue)
                .Select(did => allMlScores[did].ClosureScore!.Value)
                .ToList();
            if (scores.Count > 0)
                avgScore = Math.Round(scores.Average(), 4);

            var name = p?.GetValueOrDefault("company_name")
                ?? p?.GetValueOrDefault("name")
                ?? $"Client #{id}";

            return new TopClientDto(
                id, name, p?.GetValueOrDefault("industry"),
                ltv, activeDeals, avgScore);
        }).ToList();

        return new TopEntitiesDto(topDeals, topClients);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Workspaces comparison (org-level, requires manage_org_settings)
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<WorkspaceComparisonDto>> GetWorkspacesComparisonAsync(
        int userId, int organizationId, CancellationToken ct)
    {
        var orgPerms = await GetOrgPermissionsAsync(userId, organizationId, ct);
        if (!orgPerms.Contains(ManageOrgSettings))
            throw new ForbiddenAccessException("Requires manage_org_settings permission.");

        var workspaces = await db.Workspaces
            .Where(w => w.OrganizationId == organizationId && !w.IsArchived)
            .Select(w => new { w.Id, w.Name })
            .ToListAsync(ct);

        var result = new List<WorkspaceComparisonDto>();

        foreach (var ws in workspaces)
        {
            var wsList    = new List<int> { ws.Id };
            var dealIds   = await GetEntityIdsByTypeAsync(wsList, "deal",   ct);
            var clientIds = await GetEntityIdsByTypeAsync(wsList, "client", ct);
            var dealProps = await GetPropertyValuesAsync(dealIds, ["deal_value", "status", "deal_stage"], ct);

            var memberCount = await db.UserRoleWorkspaces
                .Where(urw => urw.WorkspaceId == ws.Id && !urw.IsArchived)
                .Select(urw => urw.UserId)
                .Distinct()
                .CountAsync(ct);

            int won = 0, lost = 0;
            decimal totalValue = 0m;
            var stageCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var id in dealIds)
            {
                var p      = dealProps.GetValueOrDefault(id);
                var status = (p?.GetValueOrDefault("status") ?? "").ToLowerInvariant();
                var value  = decimal.TryParse(p?.GetValueOrDefault("deal_value"), out var dv) ? dv : 0m;
                totalValue += value;

                if (status == "closed")  won++;
                else if (status == "revoked") lost++;

                var stage = p?.GetValueOrDefault("deal_stage") ?? "";
                if (!string.IsNullOrEmpty(stage))
                    stageCounts[stage] = stageCounts.GetValueOrDefault(stage) + 1;
            }

            var closedOrLost = won + lost;
            var winRate      = closedOrLost > 0 ? Math.Round((double)won / closedOrLost, 4) : 0.0;
            var topStage     = stageCounts.Count > 0
                ? stageCounts.MaxBy(kvp => kvp.Value).Key
                : "";

            result.Add(new WorkspaceComparisonDto(
                ws.Id, ws.Name, dealIds.Count, totalValue, winRate,
                clientIds.Count, memberCount, topStage));
        }

        return result;
    }
}
