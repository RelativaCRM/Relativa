using FluentAssertions;
using FluentValidation;
using Moq;
using Relativa.Core.Application.DTOs.Entity;
using Relativa.Core.Application.Exceptions;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class EntityRelationshipServiceTests
{
    private readonly Mock<IEntityRepository> _entityRepo = new();
    private readonly Mock<IWorkspaceAccessEvaluator> _workspaceAccess = new();
    private readonly Mock<IUserRoleWorkspaceRepository> _memberRepo = new();
    private readonly Mock<IOutboxWriter> _auditOutboxWriter = new();
    private readonly EntityService _sut;

    private const int Ws = 1;
    private const int UserId = 7;
    private const int SourceTypeId = 100;
    private const int TargetTypeId = 200;

    public EntityRelationshipServiceTests()
    {
        _workspaceAccess
            .Setup(x => x.HasWorkspacePermissionAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _entityRepo
            .Setup(r => r.GetTypePropertiesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _auditOutboxWriter
            .Setup(w => w.EnqueueAuditAsync(It.IsAny<AuditEventContract>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _auditOutboxWriter
            .Setup(w => w.EnqueueDomainAsync(It.IsAny<string>(), It.IsAny<DomainMessageEnvelope>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _entityRepo
            .Setup(r => r.ExecuteInTransactionAsync(It.IsAny<Func<Task<EntityRelationshipRefDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task<EntityRelationshipRefDto>> action, CancellationToken _) => action());

        _sut = new EntityService(
            _entityRepo.Object, _workspaceAccess.Object, _memberRepo.Object,
            Mock.Of<IValidator<CreateEntityRequest>>(), Mock.Of<IValidator<UpdateEntityRequest>>(),
            _auditOutboxWriter.Object);
    }

    private static Entity LinkEntity(int id, int typeId, bool archived = false, int inWorkspace = Ws) =>
        new()
        {
            Id = id,
            EntityTypeId = typeId,
            IsArchived = archived,
            EntityType = new EntityType { Id = typeId, Name = $"type_{typeId}" },
            EntityWorkspaces = new List<EntityWorkspace> { new() { EntityId = id, WorkspaceId = inWorkspace } },
            EntityPropertyValues = new List<EntityPropertyValue>(),
        };

    private static EntityRelationshipType RelType(
        RelationshipCardinality cardinality = RelationshipCardinality.ManyToOne, bool required = false) =>
        new()
        {
            Id = 10,
            Name = "deal_client",
            SourceEntityTypeId = SourceTypeId,
            TargetEntityTypeId = TargetTypeId,
            IsRequired = required,
            RelationshipCardinality = cardinality,
            SourceEntityType = new EntityType { Id = SourceTypeId, Name = "deal" },
            TargetEntityType = new EntityType { Id = TargetTypeId, Name = "client" },
        };

    private static EntityTypeProperty ReadonlyProp() =>
        new() { Property = new Property { Id = 1, Name = "locked", IsReadonly = true } };

    private void SetupCreate(EntityRelationshipType relType, Entity source, Entity target)
    {
        _entityRepo.Setup(r => r.GetRelationshipTypeByIdAsync(relType.Id, It.IsAny<CancellationToken>())).ReturnsAsync(relType);
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(source.Id, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(source);
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(target.Id, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        _entityRepo.Setup(r => r.CountRelationshipsBySourceAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _entityRepo.Setup(r => r.CountRelationshipsByTargetAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _entityRepo.Setup(r => r.AddRelationshipAsync(It.IsAny<EntityRelationship>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EntityRelationship e, CancellationToken _) => new EntityRelationship { Id = 55, SourceEntityId = e.SourceEntityId, TargetEntityId = e.TargetEntityId, RelationshipTypeId = e.RelationshipTypeId });
    }

    private static CreateEntityRelationshipRequest CreateReq(int typeId = 10, int sourceId = 1, int targetId = 2) =>
        new(sourceId, targetId, typeId);

    [Fact]
    public async Task CreateRelationshipAsync_HappyPath_AddsAndReturnsRef()
    {
        SetupCreate(RelType(), LinkEntity(1, SourceTypeId), LinkEntity(2, TargetTypeId));

        var result = await _sut.CreateRelationshipAsync(Ws, UserId, CreateReq());

        result.RelationshipId.Should().Be(55);
        result.RelatedEntityId.Should().Be(2);
        result.RelatedEntityTypeName.Should().Be("client");
        _entityRepo.Verify(r => r.AddRelationshipAsync(It.IsAny<EntityRelationship>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateRelationshipAsync_NoEditPermission_Throws()
    {
        _workspaceAccess.Setup(x => x.HasWorkspacePermissionAsync(UserId, Ws, "edit_entities", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var act = () => _sut.CreateRelationshipAsync(Ws, UserId, CreateReq());

        (await act.Should().ThrowAsync<AppException>()).Which.Message.Should().Contain("edit_entities");
    }

    [Fact]
    public async Task CreateRelationshipAsync_UnknownRelationshipType_Throws()
    {
        _entityRepo.Setup(r => r.GetRelationshipTypeByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync((EntityRelationshipType?)null);

        var act = () => _sut.CreateRelationshipAsync(Ws, UserId, CreateReq());

        (await act.Should().ThrowAsync<AppException>()).Which.Code.Should().Be("relationship_type_not_found");
    }

    [Fact]
    public async Task CreateRelationshipAsync_SourceNotInWorkspace_Throws()
    {
        _entityRepo.Setup(r => r.GetRelationshipTypeByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(RelType());
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(1, Ws, It.IsAny<CancellationToken>())).ReturnsAsync((Entity?)null);

        var act = () => _sut.CreateRelationshipAsync(Ws, UserId, CreateReq());

        (await act.Should().ThrowAsync<AppException>()).Which.Code.Should().Be("source_entity_not_found");
    }

    [Fact]
    public async Task CreateRelationshipAsync_TargetNotInWorkspace_Throws()
    {
        _entityRepo.Setup(r => r.GetRelationshipTypeByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(RelType());
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(1, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(LinkEntity(1, SourceTypeId));
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(2, Ws, It.IsAny<CancellationToken>())).ReturnsAsync((Entity?)null);

        var act = () => _sut.CreateRelationshipAsync(Ws, UserId, CreateReq());

        (await act.Should().ThrowAsync<AppException>()).Which.Code.Should().Be("target_entity_not_found");
    }

    [Fact]
    public async Task CreateRelationshipAsync_ArchivedEntity_Throws()
    {
        SetupCreate(RelType(), LinkEntity(1, SourceTypeId), LinkEntity(2, TargetTypeId, archived: true));

        var act = () => _sut.CreateRelationshipAsync(Ws, UserId, CreateReq());

        (await act.Should().ThrowAsync<AppException>()).Which.Code.Should().Be("relationship_archived_entity");
    }

    [Fact]
    public async Task CreateRelationshipAsync_AllReadonlyTargetType_Throws()
    {
        SetupCreate(RelType(), LinkEntity(1, SourceTypeId), LinkEntity(2, TargetTypeId));
        _entityRepo.Setup(r => r.GetTypePropertiesAsync(TargetTypeId, It.IsAny<CancellationToken>())).ReturnsAsync([ReadonlyProp()]);

        var act = () => _sut.CreateRelationshipAsync(Ws, UserId, CreateReq());

        (await act.Should().ThrowAsync<AppException>()).Which.Code.Should().Be("entity_all_readonly_link");
    }

    [Fact]
    public async Task CreateRelationshipAsync_SourceWrongType_Throws()
    {
        SetupCreate(RelType(), LinkEntity(1, 999), LinkEntity(2, TargetTypeId));

        var act = () => _sut.CreateRelationshipAsync(Ws, UserId, CreateReq());

        (await act.Should().ThrowAsync<AppException>()).Which.Code.Should().Be("source_entity_wrong_type");
    }

    [Fact]
    public async Task CreateRelationshipAsync_TargetWrongType_Throws()
    {
        SetupCreate(RelType(), LinkEntity(1, SourceTypeId), LinkEntity(2, 999));

        var act = () => _sut.CreateRelationshipAsync(Ws, UserId, CreateReq());

        (await act.Should().ThrowAsync<AppException>()).Which.Code.Should().Be("target_entity_wrong_type");
    }

    [Fact]
    public async Task CreateRelationshipAsync_ManyToOneSourceAlreadyLinked_Throws()
    {
        SetupCreate(RelType(RelationshipCardinality.ManyToOne), LinkEntity(1, SourceTypeId), LinkEntity(2, TargetTypeId));
        _entityRepo.Setup(r => r.CountRelationshipsBySourceAsync(1, 10, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var act = () => _sut.CreateRelationshipAsync(Ws, UserId, CreateReq());

        (await act.Should().ThrowAsync<AppException>()).Which.Code.Should().Be("source_cardinality_violation");
    }

    [Fact]
    public async Task CreateRelationshipAsync_OneToOneTargetAlreadyLinked_Throws()
    {
        SetupCreate(RelType(RelationshipCardinality.OneToOne), LinkEntity(1, SourceTypeId), LinkEntity(2, TargetTypeId));
        _entityRepo.Setup(r => r.CountRelationshipsByTargetAsync(2, 10, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var act = () => _sut.CreateRelationshipAsync(Ws, UserId, CreateReq());

        (await act.Should().ThrowAsync<AppException>()).Which.Code.Should().Be("target_cardinality_violation");
    }

    private EntityRelationship StoredRel(RelationshipCardinality cardinality = RelationshipCardinality.ManyToOne, bool required = false, int inWorkspace = Ws)
    {
        var relType = RelType(cardinality, required);
        var source = LinkEntity(1, SourceTypeId, inWorkspace: inWorkspace);
        var target = LinkEntity(2, TargetTypeId);
        return new EntityRelationship
        {
            Id = 55,
            SourceEntityId = source.Id,
            TargetEntityId = target.Id,
            RelationshipTypeId = relType.Id,
            SourceEntity = source,
            TargetEntity = target,
            RelationshipType = relType,
        };
    }

    [Fact]
    public async Task DeleteRelationshipAsync_HappyPath_Removes()
    {
        var rel = StoredRel();
        _entityRepo.Setup(r => r.GetRelationshipByIdAsync(55, It.IsAny<CancellationToken>())).ReturnsAsync(rel);

        await _sut.DeleteRelationshipAsync(Ws, UserId, 55);

        _entityRepo.Verify(r => r.RemoveRelationshipAsync(55, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteRelationshipAsync_NotFound_Throws()
    {
        _entityRepo.Setup(r => r.GetRelationshipByIdAsync(55, It.IsAny<CancellationToken>())).ReturnsAsync((EntityRelationship?)null);

        var act = () => _sut.DeleteRelationshipAsync(Ws, UserId, 55);

        (await act.Should().ThrowAsync<AppException>()).Which.Code.Should().Be("relationship_not_found");
    }

    [Fact]
    public async Task DeleteRelationshipAsync_NotInWorkspace_Throws()
    {
        var rel = StoredRel(inWorkspace: 999);
        _entityRepo.Setup(r => r.GetRelationshipByIdAsync(55, It.IsAny<CancellationToken>())).ReturnsAsync(rel);

        var act = () => _sut.DeleteRelationshipAsync(Ws, UserId, 55);

        (await act.Should().ThrowAsync<AppException>()).Which.Code.Should().Be("relationship_not_in_workspace");
    }

    [Fact]
    public async Task DeleteRelationshipAsync_LastRequiredLink_Throws()
    {
        var rel = StoredRel(required: true);
        _entityRepo.Setup(r => r.GetRelationshipByIdAsync(55, It.IsAny<CancellationToken>())).ReturnsAsync(rel);
        _entityRepo.Setup(r => r.CountRelationshipsBySourceAsync(1, 10, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var act = () => _sut.DeleteRelationshipAsync(Ws, UserId, 55);

        (await act.Should().ThrowAsync<AppException>()).Which.Code.Should().Be("cannot_unlink_required_relationship");
    }

    [Fact]
    public async Task DeleteRelationshipAsync_AllReadonlyTarget_Throws()
    {
        var rel = StoredRel();
        _entityRepo.Setup(r => r.GetRelationshipByIdAsync(55, It.IsAny<CancellationToken>())).ReturnsAsync(rel);
        _entityRepo.Setup(r => r.GetTypePropertiesAsync(TargetTypeId, It.IsAny<CancellationToken>())).ReturnsAsync([ReadonlyProp()]);

        var act = () => _sut.DeleteRelationshipAsync(Ws, UserId, 55);

        (await act.Should().ThrowAsync<AppException>()).Which.Code.Should().Be("entity_all_readonly_unlink");
    }

    [Fact]
    public async Task ReassignRelationshipAsync_BothEndpointsNull_Throws()
    {
        var act = () => _sut.ReassignRelationshipAsync(Ws, UserId, 55, new ReassignEntityRelationshipRequest(null, null));

        (await act.Should().ThrowAsync<AppException>()).Which.Code.Should().Be("relink_exactly_one_endpoint");
    }

    [Fact]
    public async Task ReassignRelationshipAsync_BothEndpointsSet_Throws()
    {
        var req = new ReassignEntityRelationshipRequest(3, 4);

        var act = () => _sut.ReassignRelationshipAsync(Ws, UserId, 55, req);

        (await act.Should().ThrowAsync<AppException>()).Which.Code.Should().Be("relink_exactly_one_endpoint");
    }

    [Fact]
    public async Task ReassignRelationshipAsync_NewTarget_HappyPath_UpdatesAndAudits()
    {
        var rel = StoredRel();
        _entityRepo.Setup(r => r.GetRelationshipByIdAsync(55, It.IsAny<CancellationToken>())).ReturnsAsync(rel);
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(9, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(LinkEntity(9, TargetTypeId));

        var result = await _sut.ReassignRelationshipAsync(Ws, UserId, 55, new ReassignEntityRelationshipRequest(null, 9));

        result.RelatedEntityId.Should().Be(9);
        _entityRepo.Verify(r => r.UpdateRelationshipTargetAsync(55, 9, It.IsAny<CancellationToken>()), Times.Once);
        _auditOutboxWriter.Verify(w => w.EnqueueAuditAsync(It.Is<AuditEventContract>(a => a.Action == "relationship_reassigned"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReassignRelationshipAsync_NewSource_HappyPath_UpdatesAndAudits()
    {
        var rel = StoredRel();
        _entityRepo.Setup(r => r.GetRelationshipByIdAsync(55, It.IsAny<CancellationToken>())).ReturnsAsync(rel);
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(8, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(LinkEntity(8, SourceTypeId));
        _entityRepo.Setup(r => r.CountRelationshipsBySourceAsync(8, 10, It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var result = await _sut.ReassignRelationshipAsync(Ws, UserId, 55, new ReassignEntityRelationshipRequest(8, null));

        result.RelatedEntityId.Should().Be(8);
        _entityRepo.Verify(r => r.UpdateRelationshipSourceAsync(55, 8, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReassignRelationshipAsync_NewTargetArchived_Throws()
    {
        var rel = StoredRel();
        _entityRepo.Setup(r => r.GetRelationshipByIdAsync(55, It.IsAny<CancellationToken>())).ReturnsAsync(rel);
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(9, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(LinkEntity(9, TargetTypeId, archived: true));

        var act = () => _sut.ReassignRelationshipAsync(Ws, UserId, 55, new ReassignEntityRelationshipRequest(null, 9));

        (await act.Should().ThrowAsync<AppException>()).Which.Code.Should().Be("cannot_link_archived_entity");
    }

    [Fact]
    public async Task ReassignRelationshipAsync_NewTargetWrongType_Throws()
    {
        var rel = StoredRel();
        _entityRepo.Setup(r => r.GetRelationshipByIdAsync(55, It.IsAny<CancellationToken>())).ReturnsAsync(rel);
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(9, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(LinkEntity(9, 999));

        var act = () => _sut.ReassignRelationshipAsync(Ws, UserId, 55, new ReassignEntityRelationshipRequest(null, 9));

        (await act.Should().ThrowAsync<AppException>()).Which.Code.Should().Be("entity_wrong_type_for_relationship");
    }

    [Fact]
    public async Task ReassignRelationshipAsync_NewTargetNotFound_Throws()
    {
        var rel = StoredRel();
        _entityRepo.Setup(r => r.GetRelationshipByIdAsync(55, It.IsAny<CancellationToken>())).ReturnsAsync(rel);
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(9, Ws, It.IsAny<CancellationToken>())).ReturnsAsync((Entity?)null);

        var act = () => _sut.ReassignRelationshipAsync(Ws, UserId, 55, new ReassignEntityRelationshipRequest(null, 9));

        (await act.Should().ThrowAsync<AppException>()).Which.Code.Should().Be("target_entity_not_found");
    }

    [Fact]
    public async Task ReassignRelationshipAsync_RelationshipNotFound_Throws()
    {
        _entityRepo.Setup(r => r.GetRelationshipByIdAsync(55, It.IsAny<CancellationToken>())).ReturnsAsync((EntityRelationship?)null);

        var act = () => _sut.ReassignRelationshipAsync(Ws, UserId, 55, new ReassignEntityRelationshipRequest(null, 9));

        (await act.Should().ThrowAsync<AppException>()).Which.Code.Should().Be("relationship_not_found");
    }
}
