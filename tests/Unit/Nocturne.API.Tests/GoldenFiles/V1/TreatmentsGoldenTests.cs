using Nocturne.API.Tests.GoldenFiles.Infrastructure;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Tests.GoldenFiles.V1;

public class TreatmentsGoldenTests : GoldenFileTestBase
{
    private static readonly Guid TestTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    // Use a timestamp close to "now" to avoid being filtered by the default 4-day date range.
    // The helper computes BaseMillis at class load time.
    private static readonly long BaseMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public TreatmentsGoldenTests(GoldenFileWebAppFactory factory) : base(factory) { }

    #region Helper Methods

    private static TreatmentEntity CreateTreatment(
        int index,
        string eventType = "Meal Bolus",
        double? carbs = 45,
        double? insulin = 3.5,
        string? notes = null,
        string? enteredBy = null)
    {
        var mills = BaseMillis - (index * 300_000); // 5 min apart, descending
        return new TreatmentEntity
        {
            Id = Guid.Parse($"00000000-0000-0000-0000-{(index + 1):D12}"),
            TenantId = TestTenantId,
            OriginalId = $"ccccccccccccccccccccc{(index + 1):D3}",
            EventType = eventType,
            Carbs = carbs,
            Insulin = insulin,
            Notes = notes,
            EnteredBy = enteredBy,
            Mills = mills,
            Created_at = DateTimeOffset.FromUnixTimeMilliseconds(mills).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            Date = mills,
            SysCreatedAt = DateTime.UtcNow,
            SysUpdatedAt = DateTime.UtcNow,
        };
    }

    #endregion

    #region GET /api/v1/treatments (empty)

    [Fact]
    public async Task GetTreatments_WithEmptyDb_ReturnsEmptyArray()
    {
        var response = await Client.GetAsync("/api/v1/treatments");
        var captured = await CaptureResponse(response);

        await Verify(captured);
    }

    #endregion

    #region GET /api/v1/treatments (with data)

    [Fact]
    public async Task GetTreatments_WithSeededData_ReturnsArrayResponse()
    {
        var treatments = new[]
        {
            CreateTreatment(0, eventType: "Meal Bolus", carbs: 45, insulin: 3.5, notes: "Lunch", enteredBy: "xDrip"),
            CreateTreatment(1, eventType: "Correction Bolus", carbs: null, insulin: 1.0, notes: null, enteredBy: "Loop"),
            CreateTreatment(2, eventType: "Carb Correction", carbs: 15, insulin: null, notes: "Juice box", enteredBy: "xDrip"),
        };
        await SeedTreatments(treatments);

        var response = await Client.GetAsync("/api/v1/treatments");
        var captured = await CaptureResponse(response);

        await Verify(captured)
            .ScrubMembers("mills", "date", "created_at", "srvModified", "srvCreated");
    }

    #endregion

    #region GET /api/v1/treatments response fields

    [Fact]
    public async Task GetTreatments_ResponseFields_ContainsExpectedFields()
    {
        await SeedTreatments(CreateTreatment(0,
            eventType: "Meal Bolus",
            carbs: 60,
            insulin: 5.0,
            notes: "Dinner",
            enteredBy: "careportal"));

        var response = await Client.GetAsync("/api/v1/treatments");
        var captured = await CaptureResponse(response);

        await Verify(captured)
            .ScrubMembers("mills", "date", "created_at", "srvModified", "srvCreated");
    }

    #endregion

    #region POST /api/v1/treatments

    [Fact]
    public async Task PostTreatments_SingleTreatment_ReturnsCreatedResponseShape()
    {
        var payload = new
        {
            eventType = "Meal Bolus",
            created_at = "2024-03-26T12:00:00.000Z",
            carbs = 30,
            insulin = 2.5,
            notes = "Snack",
            enteredBy = "xDrip",
        };

        var response = await PostJsonAsync("/api/v1/treatments", payload);
        var captured = await CaptureResponse(response);

        await Verify(captured)
            .ScrubMembers("_id", "identifier", "mills", "date", "srvModified", "srvCreated", "created_at");
    }

    #endregion

    #region GET /api/v1/treatments sorted by created_at descending

    [Fact]
    public async Task GetTreatments_SortedByCreatedAtDescending_NewestFirst()
    {
        // Seed treatments with different timestamps; index 0 = newest, index 4 = oldest
        var treatments = Enumerable.Range(0, 5).Select(i => CreateTreatment(i,
            eventType: "Note",
            carbs: null,
            insulin: null,
            notes: $"Note {i}",
            enteredBy: "test")).ToArray();
        await SeedTreatments(treatments);

        var response = await Client.GetAsync("/api/v1/treatments");
        var captured = await CaptureResponse(response);

        await Verify(captured)
            .ScrubMembers("mills", "date", "created_at", "srvModified", "srvCreated");
    }

    #endregion
}
