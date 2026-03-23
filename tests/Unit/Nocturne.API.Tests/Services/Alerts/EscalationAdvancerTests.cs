using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Nocturne.API.Services.Alerts;
using Nocturne.Core.Contracts.Alerts;
using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.API.Tests.Services.Alerts;

[Trait("Category", "Unit")]
public class EscalationAdvancerTests
{
    private readonly Mock<IAlertRepository> _repository;
    private readonly Mock<IAlertDeliveryService> _deliveryService;
    private readonly FakeTimeProvider _timeProvider;
    private readonly Mock<ILogger<EscalationAdvancer>> _logger;
    private readonly EscalationAdvancer _sut;

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _scheduleId = Guid.NewGuid();
    private readonly Guid _excursionId = Guid.NewGuid();

    public EscalationAdvancerTests()
    {
        _repository = new Mock<IAlertRepository>();
        _deliveryService = new Mock<IAlertDeliveryService>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 3, 22, 12, 0, 0, TimeSpan.Zero));
        _logger = new Mock<ILogger<EscalationAdvancer>>();

        _sut = new EscalationAdvancer(
            _repository.Object,
            _deliveryService.Object,
            _timeProvider,
            _logger.Object);
    }

    private AlertInstanceSnapshot MakeInstance(int currentStepOrder = 0, string status = "escalating") =>
        new(Guid.NewGuid(), _tenantId, _excursionId, _scheduleId,
            currentStepOrder, status, DateTime.UtcNow, DateTime.UtcNow, null, 0);

    private AlertEscalationStepSnapshot MakeStep(int order, int delaySeconds = 300) =>
        new(Guid.NewGuid(), _scheduleId, order, delaySeconds);

    [Fact]
    public async Task AdvanceAsync_NoNextStep_SetsStatusTriggered()
    {
        var instance = MakeInstance(currentStepOrder: 0);
        var steps = new[] { MakeStep(0) };

        _repository.Setup(r => r.GetEscalationStepsAsync(_scheduleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(steps);

        await _sut.AdvanceAsync(instance, CancellationToken.None);

        _repository.Verify(r => r.UpdateInstanceAsync(
            It.Is<UpdateAlertInstanceRequest>(req =>
                req.Id == instance.Id &&
                req.Status == "triggered" &&
                req.NextEscalationAt == DateTime.MinValue),
            It.IsAny<CancellationToken>()), Times.Once);

        _deliveryService.Verify(d => d.DispatchAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<AlertPayload>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AdvanceAsync_HasNextStep_AdvancesAndDispatches()
    {
        var instance = MakeInstance(currentStepOrder: 0);
        var steps = new[] { MakeStep(0, 300), MakeStep(1, 600), MakeStep(2, 900) };
        var tenantContext = new TenantAlertContext(_tenantId, "Jane", "jane", "Jane Doe", true, DateTime.UtcNow);

        _repository.Setup(r => r.GetEscalationStepsAsync(_scheduleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(steps);
        _repository.Setup(r => r.GetTenantAlertContextAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantContext);
        _repository.Setup(r => r.CountActiveExcursionsAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        await _sut.AdvanceAsync(instance, CancellationToken.None);

        // Should advance to step 1, status stays "escalating" because step 2 exists
        _repository.Verify(r => r.UpdateInstanceAsync(
            It.Is<UpdateAlertInstanceRequest>(req =>
                req.Id == instance.Id &&
                req.CurrentStepOrder == 1 &&
                req.Status == "escalating"),
            It.IsAny<CancellationToken>()), Times.Once);

        _deliveryService.Verify(d => d.DispatchAsync(instance.Id, 1, It.IsAny<AlertPayload>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdvanceAsync_LastStep_SetsTriggeredAndDispatches()
    {
        var instance = MakeInstance(currentStepOrder: 0);
        var steps = new[] { MakeStep(0, 300), MakeStep(1, 600) };
        var tenantContext = new TenantAlertContext(_tenantId, "Alice", "alice", "Alice Smith", true, DateTime.UtcNow);

        _repository.Setup(r => r.GetEscalationStepsAsync(_scheduleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(steps);
        _repository.Setup(r => r.GetTenantAlertContextAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantContext);
        _repository.Setup(r => r.CountActiveExcursionsAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await _sut.AdvanceAsync(instance, CancellationToken.None);

        // Advancing from step 0 to step 1, but no step 2 exists, so status becomes "triggered"
        _repository.Verify(r => r.UpdateInstanceAsync(
            It.Is<UpdateAlertInstanceRequest>(req =>
                req.Id == instance.Id &&
                req.CurrentStepOrder == 1 &&
                req.Status == "triggered" &&
                req.NextEscalationAt == DateTime.MinValue),
            It.IsAny<CancellationToken>()), Times.Once);

        _deliveryService.Verify(d => d.DispatchAsync(instance.Id, 1, It.IsAny<AlertPayload>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdvanceAsync_BuildsPayloadWithTenantContext()
    {
        var instance = MakeInstance(currentStepOrder: 0);
        var steps = new[] { MakeStep(0, 300), MakeStep(1, 600) };
        var tenantContext = new TenantAlertContext(_tenantId, "Bob", "bob", "Bob Jones", true, DateTime.UtcNow);

        _repository.Setup(r => r.GetEscalationStepsAsync(_scheduleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(steps);
        _repository.Setup(r => r.GetTenantAlertContextAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantContext);
        _repository.Setup(r => r.CountActiveExcursionsAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        AlertPayload? capturedPayload = null;
        _deliveryService.Setup(d => d.DispatchAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<AlertPayload>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, int, AlertPayload, CancellationToken>((_, _, payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        await _sut.AdvanceAsync(instance, CancellationToken.None);

        capturedPayload.Should().NotBeNull();
        capturedPayload!.SubjectName.Should().Be("Bob");
        capturedPayload.TenantId.Should().Be(_tenantId);
        capturedPayload.ExcursionId.Should().Be(_excursionId);
        capturedPayload.InstanceId.Should().Be(instance.Id);
        capturedPayload.ActiveExcursionCount.Should().Be(3);
        capturedPayload.AlertType.Should().Be("escalation");
    }
}
