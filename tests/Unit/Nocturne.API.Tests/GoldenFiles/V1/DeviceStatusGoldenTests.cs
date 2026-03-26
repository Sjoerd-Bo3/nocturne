using Nocturne.API.Tests.GoldenFiles.Infrastructure;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Tests.GoldenFiles.V1;

public class DeviceStatusGoldenTests : GoldenFileTestBase
{
    private static readonly Guid TestTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    // Fixed timestamps for deterministic tests
    // 2024-03-26T12:00:00Z = 1711454400000
    private const long BaseMillis = 1711454400000;

    public DeviceStatusGoldenTests(GoldenFileWebAppFactory factory) : base(factory) { }

    #region Helper Methods

    private static DeviceStatusEntity CreateDeviceStatus(
        int index,
        string device = "openaps://Samsung SM-G973F",
        string? uploaderJson = null,
        string? pumpJson = null,
        string? openApsJson = null)
    {
        var mills = BaseMillis - (index * 300_000); // 5 min apart, descending
        return new DeviceStatusEntity
        {
            Id = Guid.Parse($"00000000-0000-0000-0002-{(index + 1):D12}"),
            TenantId = TestTenantId,
            OriginalId = $"ccccccccccccccccccccc{(index + 1):D3}",
            Mills = mills,
            CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(mills).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            Device = device,
            UploaderJson = uploaderJson,
            PumpJson = pumpJson,
            OpenApsJson = openApsJson,
            SysCreatedAt = new DateTime(2024, 3, 26, 12, 0, 0, DateTimeKind.Utc),
            SysUpdatedAt = new DateTime(2024, 3, 26, 12, 0, 0, DateTimeKind.Utc),
        };
    }

    #endregion

    #region GET /api/v1/devicestatus

    [Fact]
    public async Task GetDeviceStatus_WithEmptyDb_ReturnsEmptyArray()
    {
        var response = await Client.GetAsync("/api/v1/devicestatus");
        var captured = await CaptureResponse(response);

        await Verify(captured);
    }

    [Fact]
    public async Task GetDeviceStatus_WithSeededData_ReturnsArrayDefaultCount10()
    {
        var statuses = Enumerable.Range(0, 15).Select(i => CreateDeviceStatus(i)).ToArray();
        await SeedDeviceStatuses(statuses);

        var response = await Client.GetAsync("/api/v1/devicestatus");
        var captured = await CaptureResponse(response);

        await Verify(captured);
    }

    [Fact]
    public async Task GetDeviceStatus_WithNestedData_IncludesDeviceAndNestedFields()
    {
        var status = CreateDeviceStatus(
            0,
            device: "openaps://Samsung SM-G973F",
            uploaderJson: """{"battery":85}""",
            pumpJson: """{"clock":"2024-03-26T12:00:00Z","battery":{"status":"normal","voltage":1.52},"reservoir":120.5}""",
            openApsJson: """{"suggested":{"bg":120,"tick":-3,"eventualBG":110},"enacted":{"bg":120,"reason":"COB: 0"}}""");

        await SeedDeviceStatuses(status);

        var response = await Client.GetAsync("/api/v1/devicestatus");
        var captured = await CaptureResponse(response);

        await Verify(captured);
    }

    #endregion
}
