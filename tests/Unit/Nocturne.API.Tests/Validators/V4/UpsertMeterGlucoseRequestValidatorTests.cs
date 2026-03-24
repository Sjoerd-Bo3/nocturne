using FluentValidation.TestHelper;
using Nocturne.API.Models.Requests.V4;
using Nocturne.API.Validators.V4;
using Xunit;

namespace Nocturne.API.Tests.Validators.V4;

public class UpsertMeterGlucoseRequestValidatorTests
{
    private readonly UpsertMeterGlucoseRequestValidator _validator = new();

    private static UpsertMeterGlucoseRequest ValidRequest() => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        Mgdl = 120,
    };

    [Fact]
    public void Valid_request_passes()
    {
        var result = _validator.TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Default_timestamp_fails()
    {
        var request = ValidRequest();
        request.Timestamp = default;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Timestamp);
    }

    [Fact]
    public void Mgdl_negative_fails()
    {
        var request = ValidRequest();
        request.Mgdl = -1;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Mgdl);
    }

    [Fact]
    public void Mgdl_above_10000_fails()
    {
        var request = ValidRequest();
        request.Mgdl = 10001;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Mgdl);
    }
}
