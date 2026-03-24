using FluentValidation.TestHelper;
using Nocturne.API.Models.Requests.V4;
using Nocturne.API.Validators.V4;
using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.API.Tests.Validators.V4;

public class UpsertDeviceEventRequestValidatorTests
{
    private readonly UpsertDeviceEventRequestValidator _validator = new();

    private static UpsertDeviceEventRequest ValidRequest() => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        EventType = DeviceEventType.SensorStart,
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
    public void Invalid_event_type_fails()
    {
        var request = ValidRequest();
        request.EventType = (DeviceEventType)999;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.EventType);
    }

    [Fact]
    public void Notes_exceeding_max_length_fails()
    {
        var request = ValidRequest();
        request.Notes = new string('a', 10001);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Notes);
    }
}
