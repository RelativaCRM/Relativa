using Microsoft.EntityFrameworkCore;
using Relativa.Graph.Dashboard.Dto;
using Relativa.Graph.Data;
using Relativa.Graph.ML;

namespace Relativa.Graph.Dashboard;

public sealed class WorkspaceDashboardService(
    GraphQueryDbContext db,
    IMlScoringClient mlClient,
    IMlRecalculationClient mlRecalc)
    : IWorkspaceDashboardService
{
    private const string ViewAnalytics   = "view_analytics";
    private const string ViewBasicStats  = "view_basic_stats";
    private const string ViewTeamAnalytics = "view_team_analytics";

    // ──────────────────────────────────────────────────────────────────────────
    // Permission helpers
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<HashSet<string>> GetWorkspacePermissionsAsync(
        int userId, int workspaceId, CancellationToken ct)
    {
        return await db.UserRoleWorkspaces
            .Where(urw => urw.UserId == userId
                          && urw.WorkspaceId == workspaceId
                          && !urw.IsArchived)
            .SelectMany(urw => urw.Role.RolePermissions.Select(rp => rp.Permission.Name))
            .ToHashSetAsync(ct);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Shared EAV helpers (workspace-scoped)
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<List<int>> GetEntityIdsAsync(
        int workspaceId, string typeName, CancellationToken ct)
    {
        return await db.EntityWorkspaces
            .Where(ew => ew.WorkspaceId == workspaceId
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

    // ──────────────────────────────────────────────────────────────────────────
    // Summary
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<WorkspaceSummaryDto> GetSummaryAsync(
        int userId, int workspaceId, CancellationToken ct)
    {
        var perms = await GetWorkspacePermissionsAsync(userId, workspaceId, ct);
        bool hasFull  = perms.Contains(ViewAnalytics);
        bool hasBasic = perms.Contains(ViewBasicStats);

        if (!hasFull && !hasBasic)
            throw new ForbiddenAccessException(
                "Requires view_analytics or view_basic_stats permission.");

        var workspace = await db.Workspaces
            .Where(w => w.Id == workspaceId && !w.IsArchived)
            .Select(w => new { w.Id, w.Name })
            .FirstOrDefaultAsync(ct)
            ?? throw new WorkspaceNotFoundException(workspaceId);

        var dealIds   = await GetEntityIdsAsync(workspaceId, "deal",   ct);
        var clientIds = await GetEntityIdsAsync(workspaceId, "client", ct);

        var memberCount = await db.UserRoleWorkspaces
            .Where(urw => urw.WorkspaceId == workspaceId && !urw.IsArchived)
            .Select(urw => urw.UserId)
            .Distinct()
            .CountAsync(ct);

        if (!hasFull)
        {
            var basicDealProps   = await GetPropertyValuesAsync(dealIds,   ["status"],        ct);
            var basicClientProps = await GetPropertyValuesAsync(clientIds, ["client_status"], ct);

            int wonDeals = 0, lostDeals = 0, openDeals = 0;
            foreach (var id in dealIds)
            {
                switch ((basicDealProps.GetValueOrDefault(id)?.GetValueOrDefault("status") ?? "").ToLowerInvariant())
                {
                    case "closed":  wonDeals++;  break;
                    case "revoked": lostDeals++; break;
                    default:        openDeals++; break;
                }
            }

            var activeClients = clientIds.Count(id => string.Equals(
                basicClientProps.GetValueOrDefault(id)?.GetValueOrDefault("client_status"),
                "active", StringComparison.OrdinalIgnoreCase));

            return new WorkspaceSummaryDto(
                workspace.Id, workspace.Name,
                dealIds.Count, openDeals, wonDeals, lostDeals,
                null, null, null,
                clientIds.Count, activeClients,
                null, null, memberCount, "basic");
        }

        // Full analytics
        var today          = DateOnly.FromDateTime(DateTime.UtcNow);
        var thisMonthStart = new DateOnly(today.Year, today.Month, 1);
        var nextMonthStart = thisMonthStart.AddMonths(1);

        var dealProps   = await GetPropertyValuesAsync(dealIds,   ["status", "deal_value", "expected_close"], ct);
        var clientProps = await GetPropertyValuesAsync(clientIds, ["client_status"],                          ct);
        var taskIds     = await GetEntityIdsAsync(workspaceId, "task", ct);
        var taskProps   = await GetPropertyValuesAsync(taskIds, ["task_status", "due_date"], ct);

        int totalDeals = dealIds.Count, wonDeals2 = 0, lostDeals2 = 0, openDeals2 = 0, closingThisMonth = 0;
        decimal totalDealValue = 0m;

        foreach (var id in dealIds)
        {
            var p      = dealProps.GetValueOrDefault(id);
            var status = (p?.GetValueOrDefault("status") ?? "").ToLowerInvariant();
            var value  = decimal.TryParse(p?.GetValueOrDefault("deal_value"), out var dv) ? dv : 0m;
            totalDealValue += value;

            switch (status)
            {
                case "closed":  wonDeals2++;  break;
                case "revoked": lostDeals2++; break;
                default:        openDeals2++; break;
            }

            var rawClose = p?.GetValueOrDefault("expected_close");
            if (rawClose is not null && DateOnly.TryParse(rawClose, out var closeDate)
                && closeDate >= thisMonthStart && closeDate < nextMonthStart)
                closingThisMonth++;
        }

        int totalClients2 = clientIds.Count, activeClients2 = 0;
        foreach (var id in clientIds)
        {
            if (string.Equals(clientProps.GetValueOrDefault(id)?.GetValueOrDefault("client_status"),
                "active", StringComparison.OrdinalIgnoreCase))
                activeClients2++;
        }

        int tasksOverdue = 0;
        foreach (var id in taskIds)
        {
            var p = taskProps.GetValueOrDefault(id);
            if (string.Equals(p?.GetValueOrDefault("task_status"), "done", StringComparison.OrdinalIgnoreCase))
                continue;
            var rawDue = p?.GetValueOrDefault("due_date");
            if (rawDue is not null && DateOnly.TryParse(rawDue, out var due) && due < today)
                tasksOverdue++;
        }

        var closedOrLost = wonDeals2 + lostDeals2;
        var winRate      = closedOrLost > 0 ? Math.Round((double)wonDeals2 / closedOrLost, 4) : 0.0;
        var avgDealSize  = totalDeals > 0 ? Math.Round(totalDealValue / totalDeals, 2) : 0m;

        return new WorkspaceSummaryDto(
            workspace.Id, workspace.Name,
            totalDeals, openDeals2, wonDeals2, lostDeals2,
            totalDealValue, winRate, avgDealSize,
            totalClients2, activeClients2,
            closingThisMonth, tasksOverdue,
            memberCount, "full");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Pipeline
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<PipelineDto> GetPipelineAsync(
        int userId, int workspaceId, CancellationToken ct)
    {
        var perms = await GetWorkspacePermissionsAsync(userId, workspaceId, ct);
        if (!perms.Contains(ViewAnalytics))
            throw new ForbiddenAccessException("Requires view_analytics permission.");

        var dealIds   = await GetEntityIdsAsync(workspaceId, "deal", ct);
        var dealProps = await GetPropertyValuesAsync(dealIds, ["deal_stage", "status", "deal_value"], ct);

        var stageOrder  = new[] { "Prospecting", "Qualification", "Proposal", "Negotiation" };
        var stageGroups = new Dictionary<string, (int count, decimal value)>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in stageOrder) stageGroups[s] = (0, 0);

        var statusBreakdown = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        { ["opened"] = 0, ["pending"] = 0, ["closed"] = 0, ["revoked"] = 0 };

        int wonDeals = 0, lostDeals = 0;

        foreach (var id in dealIds)
        {
            var p      = dealProps.GetValueOrDefault(id);
            var stage  = p?.GetValueOrDefault("deal_stage") ?? "";
            var status = p?.GetValueOrDefault("status") ?? "opened";
            var value  = decimal.TryParse(p?.GetValueOrDefault("deal_value"), out var dv) ? dv : 0m;

            if (!string.IsNullOrEmpty(stage) && stageGroups.TryGetValue(stage, out var sg))
                stageGroups[stage] = (sg.count + 1, sg.value + value);

            var sk = status.ToLowerInvariant();
            if (statusBreakdown.ContainsKey(sk)) statusBreakdown[sk]++;
            else statusBreakdown["opened"]++;

            if (sk == "closed")  wonDeals++;
            if (sk == "revoked") lostDeals++;
        }

        var totalCount = dealIds.Count;
        var stages = stageOrder.Select(s =>
        {
            var (count, value) = stageGroups[s];
            return new PipelineStageDto(s, count, value,
                totalCount > 0 ? Math.Round((double)count / totalCount, 4) : 0.0);
        }).ToList();

        var closedOrLost     = wonDeals + lostDeals;
        var conversionRate   = closedOrLost > 0 ? Math.Round((double)wonDeals / closedOrLost, 4) : 0.0;
        var closedDealIds    = dealIds.Where(id => (dealProps.GetValueOrDefault(id)?.GetValueOrDefault("status") ?? "") == "closed").ToList();
        var closedProps      = await GetPropertyValuesAsync(closedDealIds, ["expected_close"], ct);
        var today2           = DateOnly.FromDateTime(DateTime.UtcNow);
        double totalDaysToClose = 0; int closedCount = 0;

        foreach (var id in closedDealIds)
        {
            var raw = closedProps.GetValueOrDefault(id)?.GetValueOrDefault("expected_close");
            if (raw is not null && DateOnly.TryParse(raw, out var closeDate))
            {
                totalDaysToClose += Math.Abs((today2.ToDateTime(TimeOnly.MinValue) - closeDate.ToDateTime(TimeOnly.MinValue)).TotalDays);
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
        int userId, int workspaceId, CancellationToken ct)
    {
        var perms = await GetWorkspacePermissionsAsync(userId, workspaceId, ct);
        if (!perms.Contains(ViewAnalytics))
            throw new ForbiddenAccessException("Requires view_analytics permission.");

        var dealIds   = await GetEntityIdsAsync(workspaceId, "deal", ct);
        var dealProps = await GetPropertyValuesAsync(dealIds, ["title", "deal_value", "status"], ct);

        var activeDealIds = dealIds
            .Where(id => (dealProps.GetValueOrDefault(id)?.GetValueOrDefault("status") ?? "") is "opened" or "pending" or "")
            .ToList();

        var mlScores = await mlClient.ScoreBatchAsync(activeDealIds, ct);

        // Enqueue only deals that have no score yet so already-calculated deals are not re-processed.
        var unscoredIds = activeDealIds
            .Where(id => !mlScores.TryGetValue(id, out var s) || s.ClosureScore is null)
            .ToList();
        if (unscoredIds.Count > 0)
        {
            await EnsureDealAnalysisEntitiesAsync(unscoredIds, workspaceId, userId, ct);
            _ = mlRecalc.EnqueueAsync(unscoredIds, userId, workspaceId);
        }

        var dealClientRels = await db.EntityRelationships
            .Where(er => dealIds.Contains(er.SourceEntityId) && er.RelationshipType.Name == "deal_client")
            .Select(er => new { er.SourceEntityId, er.TargetEntityId })
            .ToListAsync(ct);

        var clientIds  = dealClientRels.Select(r => r.TargetEntityId).Distinct().ToList();
        var clientProps = await GetPropertyValuesAsync(clientIds, ["company_name", "name", "first_name"], ct);
        var dealClientMap = dealClientRels.ToDictionary(r => r.SourceEntityId, r => r.TargetEntityId);

        var items  = new List<RiskItemDto>();
        var high   = new List<(int id, decimal value)>();
        var medium = new List<(int id, decimal value)>();
        var low    = new List<(int id, decimal value)>();

        foreach (var id in activeDealIds)
        {
            if (!mlScores.TryGetValue(id, out var score) || score.ClosureScore is null) continue;

            var p     = dealProps.GetValueOrDefault(id);
            var title = p?.GetValueOrDefault("title") ?? $"Deal #{id}";
            var value = decimal.TryParse(p?.GetValueOrDefault("deal_value"), out var dv) ? dv : 0m;

            string? clientName = null;
            if (dealClientMap.TryGetValue(id, out var cid))
            {
                var cp = clientProps.GetValueOrDefault(cid);
                clientName = cp?.GetValueOrDefault("company_name") ?? cp?.GetValueOrDefault("name") ?? cp?.GetValueOrDefault("first_name");
            }

            var bucket = score.ClosureScore.Value < 0.4 ? "high"
                       : score.ClosureScore.Value <= 0.7 ? "medium"
                       : "low";

            items.Add(new RiskItemDto(id, title, Math.Round(score.ClosureScore.Value, 4), value, bucket, clientName));

            switch (bucket)
            {
                case "high":   high.Add((id, value));   break;
                case "medium": medium.Add((id, value)); break;
                case "low":    low.Add((id, value));    break;
            }
        }

        var totalScored = items.Count;
        RiskBucketDto MakeBucket(List<(int, decimal value)> list) => new(
            list.Count,
            list.Aggregate(0m, (s, r) => s + r.value),
            totalScored > 0 ? Math.Round((double)list.Count / totalScored, 4) : 0.0);

        return new RiskDistributionDto(
            new Dictionary<string, RiskBucketDto>
            {
                ["high"]   = MakeBucket(high),
                ["medium"] = MakeBucket(medium),
                ["low"]    = MakeBucket(low),
            },
            items.OrderBy(i => i.Score).ToList());
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Trends (6 rolling months)
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<TrendsDto> GetTrendsAsync(
        int userId, int workspaceId, CancellationToken ct)
    {
        var perms = await GetWorkspacePermissionsAsync(userId, workspaceId, ct);
        if (!perms.Contains(ViewAnalytics))
            throw new ForbiddenAccessException("Requires view_analytics permission.");

        var dealIds   = await GetEntityIdsAsync(workspaceId, "deal", ct);
        var dealProps = await GetPropertyValuesAsync(dealIds, ["status", "deal_value", "expected_close"], ct);

        var now    = DateTime.UtcNow;
        var months = Enumerable.Range(0, 6)
            .Select(i => now.AddMonths(-5 + i))
            .Select(d => new DateOnly(d.Year, d.Month, 1))
            .ToList();

        var newDealsCounts   = months.ToDictionary(m => m, _ => 0);
        var closedWonCounts  = months.ToDictionary(m => m, _ => 0);
        var closedLostCounts = months.ToDictionary(m => m, _ => 0);
        var wonRevenues      = months.ToDictionary(m => m, _ => 0m);
        var activeValues     = months.ToDictionary(m => m, _ => 0m);

        var windowStart = months[0];
        var windowEnd   = months[^1].AddMonths(1);

        foreach (var id in dealIds)
        {
            var p      = dealProps.GetValueOrDefault(id);
            var status = (p?.GetValueOrDefault("status") ?? "").ToLowerInvariant();
            var value  = decimal.TryParse(p?.GetValueOrDefault("deal_value"), out var dv) ? dv : 0m;

            var rawClose = p?.GetValueOrDefault("expected_close");
            if (rawClose is null || !DateOnly.TryParse(rawClose, out var closeDate)) continue;

            var closeMonth = new DateOnly(closeDate.Year, closeDate.Month, 1);
            if (closeMonth < windowStart || closeMonth >= windowEnd) continue;

            newDealsCounts[closeMonth]++;

            if (status == "closed")  { closedWonCounts[closeMonth]++;  wonRevenues[closeMonth] += value; }
            else if (status == "revoked") closedLostCounts[closeMonth]++;
            else activeValues[closeMonth] += value;
        }

        return new TrendsDto(months.Select(m => new TrendsMonthDto(
            m.ToString("MMM yyyy"),
            newDealsCounts[m], closedWonCounts[m], closedLostCounts[m],
            wonRevenues[m], activeValues[m])).ToList());
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Top entities
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<TopEntitiesDto> GetTopEntitiesAsync(
        int userId, int workspaceId, CancellationToken ct)
    {
        var perms = await GetWorkspacePermissionsAsync(userId, workspaceId, ct);
        if (!perms.Contains(ViewAnalytics))
            throw new ForbiddenAccessException("Requires view_analytics permission.");

        var dealIds   = await GetEntityIdsAsync(workspaceId, "deal",   ct);
        var clientIds = await GetEntityIdsAsync(workspaceId, "client", ct);

        var dealProps   = await GetPropertyValuesAsync(dealIds,   ["title", "deal_value", "deal_stage", "priority", "status"], ct);
        var clientProps = await GetPropertyValuesAsync(clientIds, ["company_name", "name", "industry", "client_status"], ct);

        var top10DealIds = dealIds
            .Select(id => (id, val: decimal.TryParse(dealProps.GetValueOrDefault(id)?.GetValueOrDefault("deal_value"), out var dv) ? dv : 0m))
            .OrderByDescending(x => x.val).Take(10).Select(x => x.id).ToList();

        var mlScores = await mlClient.ScoreBatchAsync(top10DealIds, ct);

        var dealClientRels = await db.EntityRelationships
            .Where(er => top10DealIds.Contains(er.SourceEntityId) && er.RelationshipType.Name == "deal_client")
            .Select(er => new { er.SourceEntityId, er.TargetEntityId })
            .ToListAsync(ct);

        var relClientIds  = dealClientRels.Select(r => r.TargetEntityId).Distinct().ToList();
        var relClientProps = await GetPropertyValuesAsync(relClientIds, ["company_name", "name"], ct);
        var dealClientMap = dealClientRels.ToDictionary(r => r.SourceEntityId, r => r.TargetEntityId);

        var topDeals = top10DealIds.Select(id =>
        {
            var p    = dealProps.GetValueOrDefault(id);
            var val  = decimal.TryParse(p?.GetValueOrDefault("deal_value"), out var dv) ? dv : 0m;
            string? clientName = null;
            if (dealClientMap.TryGetValue(id, out var cid))
            {
                var cp = relClientProps.GetValueOrDefault(cid);
                clientName = cp?.GetValueOrDefault("company_name") ?? cp?.GetValueOrDefault("name");
            }
            var score = mlScores.TryGetValue(id, out var s) ? s.ClosureScore : null;
            return new TopDealDto(id, p?.GetValueOrDefault("title") ?? $"Deal #{id}",
                val, p?.GetValueOrDefault("deal_stage"),
                score.HasValue ? Math.Round(score.Value, 4) : null,
                clientName, p?.GetValueOrDefault("priority"));
        }).ToList();

        // Compute LTV per client as sum of closed deal values
        var allClientDealRels = await db.EntityRelationships
            .Where(er => dealIds.Contains(er.SourceEntityId)
                         && clientIds.Contains(er.TargetEntityId)
                         && er.RelationshipType.Name == "deal_client")
            .Select(er => new { er.SourceEntityId, er.TargetEntityId })
            .ToListAsync(ct);

        var allClientDealMap = allClientDealRels
            .GroupBy(r => r.TargetEntityId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.SourceEntityId).ToList());

        decimal DealValue(int dealId) =>
            decimal.TryParse(dealProps.GetValueOrDefault(dealId)?.GetValueOrDefault("deal_value"), out var dv) ? dv : 0m;

        (decimal value, bool isExpected) ClientLtv(int clientId)
        {
            var deals = allClientDealMap.GetValueOrDefault(clientId, []);
            var closed = deals
                .Where(did => (dealProps.GetValueOrDefault(did)?.GetValueOrDefault("status") ?? "") == "closed")
                .Sum(DealValue);
            if (closed > 0) return (closed, false);
            var expected = deals
                .Where(did => (dealProps.GetValueOrDefault(did)?.GetValueOrDefault("status") ?? "") is "opened" or "pending")
                .Sum(DealValue);
            return (expected, true);
        }

        var clientLtvs = clientIds
            .Select(id => { var (ltv, isExp) = ClientLtv(id); return (id, ltv, isExp); })
            .Where(x => x.ltv > 0)
            .OrderByDescending(x => x.ltv)
            .Take(10)
            .ToList();

        var top10ClientIds   = clientLtvs.Select(x => x.id).ToList();
        var allLinkedDealIds = top10ClientIds.SelectMany(id => allClientDealMap.GetValueOrDefault(id, [])).Distinct().ToList();
        var allMlScores      = await mlClient.ScoreBatchAsync(allLinkedDealIds, ct);

        var topClients = clientLtvs.Select(x =>
        {
            var (id, ltv, isExpected) = x;
            var p      = clientProps.GetValueOrDefault(id);
            var linked = allClientDealMap.GetValueOrDefault(id, []);
            var active = linked.Count(did => (dealProps.GetValueOrDefault(did)?.GetValueOrDefault("status") ?? "") is "opened" or "pending");
            var scores = linked.Where(did => allMlScores.TryGetValue(did, out var ms) && ms.ClosureScore.HasValue)
                               .Select(did => allMlScores[did].ClosureScore!.Value).ToList();
            double? avg = scores.Count > 0 ? Math.Round(scores.Average(), 4) : null;
            var name    = p?.GetValueOrDefault("company_name") ?? p?.GetValueOrDefault("name") ?? $"Client #{id}";
            return new TopClientDto(id, name, p?.GetValueOrDefault("industry"), ltv, isExpected, active, avg);
        }).ToList();

        return new TopEntitiesDto(topDeals, topClients);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Member activity
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<MemberActivityDto>> GetMemberActivityAsync(
        int userId, int workspaceId, CancellationToken ct)
    {
        var perms = await GetWorkspacePermissionsAsync(userId, workspaceId, ct);
        if (!perms.Contains(ViewTeamAnalytics))
            throw new ForbiddenAccessException("Requires view_team_analytics permission.");

        // Members with role names
        var members = await db.UserRoleWorkspaces
            .Where(urw => urw.WorkspaceId == workspaceId && !urw.IsArchived)
            .Select(urw => new { urw.UserId, urw.Role.Name })
            .ToListAsync(ct);

        var memberUserIds = members.Select(m => m.UserId).Distinct().ToList();

        var userDetails = await db.Users
            .Where(u => memberUserIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToDictionaryAsync(u => u.Id, ct);

        // Deals and tasks created by each user in this workspace
        var dealsByUser = await db.EntityWorkspaces
            .Where(ew => ew.WorkspaceId == workspaceId
                         && !ew.Entity.IsArchived
                         && ew.Entity.EntityType.Name == "deal")
            .GroupBy(ew => ew.Entity.CreatedByUserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        var taskCreators = await db.EntityWorkspaces
            .Where(ew => ew.WorkspaceId == workspaceId
                         && !ew.Entity.IsArchived
                         && ew.Entity.EntityType.Name == "task")
            .Select(ew => new { ew.Entity.Id, ew.Entity.CreatedByUserId })
            .ToListAsync(ct);

        var taskEntityIds = taskCreators.Select(t => t.Id).Distinct().ToList();
        var taskProps     = await GetPropertyValuesAsync(taskEntityIds, ["task_status"], ct);

        var tasksByUser     = taskCreators.GroupBy(t => t.CreatedByUserId).ToDictionary(g => g.Key, g => g.Count());
        var tasksDoneByUser = taskCreators
            .Where(t => string.Equals(taskProps.GetValueOrDefault(t.Id)?.GetValueOrDefault("task_status"),
                        "done", StringComparison.OrdinalIgnoreCase))
            .GroupBy(t => t.CreatedByUserId)
            .ToDictionary(g => g.Key, g => g.Count());

        return members
            .GroupBy(m => m.UserId)
            .Select(g =>
            {
                var uid  = g.Key;
                var role = g.OrderBy(x => x.Name).First().Name;
                var u    = userDetails.GetValueOrDefault(uid);
                var full = u != null ? $"{u.FirstName} {u.LastName}".Trim() : $"User #{uid}";
                return new MemberActivityDto(
                    uid, full, role,
                    dealsByUser.GetValueOrDefault(uid),
                    tasksByUser.GetValueOrDefault(uid),
                    tasksDoneByUser.GetValueOrDefault(uid));
            })
            .ToList();
    }

    private async Task EnsureDealAnalysisEntitiesAsync(
        IReadOnlyList<int> dealIds, int workspaceId, int userId, CancellationToken ct)
    {
        var analysisType = await db.EntityTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Name == "deal_analysis", ct);
        if (analysisType is null) return;

        var relType = await db.EntityRelationshipTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(rt => rt.Name == "deal_analysis", ct);
        if (relType is null) return;

        var linkedDealIds = await db.EntityRelationships
            .Where(er => dealIds.Contains(er.SourceEntityId) && er.RelationshipTypeId == relType.Id)
            .Select(er => er.SourceEntityId)
            .ToHashSetAsync(ct);

        var missing = dealIds.Where(id => !linkedDealIds.Contains(id)).ToList();
        if (missing.Count == 0) return;

        var newEntities = missing
            .Select(_ => new Relativa.Persistence.Entities.Entity
            {
                EntityTypeId    = analysisType.Id,
                CreatedByUserId = userId,
                IsArchived      = false,
            })
            .ToList();
        db.Entities.AddRange(newEntities);
        await db.SaveChangesAsync(ct);

        for (int i = 0; i < missing.Count; i++)
        {
            db.EntityWorkspaces.Add(new Relativa.Persistence.Entities.EntityWorkspace
            {
                EntityId    = newEntities[i].Id,
                WorkspaceId = workspaceId,
            });
            db.EntityRelationships.Add(new Relativa.Persistence.Entities.EntityRelationship
            {
                SourceEntityId     = missing[i],
                TargetEntityId     = newEntities[i].Id,
                RelationshipTypeId = relType.Id,
            });
        }
        await db.SaveChangesAsync(ct);
    }
}
