using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using SNIF.SignalR.Hubs;
using System.Security.Claims;

namespace SNIF.Tests.Hubs;

public class MatchHubTests
{
    private readonly Mock<ILogger<MatchHub>> _logger = new();
    private readonly Mock<HubCallerContext> _callerContext = new();
    private readonly Mock<IHubCallerClients> _clients = new();
    private readonly Mock<IGroupManager> _groups = new();

    private MatchHub CreateHub(string? userId)
    {
        if (userId != null)
        {
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            _callerContext.SetupGet(context => context.User).Returns(principal);
        }
        else
        {
            _callerContext.SetupGet(context => context.User).Returns((ClaimsPrincipal?)null);
        }

        _callerContext.SetupGet(context => context.ConnectionId).Returns($"connection-{userId}");
        _groups.Setup(groups => groups.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new MatchHub(_logger.Object)
        {
            Context = _callerContext.Object,
            Clients = _clients.Object,
            Groups = _groups.Object,
        };
    }

    [Fact]
    public async Task JoinUserGroup_WithMatchingUserId_Succeeds()
    {
        var hub = CreateHub("user-1");

        await hub.JoinUserGroup("user-1");

        _groups.Verify(groups => groups.AddToGroupAsync("connection-user-1", "user-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task JoinUserGroup_WithMismatchedUserId_ThrowsHubException()
    {
        var hub = CreateHub("user-1");

        var action = () => hub.JoinUserGroup("user-2");

        await action.Should().ThrowAsync<HubException>()
            .WithMessage("You can only join your own user group.");
        _groups.Verify(groups => groups.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task JoinUserGroup_WithNullAuthenticatedUser_ThrowsHubException()
    {
        var hub = CreateHub(null);

        var action = () => hub.JoinUserGroup("user-1");

        await action.Should().ThrowAsync<HubException>()
            .WithMessage("You can only join your own user group.");
        _groups.Verify(groups => groups.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task JoinUserGroup_WithEmptyUserId_ThrowsHubException()
    {
        var hub = CreateHub("user-1");

        var action = () => hub.JoinUserGroup("");

        await action.Should().ThrowAsync<HubException>()
            .WithMessage("You can only join your own user group.");
        _groups.Verify(groups => groups.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
