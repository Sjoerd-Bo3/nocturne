using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data.Entities.V4;
using Nocturne.Infrastructure.Data.Mappers.V4;

namespace Nocturne.Infrastructure.Data.Tests.Mappers.V4;

public class PumpDeviceMapperTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ToEntity_MapsAllFields()
    {
        var id = Guid.CreateVersion7();
        var model = new PumpDevice
        {
            Id = id,
            PumpType = "Omnipod DASH",
            PumpSerial = "SN-12345",
            FirstSeenMills = 1700000000000,
            LastSeenMills = 1700001000000,
        };

        var entity = PumpDeviceMapper.ToEntity(model);

        entity.Id.Should().Be(id);
        entity.PumpType.Should().Be("Omnipod DASH");
        entity.PumpSerial.Should().Be("SN-12345");
        entity.FirstSeenMills.Should().Be(1700000000000);
        entity.LastSeenMills.Should().Be(1700001000000);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ToEntity_EmptyGuid_GeneratesNewId()
    {
        var model = new PumpDevice
        {
            PumpType = "Medtronic 780G",
            PumpSerial = "ABC-999",
            FirstSeenMills = 1700000000000,
            LastSeenMills = 1700000000000,
        };

        var entity = PumpDeviceMapper.ToEntity(model);

        entity.Id.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ToEntity_DefaultStrings_MapCorrectly()
    {
        var model = new PumpDevice
        {
            FirstSeenMills = 1700000000000,
            LastSeenMills = 1700000000000,
        };

        var entity = PumpDeviceMapper.ToEntity(model);

        entity.PumpType.Should().BeEmpty();
        entity.PumpSerial.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ToDomainModel_MapsAllFields()
    {
        var id = Guid.CreateVersion7();
        var entity = new PumpDeviceEntity
        {
            Id = id,
            PumpType = "Omnipod DASH",
            PumpSerial = "SN-12345",
            FirstSeenMills = 1700000000000,
            LastSeenMills = 1700001000000,
        };

        var model = PumpDeviceMapper.ToDomainModel(entity);

        model.Id.Should().Be(id);
        model.PumpType.Should().Be("Omnipod DASH");
        model.PumpSerial.Should().Be("SN-12345");
        model.FirstSeenMills.Should().Be(1700000000000);
        model.LastSeenMills.Should().Be(1700001000000);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UpdateEntity_UpdatesAllFields()
    {
        var originalId = Guid.CreateVersion7();
        var entity = new PumpDeviceEntity
        {
            Id = originalId,
            PumpType = "OldType",
            PumpSerial = "OldSerial",
            FirstSeenMills = 1000,
            LastSeenMills = 2000,
        };

        var model = new PumpDevice
        {
            PumpType = "Omnipod DASH",
            PumpSerial = "SN-99999",
            FirstSeenMills = 1700000000000,
            LastSeenMills = 1700001000000,
        };

        PumpDeviceMapper.UpdateEntity(entity, model);

        entity.Id.Should().Be(originalId);
        entity.PumpType.Should().Be("Omnipod DASH");
        entity.PumpSerial.Should().Be("SN-99999");
        entity.FirstSeenMills.Should().Be(1700000000000);
        entity.LastSeenMills.Should().Be(1700001000000);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UpdateEntity_DoesNotChangeId()
    {
        var originalId = Guid.CreateVersion7();
        var entity = new PumpDeviceEntity
        {
            Id = originalId,
            PumpType = "OldType",
            PumpSerial = "OldSerial",
            FirstSeenMills = 1000,
            LastSeenMills = 2000,
        };

        var model = new PumpDevice
        {
            Id = Guid.CreateVersion7(),
            PumpType = "NewType",
            PumpSerial = "NewSerial",
            FirstSeenMills = 3000,
            LastSeenMills = 4000,
        };

        PumpDeviceMapper.UpdateEntity(entity, model);

        entity.Id.Should().Be(originalId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RoundTrip_PreservesAllFields()
    {
        var id = Guid.CreateVersion7();
        var original = new PumpDevice
        {
            Id = id,
            PumpType = "Medtronic 780G",
            PumpSerial = "MED-54321",
            FirstSeenMills = 1700000000000,
            LastSeenMills = 1700005000000,
        };

        var entity = PumpDeviceMapper.ToEntity(original);
        var roundTripped = PumpDeviceMapper.ToDomainModel(entity);

        roundTripped.Id.Should().Be(original.Id);
        roundTripped.PumpType.Should().Be(original.PumpType);
        roundTripped.PumpSerial.Should().Be(original.PumpSerial);
        roundTripped.FirstSeenMills.Should().Be(original.FirstSeenMills);
        roundTripped.LastSeenMills.Should().Be(original.LastSeenMills);
    }
}
