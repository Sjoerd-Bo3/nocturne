using FluentValidation.TestHelper;
using Nocturne.API.Models.Requests.V4;
using Nocturne.API.Validators.V4;
using Xunit;
using V4BolusType = Nocturne.Core.Models.V4.BolusType;
using V4BolusKind = Nocturne.Core.Models.V4.BolusKind;

namespace Nocturne.API.Tests.Validators.V4;

public class CreateBolusRequestValidatorTests
{
    private readonly CreateBolusRequestValidator _validator = new();

    private static CreateBolusRequest ValidRequest() => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        Insulin = 2.5,
        BolusType = V4BolusType.Normal,
        Kind = V4BolusKind.Manual,
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

    [Fact]
    public void Zero_insulin_passes()
    {
        var request = ValidRequest();
        request.Insulin = 0;
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Insulin);
    }

    [Fact]
    public void SyncIdentifier_exceeding_max_length_fails()
    {
        var request = ValidRequest();
        request.SyncIdentifier = new string('a', 501);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.SyncIdentifier);
    }
}
