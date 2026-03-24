using FluentValidation;
using Nocturne.API.Controllers.V4;

namespace Nocturne.API.Validators.Alerts;

public class SnoozeRequestValidator : AbstractValidator<SnoozeRequest>
{
    public SnoozeRequestValidator()
    {
        RuleFor(x => x.Minutes).GreaterThan(0).LessThanOrEqualTo(1440);
    }
}
