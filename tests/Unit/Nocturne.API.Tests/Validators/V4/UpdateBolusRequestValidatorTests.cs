using FluentValidation.TestHelper;
using Nocturne.API.Models.Requests.V4;
using Nocturne.API.Validators.V4;
using Xunit;

namespace Nocturne.API.Tests.Validators.V4;

public class UpdateBolusRequestValidatorTests
{
    private readonly UpdateBolusRequestValidator _validator = new();

    private static UpdateBolusRequest ValidRequest() => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        Insulin = 2.5,
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
    public void Negative_insulin_fails()
    {
        var request = ValidRequest();
        request.Insulin = -1;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Insulin);
    }

    [Fact]
    public void Negative_duration_fails()
    {
        var request = ValidRequest();
        request.Duration = -1;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Duration);
    }
}
