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

public sealed class EntityServiceRelationshipTests
{
    private readonly Mock<IEntityRepository> _entityRepo = new();
    private readonly Mock<IWorkspaceAccessEvaluator> _workspaceAccess = new();
    private readonly Mock<IUserRoleWorkspaceRepository> _memberRepo = new();
    private readonly Mock<IOutboxWriter> _auditOutboxWriter = new();
    private readonly Mock<IEntityRelationshipNotifier> _notifier = new();
    private readonly Mock<IValidator<CreateEntityRequest>> _createValidator = new();
    private readonly Mock<IValidator<UpdateEntityRequest>> _updateValidator = new();
    private readonly EntityService _sut;

    private const int Ws = 1;
    private const int User = 2;

    public EntityServiceRelationshipTests()
    {
        _workspaceAccess
            .Setup(x => x.HasWorkspacePermissionAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
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
            _entityRepo.Object,
            _workspaceAccess.Object,
            _memberRepo.Object,
            _createValidator.Object,
            _updateValidator.Object,
            _auditOutboxWriter.Object,
            _notifier.Object);
    }

    private static EntityRelationshipType RelType(RelationshipCardinality cardinality, int sourceTypeId = 1, int targetTypeId = 2) =>
        new()
        {
            Id = 50,
            Name = "owns",
            SourceEntityTypeId = sourceTypeId,
            TargetEntityTypeId = targetTypeId,
            RelationshipCardinality = cardinality,
            SourceEntityType = new EntityType { Id = sourceTypeId, Name = "src" },
            TargetEntityType = new EntityType { Id = targetTypeId, Name = "tgt" },
        };

    private static Entity SimpleEntity(int id, int typeId, bool archived = false) =>
        new()
        {
            Id = id,
            EntityTypeId = typeId,
            CreatedByUserId = User,
            IsArchived = archived,
            EntityType = new EntityType { Id = typeId, Name = "t" + typeId },
            EntityWorkspaces = [new EntityWorkspace { EntityId = id, WorkspaceId = Ws }],
        };

    private static List<EntityTypeProperty> AllReadonlyProps() =>
        [new EntityTypeProperty { PropertyId = 1, Property = new Property { Id = 1, Name = "p", IsReadonly = true } }];

    [Fact]
    public async Task Create_ManyToOne_ExistingSourceLink_Throws()
    {
        _entityRepo.Setup(r => r.GetRelationshipTypeByIdAsync(50, It.IsAny<CancellationToken>())).ReturnsAsync(RelType(RelationshipCardinality.ManyToOne));
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(100, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(SimpleEntity(100, 1));
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(200, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(SimpleEntity(200, 2));
        _entityRepo.Setup(r => r.GetTypePropertiesAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _entityRepo.Setup(r => r.CountRelationshipsBySourceAsync(100, 50, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _sut.Invoking(s => s.CreateRelationshipAsync(Ws, User, new CreateEntityRelationshipRequest(100, 200, 50)))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "source_cardinality_violation");
    }

    [Fact]
    public async Task Create_OneToOne_ExistingTargetLink_Throws()
    {
        _entityRepo.Setup(r => r.GetRelationshipTypeByIdAsync(50, It.IsAny<CancellationToken>())).ReturnsAsync(RelType(RelationshipCardinality.OneToOne));
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(100, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(SimpleEntity(100, 1));
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(200, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(SimpleEntity(200, 2));
        _entityRepo.Setup(r => r.GetTypePropertiesAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _entityRepo.Setup(r => r.CountRelationshipsBySourceAsync(100, 50, It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _entityRepo.Setup(r => r.CountRelationshipsByTargetAsync(200, 50, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _sut.Invoking(s => s.CreateRelationshipAsync(Ws, User, new CreateEntityRelationshipRequest(100, 200, 50)))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "target_cardinality_violation");
    }

    [Fact]
    public async Task Create_ManyToOne_Success_NotifiesAndReturnsRef()
    {
        _entityRepo.Setup(r => r.GetRelationshipTypeByIdAsync(50, It.IsAny<CancellationToken>())).ReturnsAsync(RelType(RelationshipCardinality.ManyToOne));
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(100, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(SimpleEntity(100, 1));
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(200, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(SimpleEntity(200, 2));
        _entityRepo.Setup(r => r.GetTypePropertiesAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _entityRepo.Setup(r => r.CountRelationshipsBySourceAsync(100, 50, It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _entityRepo.Setup(r => r.AddRelationshipAsync(It.IsAny<EntityRelationship>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EntityRelationship er, CancellationToken _) => { er.Id = 555; return er; });

        var result = await _sut.CreateRelationshipAsync(Ws, User, new CreateEntityRelationshipRequest(100, 200, 50));

        result.RelationshipId.Should().Be(555);
        result.RelatedEntityId.Should().Be(200);
        _notifier.Verify(n => n.NotifyChangedAsync(Ws, It.IsAny<CancellationToken>(), 100, 200), Times.Once);
    }

    [Fact]
    public async Task Reassign_NeitherEndpoint_Throws()
    {
        await _sut.Invoking(s => s.ReassignRelationshipAsync(Ws, User, 1, new ReassignEntityRelationshipRequest(null, null)))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "relink_exactly_one_endpoint");
    }

    [Fact]
    public async Task Reassign_BothEndpoints_Throws()
    {
        await _sut.Invoking(s => s.ReassignRelationshipAsync(Ws, User, 1, new ReassignEntityRelationshipRequest(300, 400)))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "relink_exactly_one_endpoint");
    }

    [Fact]
    public async Task Reassign_NewTarget_Success_EnqueuesAuditAndNotifies()
    {
        var existing = new EntityRelationship
        {
            Id = 1,
            SourceEntityId = 100,
            TargetEntityId = 200,
            RelationshipTypeId = 50,
            RelationshipType = RelType(RelationshipCardinality.ManyToOne),
            SourceEntity = SimpleEntity(100, 1),
        };
        _entityRepo.Setup(r => r.GetRelationshipByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(201, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(SimpleEntity(201, 2));
        _entityRepo.Setup(r => r.GetTypePropertiesAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await _sut.ReassignRelationshipAsync(Ws, User, 1, new ReassignEntityRelationshipRequest(null, 201));

        result.RelatedEntityId.Should().Be(201);
        _entityRepo.Verify(r => r.UpdateRelationshipTargetAsync(1, 201, It.IsAny<CancellationToken>()), Times.Once);
        _auditOutboxWriter.Verify(w => w.EnqueueAuditAsync(It.IsAny<AuditEventContract>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _notifier.Verify(n => n.NotifyChangedAsync(Ws, It.IsAny<CancellationToken>(), It.IsAny<int[]>()), Times.Once);
    }

    [Fact]
    public async Task Reassign_NewSource_Success_UpdatesSource()
    {
        var existing = new EntityRelationship
        {
            Id = 1,
            SourceEntityId = 100,
            TargetEntityId = 200,
            RelationshipTypeId = 50,
            RelationshipType = RelType(RelationshipCardinality.ManyToOne),
            SourceEntity = SimpleEntity(100, 1),
            TargetEntity = SimpleEntity(200, 2),
        };
        _entityRepo.Setup(r => r.GetRelationshipByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(101, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(SimpleEntity(101, 1));
        _entityRepo.Setup(r => r.GetTypePropertiesAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _entityRepo.Setup(r => r.CountRelationshipsBySourceAsync(101, 50, It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var result = await _sut.ReassignRelationshipAsync(Ws, User, 1, new ReassignEntityRelationshipRequest(101, null));

        result.Should().NotBeNull();
        _entityRepo.Verify(r => r.UpdateRelationshipSourceAsync(1, 101, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reassign_InboundSwap_RelationshipTargetInOtherWorkspace_Throws()
    {
        var target = new Entity
        {
            Id = 200, EntityTypeId = 2, CreatedByUserId = User,
            EntityType = new EntityType { Id = 2, Name = "t2" },
            EntityWorkspaces = [new EntityWorkspace { EntityId = 200, WorkspaceId = 999 }],
        };
        var rel = new EntityRelationship
        {
            Id = 1, SourceEntityId = 100, TargetEntityId = 200, RelationshipTypeId = 50,
            RelationshipType = RelType(RelationshipCardinality.ManyToOne),
            SourceEntity = SimpleEntity(100, 1),
            TargetEntity = target,
        };
        _entityRepo.Setup(r => r.GetRelationshipByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(rel);

        await _sut.Invoking(s => s.ReassignRelationshipAsync(Ws, User, 1, new ReassignEntityRelationshipRequest(101, null)))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "relationship_not_in_workspace" && e.StatusCode == 403);
    }

    [Fact]
    public async Task Create_ArchivedEndpoint_Throws()
    {
        _entityRepo.Setup(r => r.GetRelationshipTypeByIdAsync(50, It.IsAny<CancellationToken>())).ReturnsAsync(RelType(RelationshipCardinality.ManyToOne));
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(100, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(SimpleEntity(100, 1));
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(200, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(SimpleEntity(200, 2, archived: true));

        await _sut.Invoking(s => s.CreateRelationshipAsync(Ws, User, new CreateEntityRelationshipRequest(100, 200, 50)))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "relationship_archived_entity");
    }

    [Fact]
    public async Task Create_TargetAllReadonly_Throws()
    {
        _entityRepo.Setup(r => r.GetRelationshipTypeByIdAsync(50, It.IsAny<CancellationToken>())).ReturnsAsync(RelType(RelationshipCardinality.ManyToOne));
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(100, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(SimpleEntity(100, 1));
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(200, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(SimpleEntity(200, 2));
        _entityRepo.Setup(r => r.GetTypePropertiesAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(AllReadonlyProps());

        await _sut.Invoking(s => s.CreateRelationshipAsync(Ws, User, new CreateEntityRelationshipRequest(100, 200, 50)))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "entity_all_readonly_link");
    }

    [Fact]
    public async Task Create_SourceWrongType_Throws()
    {
        _entityRepo.Setup(r => r.GetRelationshipTypeByIdAsync(50, It.IsAny<CancellationToken>())).ReturnsAsync(RelType(RelationshipCardinality.ManyToOne, sourceTypeId: 1, targetTypeId: 2));
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(100, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(SimpleEntity(100, 9));
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(200, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(SimpleEntity(200, 2));
        _entityRepo.Setup(r => r.GetTypePropertiesAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await _sut.Invoking(s => s.CreateRelationshipAsync(Ws, User, new CreateEntityRelationshipRequest(100, 200, 50)))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "source_entity_wrong_type");
    }

    [Fact]
    public async Task Delete_RelationshipNotFound_Throws()
    {
        _entityRepo.Setup(r => r.GetRelationshipByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((EntityRelationship?)null);

        await _sut.Invoking(s => s.DeleteRelationshipAsync(Ws, User, 1))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "relationship_not_found");
    }

    [Fact]
    public async Task Delete_RelationshipNotInWorkspace_Throws()
    {
        var source = SimpleEntity(100, 1);
        source.EntityWorkspaces = [new EntityWorkspace { EntityId = 100, WorkspaceId = 999 }];
        var rel = new EntityRelationship
        {
            Id = 1, SourceEntityId = 100, TargetEntityId = 200, RelationshipTypeId = 50,
            RelationshipType = RelType(RelationshipCardinality.ManyToOne), SourceEntity = source,
        };
        _entityRepo.Setup(r => r.GetRelationshipByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(rel);

        await _sut.Invoking(s => s.DeleteRelationshipAsync(Ws, User, 1))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "relationship_not_in_workspace");
    }

    [Fact]
    public async Task Delete_RequiredLastLink_Throws()
    {
        var relType = RelType(RelationshipCardinality.ManyToOne);
        relType.IsRequired = true;
        var rel = new EntityRelationship
        {
            Id = 1, SourceEntityId = 100, TargetEntityId = 200, RelationshipTypeId = 50,
            RelationshipType = relType, SourceEntity = SimpleEntity(100, 1), TargetEntity = SimpleEntity(200, 2),
        };
        _entityRepo.Setup(r => r.GetRelationshipByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(rel);
        _entityRepo.Setup(r => r.CountRelationshipsBySourceAsync(100, 50, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _sut.Invoking(s => s.DeleteRelationshipAsync(Ws, User, 1))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "cannot_unlink_required_relationship");
    }

    [Fact]
    public async Task Delete_Valid_RemovesAndNotifies()
    {
        var rel = new EntityRelationship
        {
            Id = 1, SourceEntityId = 100, TargetEntityId = 200, RelationshipTypeId = 50,
            RelationshipType = RelType(RelationshipCardinality.ManyToOne), SourceEntity = SimpleEntity(100, 1), TargetEntity = SimpleEntity(200, 2),
        };
        _entityRepo.Setup(r => r.GetRelationshipByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(rel);
        _entityRepo.Setup(r => r.GetTypePropertiesAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await _sut.DeleteRelationshipAsync(Ws, User, 1);

        _entityRepo.Verify(r => r.RemoveRelationshipAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        _notifier.Verify(n => n.NotifyChangedAsync(Ws, It.IsAny<CancellationToken>(), 100, 200), Times.Once);
    }

    [Fact]
    public async Task Reassign_NewTargetArchived_Throws()
    {
        var rel = new EntityRelationship
        {
            Id = 1, SourceEntityId = 100, TargetEntityId = 200, RelationshipTypeId = 50,
            RelationshipType = RelType(RelationshipCardinality.ManyToOne), SourceEntity = SimpleEntity(100, 1),
        };
        _entityRepo.Setup(r => r.GetRelationshipByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(rel);
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(201, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(SimpleEntity(201, 2, archived: true));

        await _sut.Invoking(s => s.ReassignRelationshipAsync(Ws, User, 1, new ReassignEntityRelationshipRequest(null, 201)))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "cannot_link_archived_entity");
    }

    [Fact]
    public async Task Reassign_NewTargetWrongType_Throws()
    {
        var rel = new EntityRelationship
        {
            Id = 1, SourceEntityId = 100, TargetEntityId = 200, RelationshipTypeId = 50,
            RelationshipType = RelType(RelationshipCardinality.ManyToOne), SourceEntity = SimpleEntity(100, 1),
        };
        _entityRepo.Setup(r => r.GetRelationshipByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(rel);
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(201, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(SimpleEntity(201, 9));

        await _sut.Invoking(s => s.ReassignRelationshipAsync(Ws, User, 1, new ReassignEntityRelationshipRequest(null, 201)))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "entity_wrong_type_for_relationship");
    }

    [Fact]
    public async Task Reassign_NewTargetOneToOneCardinality_Throws()
    {
        var rel = new EntityRelationship
        {
            Id = 1, SourceEntityId = 100, TargetEntityId = 200, RelationshipTypeId = 50,
            RelationshipType = RelType(RelationshipCardinality.OneToOne), SourceEntity = SimpleEntity(100, 1),
        };
        _entityRepo.Setup(r => r.GetRelationshipByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(rel);
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(201, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(SimpleEntity(201, 2));
        _entityRepo.Setup(r => r.GetTypePropertiesAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _entityRepo.Setup(r => r.CountRelationshipsByTargetAsync(201, 50, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _sut.Invoking(s => s.ReassignRelationshipAsync(Ws, User, 1, new ReassignEntityRelationshipRequest(null, 201)))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "target_cardinality_violation");
    }

    [Fact]
    public async Task Reassign_NewSourceWrongType_Throws()
    {
        var rel = new EntityRelationship
        {
            Id = 1, SourceEntityId = 100, TargetEntityId = 200, RelationshipTypeId = 50,
            RelationshipType = RelType(RelationshipCardinality.ManyToOne), SourceEntity = SimpleEntity(100, 1),
            TargetEntity = SimpleEntity(200, 2),
        };
        _entityRepo.Setup(r => r.GetRelationshipByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(rel);
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(101, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(SimpleEntity(101, 9));

        await _sut.Invoking(s => s.ReassignRelationshipAsync(Ws, User, 1, new ReassignEntityRelationshipRequest(101, null)))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "entity_wrong_type_for_relationship");
    }

    [Fact]
    public async Task Reassign_NewSourceCardinality_Throws()
    {
        var rel = new EntityRelationship
        {
            Id = 1, SourceEntityId = 100, TargetEntityId = 200, RelationshipTypeId = 50,
            RelationshipType = RelType(RelationshipCardinality.OneToOne), SourceEntity = SimpleEntity(100, 1),
            TargetEntity = SimpleEntity(200, 2),
        };
        _entityRepo.Setup(r => r.GetRelationshipByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(rel);
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(101, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(SimpleEntity(101, 1));
        _entityRepo.Setup(r => r.GetTypePropertiesAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _entityRepo.Setup(r => r.CountRelationshipsBySourceAsync(101, 50, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _sut.Invoking(s => s.ReassignRelationshipAsync(Ws, User, 1, new ReassignEntityRelationshipRequest(101, null)))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "source_cardinality_violation");
    }
}
