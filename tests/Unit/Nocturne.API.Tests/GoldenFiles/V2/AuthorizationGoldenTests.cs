using Nocturne.API.Tests.GoldenFiles.Infrastructure;

namespace Nocturne.API.Tests.GoldenFiles.V2;

public class AuthorizationGoldenTests : GoldenFileTestBase
{
    public AuthorizationGoldenTests(GoldenFileWebAppFactory factory) : base(factory) { }

    #region GET /api/v2/authorization/permissions

    [Fact]
    public async Task GetPermissions_ReturnsPermissionsResponseShape()
    {
        var response = await Client.GetAsync("/api/v2/authorization/permissions");
        var captured = await CaptureResponse(response);

        await Verify(captured)
            .ScrubMembers("firstSeen", "lastSeen");
    }

    #endregion

    #region GET /api/v2/authorization/subjects

    [Fact]
    public async Task GetSubjects_ReturnsSubjectsListResponseShape()
    {
        var response = await Client.GetAsync("/api/v2/authorization/subjects");
        var captured = await CaptureResponse(response);

        await Verify(captured)
            .ScrubMembers("created", "modified");
    }

    #endregion
}
