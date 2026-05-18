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
    public void SyncIdentifier_with_DataSource_passes()
    {
        var request = ValidRequest();
        request.SyncIdentifier = "sync-1";
        request.DataSource = "loop";
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.DataSource);
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
