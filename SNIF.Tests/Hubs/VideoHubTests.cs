using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SNIF.Core.DTOs;
using SNIF.Core.Enums;
using SNIF.Core.Interfaces;
using SNIF.Infrastructure.Data;
using SNIF.SignalR.Hubs;

namespace SNIF.Tests.Hubs;

public class VideoHubTests
{
    private readonly Mock<ILogger<VideoHub>> _logger = new();
    private readonly Mock<IMatchService> _matchService = new();
    private readonly Mock<IPushNotificationService> _pushNotifications = new();
    private readonly Mock<IUsageService> _usageService = new();
    private readonly Mock<HubCallerContext> _callerContext = new();
    private readonly Mock<IHubCallerClients> _clients = new();
    private readonly Mock<IGroupManager> _groups = new();
    private readonly Mock<IClientProxy> _clientProxy = new();

    private static SNIFContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SNIFContext>()
            .UseInMemoryDatabase($"VideoHubTests_{Guid.NewGuid()}")
            .Options;

        return new SNIFContext(options);
    }

    private VideoHub CreateHub(string userId, SNIFContext dbContext)
    {
        _callerContext.SetupGet(context => context.UserIdentifier).Returns(userId);
        _callerContext.SetupGet(context => context.ConnectionId).Returns($"connection-{userId}");
        _clients.Setup(clients => clients.User(It.IsAny<string>())).Returns(_clientProxy.Object);
        _clientProxy.Setup(proxy => proxy.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _groups.Setup(groups => groups.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new VideoHub(
        _logger.Object,
        dbContext,
        _matchService.Object,
        _pushNotifications.Object,
        _usageService.Object)
        {
            Context = _callerContext.Object,
            Clients = _clients.Object,
            Groups = _groups.Object,
        };
    }

    [Fact]
    public async Task InitiateCall_IgnoresClientSuppliedReceiverId_AndUsesDerivedPeer()
    {
        using var context = CreateContext();
        var hub = CreateHub("caller-user", context);

        _usageService.Setup(service => service.CanPerformAction("caller-user", UsageType.VideoCall))
            .ReturnsAsync(new UsageCheckResult { Allowed = true, Source = UsageSource.PlanQuota });
        _matchService.Setup(service => service.GetPeerUserIdAsync("match-1", "caller-user"))
            .ReturnsAsync("derived-peer");

        await hub.InitiateCall("match-1", "spoofed-user");

        _clients.Verify(clients => clients.User("derived-peer"), Times.Once);
        _clients.Verify(clients => clients.User("spoofed-user"), Times.Never);
        _clientProxy.Verify(proxy => proxy.SendCoreAsync(
            "IncomingCall",
            It.Is<object?[]>(args => args.Length == 2 && Equals(args[0], "caller-user") && Equals(args[1], "match-1")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendSignal_IgnoresClientSuppliedReceiverId_AndUsesDerivedPeer()
    {
        using var context = CreateContext();
        var hub = CreateHub("caller-user", context);

        _matchService.Setup(service => service.GetPeerUserIdAsync("match-1", "caller-user"))
            .ReturnsAsync("derived-peer");

        await hub.SendSignal("match-1", "spoofed-user", "signal-payload");

        _clients.Verify(clients => clients.User("derived-peer"), Times.Once);
        _clients.Verify(clients => clients.User("spoofed-user"), Times.Never);
        _clientProxy.Verify(proxy => proxy.SendCoreAsync(
            "ReceiveSignal",
            It.Is<object?[]>(args => args.Length == 1 && Equals(args[0], "signal-payload")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}