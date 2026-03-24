using FluentValidation.TestHelper;
using Nocturne.API.Models.Requests.V4;
using Nocturne.API.Validators.V4;
using Xunit;

namespace Nocturne.API.Tests.Validators.V4;

public class UpsertBolusCalculationRequestValidatorTests
{
    private readonly UpsertBolusCalculationRequestValidator _validator = new();

    private static UpsertBolusCalculationRequest ValidRequest() => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        BloodGlucoseInput = 120,
        CarbInput = 30,
        CarbRatio = 10,
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
    public void Negative_carb_input_fails()
    {
        var request = ValidRequest();
        request.CarbInput = -1;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.CarbInput);
    }

    [Fact]
    public void Zero_carb_ratio_fails()
    {
        var request = ValidRequest();
        request.CarbRatio = 0;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.CarbRatio);
    }

    [Fact]
    public void Negative_insulin_recommendation_fails()
    {
        var request = ValidRequest();
        request.InsulinRecommendation = -1;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.InsulinRecommendation);
    }

    [Fact]
    public void Null_optional_fields_pass()
    {
        var request = new UpsertBolusCalculationRequest
        {
            Timestamp = DateTimeOffset.UtcNow,
        };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
