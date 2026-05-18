using FluentValidation;
using Nocturne.API.Controllers.V4.Monitoring;

namespace Nocturne.API.Validators.Alerts;

/// <summary>
/// Validates <see cref="SnoozeRequest"/> for the V4 alert snooze endpoint.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>Minutes must be greater than 0 and at most 1,440 (24 hours).</description></item>
/// </list>
/// </remarks>
/// <seealso cref="SnoozeRequest"/>
/// <seealso cref="AlertsController"/>
public class SnoozeRequestValidator : AbstractValidator<SnoozeRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SnoozeRequestValidator"/> class
    /// and configures all validation rules for alert snoozing.
    /// </summary>
    public SnoozeRequestValidator()
    {
        RuleFor(x => x.Minutes).GreaterThan(0).LessThanOrEqualTo(1440);
    }
}
