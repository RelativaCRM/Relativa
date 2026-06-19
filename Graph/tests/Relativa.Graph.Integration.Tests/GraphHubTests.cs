using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using Relativa.Graph.Hubs;
using Xunit;

namespace Relativa.Graph.Integration.Tests;

public sealed class GraphHubTests
{
    private readonly IGroupManager _groups = Substitute.For<IGroupManager>();
    private readonly GraphHub _sut;

    public GraphHubTests()
    {
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns("conn-1");
        _sut = new GraphHub { Groups = _groups, Context = context };
    }

    [Fact]
    public async Task JoinWorkspace_AddsConnectionToWorkspaceScopedGroup()
    {
        await _sut.JoinWorkspace(42);

        await _groups.Received(1).AddToGroupAsync("conn-1", "workspace-42", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LeaveWorkspace_RemovesConnectionFromWorkspaceScopedGroup()
    {
        await _sut.LeaveWorkspace(42);

        await _groups.Received(1).RemoveFromGroupAsync("conn-1", "workspace-42", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnConnectedAsync_CompletesWithoutTouchingGroups()
    {
        await _sut.OnConnectedAsync();

        await _groups.DidNotReceiveWithAnyArgs().AddToGroupAsync(default!, default!);
    }
}
