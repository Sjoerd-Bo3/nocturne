using Nocturne.API.Tests.GoldenFiles.Infrastructure;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Tests.GoldenFiles.V3;

public class LastModifiedGoldenTests : GoldenFileTestBase
{
    private static readonly Guid TestTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public LastModifiedGoldenTests(GoldenFileWebAppFactory factory) : base(factory) { }

    #region GET /api/v3/lastModified

    [Fact]
    public async Task GetLastModified_WithEmptyDb_ReturnsNullCollectionTimestamps()
    {
        var response = await Client.GetAsync("/api/v3/lastModified");
        var captured = await CaptureResponse(response);

        await Verify(captured)
            .ScrubMembers("serverTime");
    }

    [Fact]
    public async Task GetLastModified_WithSeededEntries_ReturnsEntryTimestamp()
    {
        await SeedEntries(new EntryEntity
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            TenantId = TestTenantId,
            OriginalId = "aaaaaaaaaaaaaaaaaaaaa001",
            Type = "sgv",
            Sgv = 120,
            Mgdl = 120,
            Mills = 1711454400000,
            DateString = "2024-03-26T12:00:00.000Z",
            Device = "xDrip-DexcomG6",
            SysCreatedAt = new DateTime(2024, 3, 26, 12, 0, 0, DateTimeKind.Utc),
            SysUpdatedAt = new DateTime(2024, 3, 26, 12, 0, 0, DateTimeKind.Utc),
        });

        var response = await Client.GetAsync("/api/v3/lastModified");
        var captured = await CaptureResponse(response);

        await Verify(captured)
            .ScrubMembers("serverTime");
    }

    #endregion
}
