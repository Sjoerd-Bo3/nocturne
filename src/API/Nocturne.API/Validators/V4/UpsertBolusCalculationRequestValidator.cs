using FluentValidation;
using Nocturne.API.Models.Requests.V4;

namespace Nocturne.API.Validators.V4;

public class UpsertBolusCalculationRequestValidator : AbstractValidator<UpsertBolusCalculationRequest>
{
    public UpsertBolusCalculationRequestValidator()
    {
        RuleFor(x => x.Timestamp).NotEqual(default(DateTimeOffset)).WithMessage("Timestamp is required");
        RuleFor(x => x.Device).MaximumLength(500).When(x => x.Device is not null);
        RuleFor(x => x.App).MaximumLength(500).When(x => x.App is not null);
        RuleFor(x => x.DataSource).MaximumLength(500).When(x => x.DataSource is not null);
        RuleFor(x => x.CarbInput).GreaterThanOrEqualTo(0).When(x => x.CarbInput is not null)
            .WithMessage("CarbInput must be >= 0");
        RuleFor(x => x.CarbRatio).GreaterThan(0).When(x => x.CarbRatio is not null)
            .WithMessage("CarbRatio must be > 0");
        RuleFor(x => x.InsulinRecommendation).GreaterThanOrEqualTo(0).When(x => x.InsulinRecommendation is not null)
            .WithMessage("InsulinRecommendation must be >= 0");
        RuleFor(x => x.CalculationType).IsInEnum().When(x => x.CalculationType is not null);
        RuleFor(x => x.BloodGlucoseInputSource).MaximumLength(200).When(x => x.BloodGlucoseInputSource is not null);
    }
}
