using Relativa.Core.Application.DTOs.WsJoinRequest;

namespace Relativa.Core.Application.Interfaces;

public interface IWsJoinRequestService
{
    Task<WsJoinRequestDto> SubmitAsync(int workspaceId, int userId, CreateWsJoinRequestRequest request, CancellationToken ct = default);
    Task<List<WsJoinRequestDto>> GetByWorkspaceAsync(int workspaceId, int callerUserId, CancellationToken ct = default);
    Task ReviewAsync(int workspaceId, int requestId, int callerUserId, ReviewWsJoinRequestRequest request, CancellationToken ct = default);
    Task<List<WsJoinRequestDto>> GetMyRequestsAsync(int userId, CancellationToken ct = default);
}
