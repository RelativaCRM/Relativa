using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Relativa.Audit.Data;
using Relativa.Audit.DTOs;
using Relativa.Audit.Exceptions;
using Relativa.Audit.Validation;
using Relativa.Persistence.Entities;
using Relativa.Persistence.Entities.AuditLogs;

namespace Relativa.Audit.Services;

public sealed class AuditLogReadService(
    AuditDbContext db,
    IValidator<GetAuditLogQuery> queryValidator,
    IOptions<AuditLogReadOptions> options)
{
    private readonly AuditLogReadOptions _opt = options.Value;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<AuditLogListResponse> GetAsync(GetAuditLogQuery q, int callerUserId, CancellationToken ct)
    {
        await queryValidator.ValidateAndThrowAsync(q, ct);
        var category = q.EntityTypeCategory.Trim().ToLowerInvariant();

        var from = q.DateFrom ?? DateTimeOffset.UtcNow.AddDays(-_opt.DefaultDateRangeDays);
        var to = q.DateTo ?? DateTimeOffset.UtcNow;
        var index = q.Index;
        var pageSize = q.PageSize;
        var skip = (index - 1) * pageSize;

        await EnsureResourcesExistAsync(q, category, ct);
        await EnsureRbacAsync(callerUserId, category, q.WorkspaceId, q.OrganizationId, ct);

        var filterContext = await BuildFilterContextAsync(q, category, ct);

        return category switch
        {
            "entity" => await GetEntityScopeAsync(
                from, to, q.Action, q.EntityId, q.DomainEntityType, q.ActorUserId, q.WorkspaceId!.Value, skip, pageSize, index, filterContext, ct),
            "workspace" => await GetWorkspaceScopeAsync(
                from, to, q.Action, q.ActorUserId, q.WorkspaceId!.Value, skip, pageSize, index, filterContext, ct),
            "organization" => await GetOrganizationScopeAsync(
                from, to, q.Action, q.ActorUserId, q.OrganizationId!.Value, skip, pageSize, index, filterContext, ct),
            "user" => await GetUserScopeAsync(
                from, to, q.Action, q.ActorUserId, q.TargetUserId, callerUserId, skip, pageSize, index, filterContext, ct),
            _ => throw new ArgumentException("Invalid category.")
        };
    }

    private async Task<AuditFilterContextDto?> BuildFilterContextAsync(GetAuditLogQuery q, string category, CancellationToken ct)
    {
        if (category is "entity" or "workspace" && q.WorkspaceId is { } wid)
        {
            var w = await db.Set<Workspace>().AsNoTracking()
                .Where(x => x.Id == wid)
                .Select(x => new { x.Id, x.Name, x.OrganizationId, OrgName = x.Organization.Name })
                .FirstAsync(ct);
            return new AuditFilterContextDto(
                new WorkspaceContextDto(w.Id, w.Name, w.OrganizationId, w.OrgName),
                null);
        }

        if (category == "organization" && q.OrganizationId is { } oid)
        {
            var o = await db.Set<Organization>().AsNoTracking()
                .Where(x => x.Id == oid)
                .Select(x => new { x.Id, x.Name })
                .FirstAsync(ct);
            return new AuditFilterContextDto(
                null,
                new OrganizationContextDto(o.Id, o.Name));
        }

        return null;
    }

    private async Task EnsureResourcesExistAsync(GetAuditLogQuery q, string category, CancellationToken ct)
    {
        if (q.WorkspaceId is { } wId)
        {
            if (!await db.Set<Workspace>().AsNoTracking().AnyAsync(x => x.Id == wId, ct))
                throw new KeyNotFoundException($"Workspace with id {wId} was not found.");
        }

        if (q.OrganizationId is { } oId)
        {
            if (!await db.Set<Organization>().AsNoTracking().AnyAsync(x => x.Id == oId, ct))
                throw new KeyNotFoundException($"Organization with id {oId} was not found.");
        }

        if (q.EntityId is { } eId)
        {
            if (!await db.Set<Entity>().AsNoTracking().AnyAsync(x => x.Id == eId, ct))
                throw new KeyNotFoundException($"Entity with id {eId} was not found.");
        }

        if (q.TargetUserId is { } tuId)
        {
            if (!await db.Set<User>().AsNoTracking().AnyAsync(x => x.Id == tuId, ct))
                throw new KeyNotFoundException($"User with id {tuId} was not found.");
        }

        if (category == "entity" && q.EntityId is { } entId && q.WorkspaceId is { } wsId)
        {
            var linked = await db.Set<EntityWorkspace>().AsNoTracking()
                .AnyAsync(x => x.EntityId == entId && x.WorkspaceId == wsId, ct);
            if (!linked)
                throw new KeyNotFoundException($"Entity {entId} is not linked to workspace {wsId}.");
        }
    }

    private async Task EnsureRbacAsync(
        int callerUserId,
        string category,
        int? workspaceId,
        int? organizationId,
        CancellationToken ct)
    {
        switch (category)
        {
            case "entity":
            case "workspace":
                await RequireWsAdminOrAnalystAsync(callerUserId, workspaceId!.Value, ct);
                break;
            case "organization":
                await RequireOrgOwnerOrAdminAsync(callerUserId, organizationId!.Value, ct);
                break;
            case "user":
                return;
        }
    }

    private async Task RequireWsAdminOrAnalystAsync(int userId, int workspaceId, CancellationToken ct)
    {
        var ok = await (
            from urw in db.Set<UserRoleWorkspace>().AsNoTracking()
            join wr in db.Set<WorkspaceRole>().AsNoTracking() on urw.WsRoleId equals wr.Id
            where urw.UserId == userId && urw.WorkspaceId == workspaceId && !urw.IsArchived
                  && (wr.Name == "ws_admin" || wr.Name == "ws_analyst")
            select urw.Id).AnyAsync(ct);

        if (!ok)
            throw new ForbiddenAccessException("Audit log requires workspace role ws_admin or ws_analyst.");
    }

    private async Task RequireOrgOwnerOrAdminAsync(int userId, int organizationId, CancellationToken ct)
    {
        var ok = await (
            from uro in db.Set<UserRoleOrganization>().AsNoTracking()
            join oro in db.Set<OrganizationRole>().AsNoTracking() on uro.OrgRoleId equals oro.Id
            where uro.UserId == userId && uro.OrganizationId == organizationId && !uro.IsArchived
                  && (oro.Name == "org_owner" || oro.Name == "org_admin")
            select uro.Id).AnyAsync(ct);

        if (!ok)
            throw new ForbiddenAccessException("Audit log requires organization role org_owner or org_admin.");
    }

    private async Task<HashSet<int>> GetVisibleTargetUserIdsAsync(int callerUserId, CancellationToken ct)
    {
        var visible = new HashSet<int> { callerUserId };

        var orgTargets = await (
            from cu in db.Set<UserRoleOrganization>().AsNoTracking()
            join cr in db.Set<OrganizationRole>().AsNoTracking() on cu.OrgRoleId equals cr.Id
            join tu in db.Set<UserRoleOrganization>().AsNoTracking() on cu.OrganizationId equals tu.OrganizationId
            where cu.UserId == callerUserId && !cu.IsArchived && !tu.IsArchived
                  && (cr.Name == "org_owner" || cr.Name == "org_admin")
            select tu.UserId).Distinct().ToListAsync(ct);
        foreach (var u in orgTargets)
            visible.Add(u);

        var wsTargets = await (
            from cw in db.Set<UserRoleWorkspace>().AsNoTracking()
            join wr in db.Set<WorkspaceRole>().AsNoTracking() on cw.WsRoleId equals wr.Id
            join tw in db.Set<UserRoleWorkspace>().AsNoTracking() on cw.WorkspaceId equals tw.WorkspaceId
            where cw.UserId == callerUserId && !cw.IsArchived && !tw.IsArchived
                  && (wr.Name == "ws_admin" || wr.Name == "ws_analyst")
            select tw.UserId).Distinct().ToListAsync(ct);
        foreach (var u in wsTargets)
            visible.Add(u);

        return visible;
    }

    private async Task<AuditLogListResponse> GetEntityScopeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? actionFilter,
        int? entityId,
        string? domainEntityType,
        int? actorUserId,
        int workspaceId,
        int skip,
        int pageSize,
        int pageIndex,
        AuditFilterContextDto? filterContext,
        CancellationToken ct)
    {
        var entityIdsInWs = db.Set<EntityWorkspace>().AsNoTracking()
            .Where(ew => ew.WorkspaceId == workspaceId)
            .Select(ew => ew.EntityId);

        var query = db.EntityAuditLogs.AsNoTracking()
            .Where(x => x.ChangedAt >= from && x.ChangedAt <= to)
            .Where(x => x.EntityId != null && entityIdsInWs.Contains(x.EntityId.Value));

        if (!string.IsNullOrWhiteSpace(actionFilter))
            query = query.Where(x => x.Action == actionFilter);
        if (entityId.HasValue)
            query = query.Where(x => x.EntityId == entityId.Value);
        if (!string.IsNullOrWhiteSpace(domainEntityType))
        {
            var d = domainEntityType.Trim();
            query = query.Where(x => x.EntityType == d);
        }

        if (actorUserId.HasValue)
            query = query.Where(x => x.ChangedById == actorUserId.Value);

        var total = await query.LongCountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.ChangedAt)
            .Skip(skip)
            .Take(pageSize)
            .Include(x => x.ChangedBy)
            .Include(x => x.Entity)!.ThenInclude(e => e!.EntityType)
            .ToListAsync(ct);

        var dictTypeIds = rows
            .Select(r => r.Entity?.EntityTypeId ?? ParseEntityTypeId(r.EntityType))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var propDefsByType = await LoadPropertyDefinitionsAsync(dictTypeIds, ct);
        var data = rows.Select(r => MapEntityRow(r, propDefsByType)).ToList();
        return new AuditLogListResponse(data, total, pageIndex, pageSize, filterContext);
    }

    private static int? ParseEntityTypeId(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return null;
        return int.TryParse(s.Trim(), out var id) ? id : null;
    }

    private async Task<AuditLogListResponse> GetWorkspaceScopeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? actionFilter,
        int? actorUserId,
        int workspaceId,
        int skip,
        int pageSize,
        int pageIndex,
        AuditFilterContextDto? filterContext,
        CancellationToken ct)
    {
        var query = db.WorkspaceAuditLogs.AsNoTracking()
            .Where(x => x.ChangedAt >= from && x.ChangedAt <= to && x.WorkspaceId == workspaceId);

        if (!string.IsNullOrWhiteSpace(actionFilter))
            query = query.Where(x => x.Action == actionFilter);
        if (actorUserId.HasValue)
            query = query.Where(x => x.ChangedById == actorUserId.Value);

        var total = await query.LongCountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.ChangedAt)
            .Skip(skip)
            .Take(pageSize)
            .Include(x => x.ChangedBy)
            .Include(x => x.Workspace)!.ThenInclude(w => w!.Organization)
            .ToListAsync(ct);

        var data = rows.Select(MapWorkspaceRow).ToList();
        return new AuditLogListResponse(data, total, pageIndex, pageSize, filterContext);
    }

    private async Task<AuditLogListResponse> GetOrganizationScopeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? actionFilter,
        int? actorUserId,
        int organizationId,
        int skip,
        int pageSize,
        int pageIndex,
        AuditFilterContextDto? filterContext,
        CancellationToken ct)
    {
        var query = db.OrganizationAuditLogs.AsNoTracking()
            .Where(x => x.ChangedAt >= from && x.ChangedAt <= to && x.OrganizationId == organizationId);

        if (!string.IsNullOrWhiteSpace(actionFilter))
            query = query.Where(x => x.Action == actionFilter);
        if (actorUserId.HasValue)
            query = query.Where(x => x.ChangedById == actorUserId.Value);

        var total = await query.LongCountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.ChangedAt)
            .Skip(skip)
            .Take(pageSize)
            .Include(x => x.ChangedBy)
            .Include(x => x.Organization)
            .ToListAsync(ct);

        var data = rows.Select(MapOrganizationRow).ToList();
        return new AuditLogListResponse(data, total, pageIndex, pageSize, filterContext);
    }

    private async Task<AuditLogListResponse> GetUserScopeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? actionFilter,
        int? actorUserId,
        int? targetUserIdFilter,
        int callerUserId,
        int skip,
        int pageSize,
        int pageIndex,
        AuditFilterContextDto? filterContext,
        CancellationToken ct)
    {
        var visibleTargets = await GetVisibleTargetUserIdsAsync(callerUserId, ct);
        if (targetUserIdFilter.HasValue)
        {
            if (!visibleTargets.Contains(targetUserIdFilter.Value))
                throw new ForbiddenAccessException("You are not allowed to view audit for this user.");
        }

        var query = db.UserAuditLogs.AsNoTracking()
            .Where(x => x.ChangedAt >= from && x.ChangedAt <= to
                        && x.TargetUserId != null
                        && visibleTargets.Contains(x.TargetUserId.Value));

        if (!string.IsNullOrWhiteSpace(actionFilter))
            query = query.Where(x => x.Action == actionFilter);
        if (actorUserId.HasValue)
            query = query.Where(x => x.ChangedById == actorUserId.Value);
        if (targetUserIdFilter.HasValue)
            query = query.Where(x => x.TargetUserId == targetUserIdFilter.Value);

        var total = await query.LongCountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.ChangedAt)
            .Skip(skip)
            .Take(pageSize)
            .Include(x => x.ChangedBy)
            .Include(x => x.TargetUser)
            .ToListAsync(ct);

        var data = rows.Select(MapUserRow).ToList();
        return new AuditLogListResponse(data, total, pageIndex, pageSize, filterContext);
    }

    private async Task<Dictionary<int, IReadOnlyList<PropertyDefinitionDto>>> LoadPropertyDefinitionsAsync(
        IReadOnlyCollection<int> entityTypeIds,
        CancellationToken ct)
    {
        var result = new Dictionary<int, IReadOnlyList<PropertyDefinitionDto>>();
        if (entityTypeIds.Count == 0)
            return result;

        var rows = await (
            from etp in db.Set<EntityTypeProperty>().AsNoTracking()
            join p in db.Set<Property>().AsNoTracking() on etp.PropertyId equals p.Id
            where entityTypeIds.Contains(etp.EntityTypeId)
            select new { etp.EntityTypeId, p.Id, p.Name, p.DataType }).ToListAsync(ct);

        foreach (var g in rows.GroupBy(x => x.EntityTypeId))
        {
            result[g.Key] = g.Select(x =>
                    new PropertyDefinitionDto(x.Id, x.Name, x.DataType.ToString()))
                .ToList();
        }

        return result;
    }

    private AuditLogEntryDto MapEntityRow(
        EntityAuditLog r,
        IReadOnlyDictionary<int, IReadOnlyList<PropertyDefinitionDto>> propDefsByType)
    {
        var typeId = r.Entity?.EntityTypeId ?? ParseEntityTypeId(r.EntityType);
        var entityDeleted = r.EntityId.HasValue && r.Entity == null;
        IReadOnlyList<PropertyDefinitionDto>? defs = null;
        if (typeId.HasValue && propDefsByType.TryGetValue(typeId.Value, out var d))
            defs = d;

        var (propChanges, defsOut) = BuildPropertyChanges(r.OldValue, r.NewValue, defs);
        if (defs is null)
            defs = defsOut;

        EntityAuditContextDto? entCtx;
        if (r.Entity != null)
            entCtx = new EntityAuditContextDto(
                r.Entity.Id, r.Entity.EntityTypeId, r.Entity.EntityType?.Name, r.Entity.IsArchived);
        else if (r.EntityId.HasValue)
            entCtx = new EntityAuditContextDto(r.EntityId.Value, typeId, null, null);
        else
            entCtx = null;

        return new AuditLogEntryDto(
            r.Id,
            "entity",
            r.Action,
            r.FieldName,
            r.ChangedAt,
            MapActor(r.ChangedById, r.ChangedBy),
            JsonDocumentToObject(r.OldValue),
            JsonDocumentToObject(r.NewValue),
            entCtx,
            null,
            null,
            null,
            entityDeleted,
            r.EntityType,
            defs,
            propChanges);
    }

    private static AuditLogEntryDto MapWorkspaceRow(WorkspaceAuditLog r)
    {
        WorkspaceAuditContextDto? wctx = null;
        if (r.Workspace != null)
            wctx = new WorkspaceAuditContextDto(
                r.Workspace.Id,
                r.Workspace.Name,
                r.Workspace.OrganizationId,
                r.Workspace.Organization?.Name);

        return new AuditLogEntryDto(
            r.Id,
            "workspace",
            r.Action,
            r.FieldName,
            r.ChangedAt,
            MapActor(r.ChangedById, r.ChangedBy),
            JsonDocumentToObject(r.OldValue),
            JsonDocumentToObject(r.NewValue),
            null,
            wctx,
            null,
            null,
            null,
            null,
            null,
            null);
    }

    private static AuditLogEntryDto MapOrganizationRow(OrganizationAuditLog r)
    {
        OrganizationAuditContextDto? octx = null;
        if (r.Organization != null)
            octx = new OrganizationAuditContextDto(r.Organization.Id, r.Organization.Name);

        return new AuditLogEntryDto(
            r.Id,
            "organization",
            r.Action,
            r.FieldName,
            r.ChangedAt,
            MapActor(r.ChangedById, r.ChangedBy),
            JsonDocumentToObject(r.OldValue),
            JsonDocumentToObject(r.NewValue),
            null,
            null,
            octx,
            null,
            null,
            null,
            null,
            null);
    }

    private static AuditLogEntryDto MapUserRow(UserAuditLog r)
    {
        UserAuditContextDto? tu = null;
        if (r.TargetUser != null)
            tu = new UserAuditContextDto(
                r.TargetUser.Id,
                r.TargetUser.Email,
                r.TargetUser.FirstName,
                r.TargetUser.LastName);

        return new AuditLogEntryDto(
            r.Id,
            "user",
            r.Action,
            r.FieldName,
            r.ChangedAt,
            MapActor(r.ChangedById, r.ChangedBy),
            JsonDocumentToObject(r.OldValue),
            JsonDocumentToObject(r.NewValue),
            null,
            null,
            null,
            tu,
            null,
            null,
            null,
            null);
    }

    private static ActorDto? MapActor(int? id, User? u)
    {
        if (id is null && u is null)
            return null;
        if (u != null)
            return new ActorDto(u.Id, u.Email, u.FirstName, u.LastName);
        return new ActorDto(id, null, null, null);
    }

    private static object? JsonDocumentToObject(System.Text.Json.JsonDocument? doc)
    {
        if (doc is null)
            return null;
        return JsonSerializer.Deserialize<JsonElement>(doc.RootElement.GetRawText());
    }

    private (IReadOnlyList<PropertyChangeDto>? changes, IReadOnlyList<PropertyDefinitionDto>? defsUsed)
        BuildPropertyChanges(
            System.Text.Json.JsonDocument? oldDoc,
            System.Text.Json.JsonDocument? newDoc,
            IReadOnlyList<PropertyDefinitionDto>? defs)
    {
        if (defs is null || defs.Count == 0)
            return (null, null);

        var defMap = defs.ToDictionary(d => d.PropertyId);

        List<PropertyValueDto>? oldList = null;
        List<PropertyValueDto>? newList = null;
        try
        {
            if (newDoc != null)
                newList = JsonSerializer.Deserialize<List<PropertyValueDto>>(newDoc.RootElement.GetRawText(), JsonOpts);
            if (oldDoc != null)
                oldList = JsonSerializer.Deserialize<List<PropertyValueDto>>(oldDoc.RootElement.GetRawText(), JsonOpts);
        }
        catch (JsonException)
        {
            return (null, defs);
        }

        if (newList is null && oldList is null)
            return (null, defs);

        var ids = new HashSet<int>();
        if (newList != null)
        {
            foreach (var x in newList)
                ids.Add(x.PropertyId);
        }

        if (oldList != null)
        {
            foreach (var x in oldList)
                ids.Add(x.PropertyId);
        }

        var changes = new List<PropertyChangeDto>();
        foreach (var id in ids)
        {
            var oldV = oldList?.FirstOrDefault(p => p.PropertyId == id)?.Value;
            var newV = newList?.FirstOrDefault(p => p.PropertyId == id)?.Value;
            if (defMap.TryGetValue(id, out var d))
            {
                changes.Add(new PropertyChangeDto(
                    id,
                    d.Name,
                    d.DataType,
                    oldV,
                    newV));
            }
        }

        return (changes.Count > 0 ? changes : null, defs);
    }

    private sealed class PropertyValueDto
    {
        public int PropertyId { get; set; }
        public string? Value { get; set; }
    }
}

public sealed class AuditLogReadOptions
{
    public int DefaultDateRangeDays { get; set; } = 30;
}
