using Relativa.Core.Application.DTOs.JoinRequest;

namespace Relativa.Core.Application.Interfaces;

public interface IJoinRequestService
{
    Task<JoinRequestDto> SubmitAsync(int organizationId, int userId, CreateJoinRequestRequest request, CancellationToken ct = default);
    Task<List<JoinRequestDto>> GetByOrganizationAsync(int organizationId, int callerUserId, CancellationToken ct = default);
    Task ReviewAsync(int organizationId, int requestId, int callerUserId, ReviewJoinRequestRequest request, CancellationToken ct = default);
    Task<List<JoinRequestDto>> GetMyRequestsAsync(int userId, CancellationToken ct = default);
}
