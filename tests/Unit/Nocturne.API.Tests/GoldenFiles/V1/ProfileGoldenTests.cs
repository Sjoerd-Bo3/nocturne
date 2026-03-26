using Nocturne.API.Tests.GoldenFiles.Infrastructure;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Tests.GoldenFiles.V1;

public class ProfileGoldenTests : GoldenFileTestBase
{
    private static readonly Guid TestTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    // Fixed timestamps for deterministic tests
    // 2024-03-26T12:00:00Z = 1711454400000
    private const long BaseMillis = 1711454400000;

    public ProfileGoldenTests(GoldenFileWebAppFactory factory) : base(factory) { }

    #region Helper Methods

    private static ProfileEntity CreateProfile(
        int index,
        string defaultProfile = "Default",
        string units = "mg/dl",
        string? storeJson = null)
    {
        var mills = BaseMillis - (index * 3600_000); // 1 hour apart, descending
        return new ProfileEntity
        {
            Id = Guid.Parse($"00000000-0000-0000-0003-{(index + 1):D12}"),
            TenantId = TestTenantId,
            OriginalId = $"ddddddddddddddddddddd{(index + 1):D3}",
            DefaultProfile = defaultProfile,
            StartDate = DateTimeOffset.FromUnixTimeMilliseconds(mills).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            Mills = mills,
            CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(mills).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            Units = units,
            StoreJson = storeJson ?? """{"Default":{"dia":3,"carbratio":[{"time":"00:00","value":10,"timeAsSeconds":0}],"sens":[{"time":"00:00","value":50,"timeAsSeconds":0}],"basal":[{"time":"00:00","value":0.8,"timeAsSeconds":0}],"target_low":[{"time":"00:00","value":80,"timeAsSeconds":0}],"target_high":[{"time":"00:00","value":120,"timeAsSeconds":0}],"timezone":"UTC"}}""",
            CreatedAtPg = new DateTime(2024, 3, 26, 12, 0, 0, DateTimeKind.Utc),
            UpdatedAtPg = new DateTime(2024, 3, 26, 12, 0, 0, DateTimeKind.Utc),
        };
    }

    #endregion

    #region GET /api/v1/profile

    [Fact]
    public async Task GetProfiles_WithEmptyDb_ReturnsEmptyArray()
    {
        var response = await Client.GetAsync("/api/v1/profile");
        var captured = await CaptureResponse(response);

        await Verify(captured);
    }

    [Fact]
    public async Task GetProfiles_WithSeededData_ReturnsArray()
    {
        var profiles = Enumerable.Range(0, 3).Select(i => CreateProfile(i)).ToArray();
        await SeedProfiles(profiles);

        var response = await Client.GetAsync("/api/v1/profile");
        var captured = await CaptureResponse(response);

        await Verify(captured);
    }

    #endregion

    #region GET /api/v1/profile/current

    [Fact]
    public async Task GetCurrentProfile_WithNoProfiles_ReturnsEmptyArray()
    {
        var response = await Client.GetAsync("/api/v1/profile/current");
        var captured = await CaptureResponse(response);

        await Verify(captured);
    }

    [Fact]
    public async Task GetCurrentProfile_WithSeededData_ReturnsSingleObject()
    {
        await SeedProfiles(CreateProfile(0));

        var response = await Client.GetAsync("/api/v1/profile/current");
        var captured = await CaptureResponse(response);

        await Verify(captured);
    }

    #endregion
}
