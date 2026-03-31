using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nocturne.API.Services;
using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;
using Nocturne.Core.Contracts.Multitenancy;
using Xunit;

namespace Nocturne.API.Tests.Services;

public class ConnectorSyncServiceTests
{
    private static ConnectorSyncService CreateService(
        IServiceProvider serviceProvider,
        ITenantAccessor? tenantAccessor = null)
    {
        var ta = tenantAccessor ?? CreateTenantAccessor();
        var logger = NullLogger<ConnectorSyncService>.Instance;
        var progressReporter = Mock.Of<ISyncProgressReporter>();
        return new ConnectorSyncService(serviceProvider, ta, logger, progressReporter);
    }

    private static ITenantAccessor CreateTenantAccessor(TenantContext? context = null)
    {
        var mock = new Mock<ITenantAccessor>();
        mock.Setup(x => x.Context).Returns(context);
        return mock.Object;
    }

    private static IServiceProvider BuildProvider(
        params IConnectorSyncExecutor[] executors)
    {
        var services = new ServiceCollection();

        // Register a scoped ITenantAccessor so the service can resolve it in the child scope
        services.AddScoped<ITenantAccessor>(_ =>
        {
            var mock = new Mock<ITenantAccessor>();
            return mock.Object;
        });

        foreach (var executor in executors)
        {
            services.AddSingleton<IConnectorSyncExecutor>(executor);
        }

        return services.BuildServiceProvider();
    }

    private static IConnectorSyncExecutor CreateMockExecutor(
        string connectorId,
        SyncResult? result = null)
    {
        var mock = new Mock<IConnectorSyncExecutor>();
        mock.Setup(x => x.ConnectorId).Returns(connectorId);
        mock.Setup(x => x.ExecuteSyncAsync(
                It.IsAny<IServiceProvider>(),
                It.IsAny<SyncRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result ?? new SyncResult { Success = true, Message = "OK" });
        return mock.Object;
    }

    [Fact]
    public async Task TriggerSyncAsync_WithKnownConnector_DelegatesToExecutor()
    {
        // Arrange
        var expectedResult = new SyncResult { Success = true, Message = "Synced 42 entries" };
        var executor = CreateMockExecutor("test", expectedResult);
        var provider = BuildProvider(executor);
        var sut = CreateService(provider);
        var request = new SyncRequest();

        // Act
        var result = await sut.TriggerSyncAsync("test", request, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(expectedResult);
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Synced 42 entries");

        Mock.Get(executor).Verify(
            x => x.ExecuteSyncAsync(
                It.IsAny<IServiceProvider>(),
                request,
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task TriggerSyncAsync_WithUnknownConnector_ReturnsFailure()
    {
        // Arrange - no executors registered
        var provider = BuildProvider();
        var sut = CreateService(provider);
        var request = new SyncRequest();

        // Act
        var result = await sut.TriggerSyncAsync("nonexistent", request, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Unknown connector");
    }

    [Fact]
    public async Task TriggerSyncAsync_IsCaseInsensitive()
    {
        // Arrange - register with lowercase, trigger with uppercase
        var expectedResult = new SyncResult { Success = true, Message = "Dexcom sync complete" };
        var executor = CreateMockExecutor("dexcom", expectedResult);
        var provider = BuildProvider(executor);
        var sut = CreateService(provider);
        var request = new SyncRequest();

        // Act
        var result = await sut.TriggerSyncAsync("DEXCOM", request, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(expectedResult);
        result.Success.Should().BeTrue();

        Mock.Get(executor).Verify(
            x => x.ExecuteSyncAsync(
                It.IsAny<IServiceProvider>(),
                request,
                CancellationToken.None),
            Times.Once);
    }
}
