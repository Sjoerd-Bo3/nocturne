using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data.Entities.V4;
using Nocturne.Infrastructure.Data.Mappers.V4;

namespace Nocturne.Infrastructure.Data.Tests.Mappers.V4;

public class MicroBolusMapperTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ToEntity_MapsAllFields()
    {
        var id = Guid.CreateVersion7();
        var correlationId = Guid.NewGuid();
        var pumpDeviceId = Guid.NewGuid();
        var model = new MicroBolus
        {
            Id = id,
            Mills = 1700000000000,
            Insulin = 0.15,
            Device = "aaps-phone",
            App = "aaps",
            UtcOffset = -300,
            DataSource = "nightscout",
            CorrelationId = correlationId,
            LegacyId = "smb123",
            SyncIdentifier = "aaps-sync-abc123",
            PumpDeviceId = pumpDeviceId,
            PumpRecordId = "pump-rec-001"
        };

        var entity = MicroBolusMapper.ToEntity(model);

        entity.Id.Should().Be(id);
        entity.Mills.Should().Be(1700000000000);
        entity.Insulin.Should().Be(0.15);
        entity.Device.Should().Be("aaps-phone");
        entity.App.Should().Be("aaps");
        entity.UtcOffset.Should().Be(-300);
        entity.DataSource.Should().Be("nightscout");
        entity.CorrelationId.Should().Be(correlationId);
        entity.LegacyId.Should().Be("smb123");
        entity.SyncIdentifier.Should().Be("aaps-sync-abc123");
        entity.PumpDeviceId.Should().Be(pumpDeviceId);
        entity.PumpRecordId.Should().Be("pump-rec-001");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ToEntity_EmptyGuid_GeneratesNewId()
    {
        var model = new MicroBolus { Mills = 1700000000000, Insulin = 0.1 };

        var entity = MicroBolusMapper.ToEntity(model);

        entity.Id.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ToEntity_NullOptionalFields_MapsToNull()
    {
        var model = new MicroBolus
        {
            Mills = 1700000000000,
            Insulin = 0.1,
            SyncIdentifier = null,
            PumpDeviceId = null,
            PumpRecordId = null,
            Device = null,
            App = null,
            DataSource = null,
            CorrelationId = null,
            LegacyId = null,
            UtcOffset = null
        };

        var entity = MicroBolusMapper.ToEntity(model);

        entity.SyncIdentifier.Should().BeNull();
        entity.PumpDeviceId.Should().BeNull();
        entity.PumpRecordId.Should().BeNull();
        entity.Device.Should().BeNull();
        entity.App.Should().BeNull();
        entity.DataSource.Should().BeNull();
        entity.CorrelationId.Should().BeNull();
        entity.LegacyId.Should().BeNull();
        entity.UtcOffset.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ToEntity_SetsTimestamps()
    {
        var beforeCreate = DateTime.UtcNow;
        var model = new MicroBolus { Mills = 1700000000000, Insulin = 0.1 };

        var entity = MicroBolusMapper.ToEntity(model);

        entity.SysCreatedAt.Should().BeOnOrAfter(beforeCreate);
        entity.SysUpdatedAt.Should().BeOnOrAfter(beforeCreate);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ToDomainModel_MapsAllFields()
    {
        var id = Guid.CreateVersion7();
        var correlationId = Guid.NewGuid();
        var pumpDeviceId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddHours(-1);
        var updatedAt = DateTime.UtcNow;
        var entity = new MicroBolusEntity
        {
            Id = id,
            Mills = 1700000000000,
            Insulin = 0.15,
            Device = "aaps-phone",
            App = "aaps",
            UtcOffset = -300,
            DataSource = "nightscout",
            CorrelationId = correlationId,
            LegacyId = "smb123",
            SysCreatedAt = createdAt,
            SysUpdatedAt = updatedAt,
            SyncIdentifier = "aaps-sync-abc123",
            PumpDeviceId = pumpDeviceId,
            PumpRecordId = "pump-rec-001"
        };

        var model = MicroBolusMapper.ToDomainModel(entity);

        model.Id.Should().Be(id);
        model.Mills.Should().Be(1700000000000);
        model.Insulin.Should().Be(0.15);
        model.Device.Should().Be("aaps-phone");
        model.App.Should().Be("aaps");
        model.UtcOffset.Should().Be(-300);
        model.DataSource.Should().Be("nightscout");
        model.CorrelationId.Should().Be(correlationId);
        model.LegacyId.Should().Be("smb123");
        model.CreatedAt.Should().Be(createdAt);
        model.ModifiedAt.Should().Be(updatedAt);
        model.SyncIdentifier.Should().Be("aaps-sync-abc123");
        model.PumpDeviceId.Should().Be(pumpDeviceId);
        model.PumpRecordId.Should().Be("pump-rec-001");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ToDomainModel_NullOptionalFields_ReturnsNull()
    {
        var entity = new MicroBolusEntity
        {
            Id = Guid.CreateVersion7(),
            Mills = 1700000000000,
            Insulin = 0.1,
            SyncIdentifier = null,
            PumpDeviceId = null,
            PumpRecordId = null
        };

        var model = MicroBolusMapper.ToDomainModel(entity);

        model.SyncIdentifier.Should().BeNull();
        model.PumpDeviceId.Should().BeNull();
        model.PumpRecordId.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UpdateEntity_UpdatesAllFieldsExceptIdAndCreatedAt()
    {
        var originalId = Guid.CreateVersion7();
        var originalCreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newCorrelationId = Guid.NewGuid();
        var newPumpDeviceId = Guid.NewGuid();
        var entity = new MicroBolusEntity
        {
            Id = originalId,
            SysCreatedAt = originalCreatedAt,
            Mills = 1000,
            Insulin = 0.05
        };

        var model = new MicroBolus
        {
            Insulin = 0.20,
            Mills = 1700000000000,
            Device = "dana-rs",
            App = "aaps",
            UtcOffset = 60,
            DataSource = "tidepool",
            CorrelationId = newCorrelationId,
            LegacyId = "upd456",
            SyncIdentifier = "sync-updated",
            PumpDeviceId = newPumpDeviceId,
            PumpRecordId = "pump-rec-updated"
        };

        MicroBolusMapper.UpdateEntity(entity, model);

        entity.Id.Should().Be(originalId);
        entity.SysCreatedAt.Should().Be(originalCreatedAt);
        entity.Insulin.Should().Be(0.20);
        entity.Mills.Should().Be(1700000000000);
        entity.Device.Should().Be("dana-rs");
        entity.App.Should().Be("aaps");
        entity.UtcOffset.Should().Be(60);
        entity.DataSource.Should().Be("tidepool");
        entity.CorrelationId.Should().Be(newCorrelationId);
        entity.LegacyId.Should().Be("upd456");
        entity.SyncIdentifier.Should().Be("sync-updated");
        entity.PumpDeviceId.Should().Be(newPumpDeviceId);
        entity.PumpRecordId.Should().Be("pump-rec-updated");
        entity.SysUpdatedAt.Should().BeAfter(originalCreatedAt);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UpdateEntity_SetsUpdatedAtTimestamp()
    {
        var entity = new MicroBolusEntity
        {
            Id = Guid.CreateVersion7(),
            SysCreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SysUpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var beforeUpdate = DateTime.UtcNow;

        var model = new MicroBolus { Insulin = 0.1 };
        MicroBolusMapper.UpdateEntity(entity, model);

        entity.SysUpdatedAt.Should().BeOnOrAfter(beforeUpdate);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RoundTrip_PreservesAllFields()
    {
        var id = Guid.CreateVersion7();
        var correlationId = Guid.NewGuid();
        var pumpDeviceId = Guid.NewGuid();
        var original = new MicroBolus
        {
            Id = id,
            Mills = 1700000000000,
            Insulin = 0.15,
            Device = "omnipod",
            App = "aaps",
            UtcOffset = -480,
            DataSource = "nightscout",
            CorrelationId = correlationId,
            LegacyId = "rt789",
            SyncIdentifier = "sync-round-trip",
            PumpDeviceId = pumpDeviceId,
            PumpRecordId = "pump-rec-rt"
        };

        var entity = MicroBolusMapper.ToEntity(original);
        var roundTripped = MicroBolusMapper.ToDomainModel(entity);

        roundTripped.Id.Should().Be(original.Id);
        roundTripped.Mills.Should().Be(original.Mills);
        roundTripped.Insulin.Should().Be(original.Insulin);
        roundTripped.Device.Should().Be(original.Device);
        roundTripped.App.Should().Be(original.App);
        roundTripped.UtcOffset.Should().Be(original.UtcOffset);
        roundTripped.DataSource.Should().Be(original.DataSource);
        roundTripped.CorrelationId.Should().Be(original.CorrelationId);
        roundTripped.LegacyId.Should().Be(original.LegacyId);
        roundTripped.SyncIdentifier.Should().Be(original.SyncIdentifier);
        roundTripped.PumpDeviceId.Should().Be(original.PumpDeviceId);
        roundTripped.PumpRecordId.Should().Be(original.PumpRecordId);
    }
}
