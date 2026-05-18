using FluentValidation.TestHelper;
using Nocturne.API.Models.Requests.V4;
using Nocturne.API.Validators.V4;
using Xunit;

namespace Nocturne.API.Tests.Validators.V4;

public class UpdateCarbIntakeRequestValidatorTests
{
    private readonly UpdateCarbIntakeRequestValidator _validator = new();

    private static UpdateCarbIntakeRequest ValidRequest() => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        Carbs = 40,
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
    public void Negative_carbs_fails()
    {
        var request = ValidRequest();
        request.Carbs = -1;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Carbs);
    }

    [Fact]
    public void SyncIdentifier_without_DataSource_fails()
    {
        var request = ValidRequest();
        request.SyncIdentifier = "sync-1";
        request.DataSource = null;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.DataSource);
    }

    [Fact]
    public void Empty_CorrelationId_fails()
    {
        var request = ValidRequest();
        request.CorrelationId = Guid.Empty;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.CorrelationId);
    }

    [Fact]
    public void Null_CorrelationId_passes()
    {
        var request = ValidRequest();
        request.CorrelationId = null;
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.CorrelationId);
    }
}
