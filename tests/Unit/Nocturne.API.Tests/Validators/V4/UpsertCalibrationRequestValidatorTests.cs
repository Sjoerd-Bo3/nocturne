using FluentValidation.TestHelper;
using Nocturne.API.Models.Requests.V4;
using Nocturne.API.Validators.V4;
using Xunit;

namespace Nocturne.API.Tests.Validators.V4;

public class UpsertCalibrationRequestValidatorTests
{
    private readonly UpsertCalibrationRequestValidator _validator = new();

    private static UpsertCalibrationRequest ValidRequest() => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        Slope = 1.0,
        Intercept = 0.5,
        Scale = 1.0,
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
    public void Null_fields_pass()
    {
        var request = new UpsertCalibrationRequest
        {
            Timestamp = DateTimeOffset.UtcNow,
        };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
