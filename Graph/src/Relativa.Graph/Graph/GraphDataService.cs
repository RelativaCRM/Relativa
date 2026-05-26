using Microsoft.EntityFrameworkCore;
using Relativa.Graph.Data;
using Relativa.Graph.ML;

namespace Relativa.Graph.Graph;

public sealed class GraphDataService(GraphQueryDbContext db, IMlScoringClient mlClient) : IGraphDataService
{
    private static readonly string[] LabelPriority = ["title", "name", "first_name", "email", "company"];

    // System types hidden from the graph view (ML-derived, noise if shown)
    private static readonly HashSet<string> SystemEntityTypes =
        new(StringComparer.OrdinalIgnoreCase) { "deal_analysis" };

    public async Task<GraphResponseDto> BuildGraphAsync(int userId, int organizationId, string? riskLevel, CancellationToken ct)
    {
        var nodes = new List<GraphNodeDto>();
        var edges = new List<GraphEdgeDto>();

        // Step 1: focal user
        var focalUser = await db.Users
            .Where(u => u.Id == userId && !u.IsArchived)
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .FirstOrDefaultAsync(ct);

        if (focalUser is null)
            return new GraphResponseDto([], []);

        nodes.Add(new GraphNodeDto(
            Id: $"user:{userId}",
            Type: "user_self",
            Label: $"{focalUser.FirstName} {focalUser.LastName}".Trim(),
            Subtitle: focalUser.Email,
            EntityTypeName: null,
            ResourceId: userId,
            ResourceType: "user",
            WorkspaceId: null,
            Permissions: ["view"]
        ));

        // Step 2: org-level permissions
        var orgPermissions = await db.UserRoleOrganizations
            .Where(uro => uro.UserId == userId
                          && uro.OrganizationId == organizationId
                          && !uro.IsArchived)
            .SelectMany(uro => uro.Role.RolePermissions.Select(rp => rp.Permission.Name))
            .ToHashSetAsync(ct);

        // Step 3: accessible workspaces + per-workspace permissions
        var workspaceMemberships = await db.UserRoleWorkspaces
            .Where(urw => urw.UserId == userId
                          && !urw.IsArchived
                          && !urw.Workspace.IsArchived
                          && urw.Workspace.OrganizationId == organizationId)
            .Select(urw => new
            {
                urw.WorkspaceId,
                WorkspaceName = urw.Workspace.Name,
                PermissionName = (string?)null
            })
            .ToListAsync(ct);

        // Separate query for permissions to avoid cartesian explosion
        var workspaceIds = workspaceMemberships.Select(m => m.WorkspaceId).Distinct().ToList();

        var wsPermRows = await db.UserRoleWorkspaces
            .Where(urw => urw.UserId == userId
                          && !urw.IsArchived
                          && workspaceIds.Contains(urw.WorkspaceId))
            .SelectMany(urw => urw.Role.RolePermissions.Select(rp => new
            {
                urw.WorkspaceId,
                PermissionName = rp.Permission.Name
            }))
            .ToListAsync(ct);

        var workspacePermissions = wsPermRows
            .GroupBy(r => r.WorkspaceId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.PermissionName).ToHashSet());

        // Workspace names (deduplicated)
        var workspaceNames = workspaceMemberships
            .GroupBy(m => m.WorkspaceId)
            .ToDictionary(g => g.Key, g => g.First().WorkspaceName);

        foreach (var wsId in workspaceIds)
        {
            var wsPerms = workspacePermissions.GetValueOrDefault(wsId, []);
            var wsPermList = new List<string> { "view" };
            if (wsPerms.Contains("manage_ws_settings")) wsPermList.Add("manage");

            nodes.Add(new GraphNodeDto(
                Id: $"workspace:{wsId}",
                Type: "workspace",
                Label: workspaceNames.GetValueOrDefault(wsId, $"Workspace #{wsId}"),
                Subtitle: null,
                EntityTypeName: null,
                ResourceId: wsId,
                ResourceType: "workspace",
                WorkspaceId: wsId,
                Permissions: wsPermList
            ));

            edges.Add(new GraphEdgeDto(
                Id: $"uw:{userId}:{wsId}",
                From: $"user:{userId}",
                To: $"workspace:{wsId}",
                Type: "user_workspace",
                Label: null
            ));
        }

        // Step 4a: entities in accessible workspaces with view_entities permission
        var viewableWsIds = workspaceIds
            .Where(wsId => workspacePermissions.GetValueOrDefault(wsId, []).Contains("view_entities"))
            .ToList();

        // entityId → list of accessible workspace IDs
        var entityWorkspaceMap = new Dictionary<int, List<int>>();
        // entityId → entity type name
        var entityTypeNameMap = new Dictionary<int, string>();

        if (viewableWsIds.Count > 0)
        {
            var entityRows = await db.EntityWorkspaces
                .Where(ew => viewableWsIds.Contains(ew.WorkspaceId)
                             && !ew.Entity.IsArchived
                             && !SystemEntityTypes.Contains(ew.Entity.EntityType.Name))
                .Select(ew => new
                {
                    ew.EntityId,
                    ew.WorkspaceId,
                    EntityTypeName = ew.Entity.EntityType.Name
                })
                .ToListAsync(ct);

            foreach (var row in entityRows)
            {
                if (!entityWorkspaceMap.TryGetValue(row.EntityId, out var wsList))
                {
                    wsList = [];
                    entityWorkspaceMap[row.EntityId] = wsList;
                    entityTypeNameMap[row.EntityId] = row.EntityTypeName;
                }
                wsList.Add(row.WorkspaceId);
            }
        }

        var entityIds = entityWorkspaceMap.Keys.ToList();

        // Step 4b: entity labels via priority property values
        var labelMap = new Dictionary<int, string>();

        if (entityIds.Count > 0)
        {
            var labelPrioritySet = LabelPriority.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var valueRows = await db.EntityPropertyValues
                .Where(epv => entityIds.Contains(epv.EntityId)
                              && labelPrioritySet.Contains(epv.Property.Name.ToLower()))
                .Select(epv => new
                {
                    epv.EntityId,
                    PropertyName = epv.Property.Name.ToLower(),
                    epv.ValueString,
                    epv.ValueInt,
                    epv.ValueDecimal,
                    epv.ValueBool,
                    epv.ValueDate
                })
                .ToListAsync(ct);

            var valuesByEntity = valueRows.GroupBy(r => r.EntityId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var entityId in entityIds)
            {
                if (!valuesByEntity.TryGetValue(entityId, out var vals))
                    continue;

                foreach (var priorityField in LabelPriority)
                {
                    var match = vals.FirstOrDefault(v => v.PropertyName == priorityField);
                    if (match is null) continue;

                    var text = match.ValueString?.Trim()
                        ?? match.ValueInt?.ToString()
                        ?? match.ValueDecimal?.ToString()
                        ?? (match.ValueBool.HasValue ? match.ValueBool.Value.ToString() : null)
                        ?? match.ValueDate?.ToString();

                    if (!string.IsNullOrEmpty(text))
                    {
                        labelMap[entityId] = text;
                        break;
                    }
                }
            }
        }

        // Step 4c: ML scoring + highlight classification
        var dealEntityIds = entityIds
            .Where(id => string.Equals(entityTypeNameMap.GetValueOrDefault(id), "deal", StringComparison.OrdinalIgnoreCase))
            .ToList();

        IReadOnlyDictionary<int, MlScoreDto> mlScores;
        try
        {
            mlScores = await mlClient.ScoreBatchAsync(dealEntityIds, ct);
        }
        catch
        {
            mlScores = new Dictionary<int, MlScoreDto>();
        }

        if (riskLevel is not null)
        {
            var excludedDealIds = dealEntityIds
                .Where(id => !mlScores.TryGetValue(id, out var s) || !s.ClosureScore.HasValue || !IsInRiskBucket(s.ClosureScore.Value, riskLevel))
                .ToHashSet();
            foreach (var id in excludedDealIds)
                entityWorkspaceMap.Remove(id);
            dealEntityIds = dealEntityIds.Where(id => !excludedDealIds.Contains(id)).ToList();
            entityIds = entityWorkspaceMap.Keys.ToList();
        }

        // deal→client relationship for client composite scoring
        var dealClientMap = new Dictionary<int, int>(); // dealId → clientId
        var clientLtvMap = new Dictionary<int, double>(); // clientId → LTV
        if (dealEntityIds.Count > 0)
        {
            var dealClientRels = await db.EntityRelationships
                .Where(er => dealEntityIds.Contains(er.SourceEntityId)
                             && er.RelationshipType.Name == "deal_client")
                .Select(er => new { er.SourceEntityId, er.TargetEntityId })
                .ToListAsync(ct);

            foreach (var rel in dealClientRels)
                dealClientMap[rel.SourceEntityId] = rel.TargetEntityId;

            var clientIds = dealClientRels.Select(r => r.TargetEntityId).Distinct().ToList();
            if (clientIds.Count > 0)
            {
                var ltvRows = await db.EntityPropertyValues
                    .Where(epv => clientIds.Contains(epv.EntityId)
                                  && epv.Property.Name == "client_lifetime_value"
                                  && epv.ValueDecimal != null)
                    .Select(epv => new { epv.EntityId, epv.ValueDecimal })
                    .ToListAsync(ct);

                foreach (var row in ltvRows)
                    clientLtvMap[row.EntityId] = (double)row.ValueDecimal!;
            }
        }

        // Compute composite client scores: avgClosure * (1 - avgChurn/100) + log10(max(1, ltv)) * 5
        var clientDealScores = new Dictionary<int, List<(double closure, double churn)>>();
        foreach (var (dealId, clientId) in dealClientMap)
        {
            if (!mlScores.TryGetValue(dealId, out var score) || score.ClosureScore is null || score.ChurnScore is null)
                continue;
            if (!clientDealScores.TryGetValue(clientId, out var list))
            {
                list = [];
                clientDealScores[clientId] = list;
            }
            list.Add((score.ClosureScore.Value, score.ChurnScore.Value));
        }

        var clientCompositeScores = new Dictionary<int, double>();
        foreach (var (clientId, dealScoreList) in clientDealScores)
        {
            var avgClosure = dealScoreList.Average(s => s.closure);
            var avgChurn = dealScoreList.Average(s => s.churn);
            var ltv = clientLtvMap.GetValueOrDefault(clientId, 0.0);
            clientCompositeScores[clientId] = avgClosure * (1.0 - avgChurn / 100.0) + Math.Log10(Math.Max(1.0, ltv)) * 5.0;
        }

        // Classify top/bottom 20% of deals and clients with valid scores
        var dealHighlightTags = ClassifyTopBottom(
            mlScores.Values
                .Where(s => s.ClosureScore.HasValue && s.UnavailableReason is null)
                .ToDictionary(s => s.EntityId, s => s.ClosureScore!.Value),
            "best_deal", "worst_deal");

        var clientHighlightTags = ClassifyTopBottom(
            clientCompositeScores,
            "best_client", "worst_client");

        // Build entity nodes and workspace→entity edges
        foreach (var (entityId, wsIds) in entityWorkspaceMap)
        {
            var typeName = entityTypeNameMap[entityId];
            var label = labelMap.GetValueOrDefault(entityId, $"{typeName} #{entityId}");
            var primaryWsId = wsIds.Min();
            var wsPerms = workspacePermissions.GetValueOrDefault(primaryWsId, []);

            var perms = new List<string> { "view" };
            if (wsPerms.Contains("edit_entities")) perms.Add("edit");
            if (wsPerms.Contains("delete_entities")) perms.Add("delete");

            string? highlightTag = dealHighlightTags.GetValueOrDefault(entityId)
                ?? clientHighlightTags.GetValueOrDefault(entityId);

            nodes.Add(new GraphNodeDto(
                Id: $"entity:{entityId}",
                Type: "entity",
                Label: label,
                Subtitle: null,
                EntityTypeName: typeName,
                ResourceId: entityId,
                ResourceType: "entity",
                WorkspaceId: primaryWsId,
                Permissions: perms,
                HighlightTag: highlightTag
            ));

            foreach (var wsId in wsIds)
            {
                edges.Add(new GraphEdgeDto(
                    Id: $"we:{wsId}:{entityId}",
                    From: $"workspace:{wsId}",
                    To: $"entity:{entityId}",
                    Type: "workspace_entity",
                    Label: null
                ));
            }
        }

        // Step 5: entity-entity relationships (both endpoints must be visible)
        if (entityIds.Count > 0)
        {
            var entityIdSet = entityIds.ToHashSet();

            var relRows = await db.EntityRelationships
                .Where(er => entityIdSet.Contains(er.SourceEntityId) && entityIdSet.Contains(er.TargetEntityId))
                .Select(er => new
                {
                    er.Id,
                    er.SourceEntityId,
                    er.TargetEntityId,
                    RelTypeName = er.RelationshipType.Name
                })
                .ToListAsync(ct);

            foreach (var rel in relRows)
            {
                edges.Add(new GraphEdgeDto(
                    Id: $"ee:{rel.Id}",
                    From: $"entity:{rel.SourceEntityId}",
                    To: $"entity:{rel.TargetEntityId}",
                    Type: "entity_entity",
                    Label: rel.RelTypeName
                ));
            }
        }

        // Step 6: other org members (conditional on org permissions)
        if (orgPermissions.Contains("remove_org_members") || orgPermissions.Contains("manage_org_workspace_members"))
        {
            var canEditUsers = orgPermissions.Contains("edit_other_org_users_profile");
            var canDeleteUsers = orgPermissions.Contains("delete_org_users");

            var userPerms = new List<string> { "view" };
            if (canEditUsers) userPerms.Add("edit");
            if (canDeleteUsers) userPerms.Add("delete");

            var orgMembers = await db.UserRoleOrganizations
                .Where(uro => uro.OrganizationId == organizationId
                              && !uro.IsArchived
                              && uro.UserId != userId)
                .Select(uro => new { uro.UserId, uro.User.FirstName, uro.User.LastName, uro.User.Email, uro.User.IsArchived })
                .Distinct()
                .ToListAsync(ct);

            foreach (var member in orgMembers.Where(m => !m.IsArchived))
            {
                var nodeId = $"user:{member.UserId}";
                if (nodes.Any(n => n.Id == nodeId)) continue;

                nodes.Add(new GraphNodeDto(
                    Id: nodeId,
                    Type: "user",
                    Label: $"{member.FirstName} {member.LastName}".Trim(),
                    Subtitle: member.Email,
                    EntityTypeName: null,
                    ResourceId: member.UserId,
                    ResourceType: "user",
                    WorkspaceId: null,
                    Permissions: userPerms
                ));

                edges.Add(new GraphEdgeDto(
                    Id: $"uu:{userId}:{member.UserId}",
                    From: $"user:{userId}",
                    To: nodeId,
                    Type: "user_user",
                    Label: null
                ));
            }
        }

        return new GraphResponseDto(nodes, edges);
    }

    private static bool IsInRiskBucket(double score, string riskLevel) => riskLevel switch
    {
        "high"   => score < 33.0,
        "medium" => score >= 33.0 && score < 67.0,
        "low"    => score >= 67.0,
        _        => true
    };

    private static Dictionary<int, string> ClassifyTopBottom(
        Dictionary<int, double> scores,
        string bestTag,
        string worstTag)
    {
        if (scores.Count < 2)
            return [];

        var sorted = scores.OrderByDescending(kv => kv.Value).ToList();
        var cutoff = Math.Max(1, (int)Math.Ceiling(sorted.Count * 0.20));

        var result = new Dictionary<int, string>();
        for (var i = 0; i < cutoff; i++)
            result[sorted[i].Key] = bestTag;
        for (var i = sorted.Count - cutoff; i < sorted.Count; i++)
            if (!result.ContainsKey(sorted[i].Key))
                result[sorted[i].Key] = worstTag;
        return result;
    }
}
