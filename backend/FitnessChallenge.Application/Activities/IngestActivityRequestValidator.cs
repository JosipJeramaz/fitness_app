using FitnessChallenge.Application.Dtos.Activities;
using FluentValidation;

namespace FitnessChallenge.Application.Activities;

/// <summary>
/// FluentValidation entry point for activity ingestion. Structural/userId checks live here; the metric
/// matrix is delegated to <see cref="ActivityInterpreter"/> so validation and resolution share one rule set.
/// User existence is checked later in the service (needs the database).
/// </summary>
public sealed class IngestActivityRequestValidator : AbstractValidator<IngestActivityRequest>
{
    public IngestActivityRequestValidator(TimeProvider clock)
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("'userId' is required.")
            .Must(id => Guid.TryParse(id, out _)).WithMessage("'userId' must be a valid identifier.")
            .OverridePropertyName("userId");

        RuleFor(x => x).Custom((request, context) =>
        {
            var interpretation = ActivityInterpreter.Interpret(request, clock.GetUtcNow());
            if (interpretation.IsValid)
                return;

            foreach (var error in interpretation.Errors)
                context.AddFailure(error.Field, error.Message);
        });
    }
}
