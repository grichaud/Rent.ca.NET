using FluentValidation;

namespace Rent.Web.Features.Inquiries.Validators;

public class InquiryValidator : AbstractValidator<InquiryRequest>
{
    public InquiryValidator()
    {
        RuleFor(x => x.PropertyId).NotEmpty();
        RuleFor(x => x.SenderName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SenderEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.SenderPhone).MaximumLength(30);
        RuleFor(x => x.Message).NotEmpty().MinimumLength(10).MaximumLength(4000);
        RuleFor(x => x.MoveInDate)
            .Must(d => d is null || d >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)))
            .WithMessage("Move-in date must be today or later.");
    }
}
