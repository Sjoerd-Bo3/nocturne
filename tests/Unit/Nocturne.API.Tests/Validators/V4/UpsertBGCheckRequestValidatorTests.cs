using FluentValidation.TestHelper;
using Nocturne.API.Models.Requests.V4;
using Nocturne.API.Validators.V4;
using Xunit;

namespace Nocturne.API.Tests.Validators.V4;

public class UpsertBGCheckRequestValidatorTests
{
    private readonly UpsertBGCheckRequestValidator _validator = new();

    private static UpsertBGCheckRequest ValidRequest() => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        Glucose = 120,
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
    public void Glucose_negative_fails()
    {
        var request = ValidRequest();
        request.Glucose = -1;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Glucose);
    }

    [Fact]
    public void Glucose_above_10000_fails()
    {
        var request = ValidRequest();
        request.Glucose = 10001;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Glucose);
    }

    [Fact]
    public void Glucose_at_boundary_passes()
    {
        var request = ValidRequest();
        request.Glucose = 10000;
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Glucose);
    }
}
