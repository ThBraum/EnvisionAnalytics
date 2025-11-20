using EnvisionAnalytics.Models;
using FluentValidation;

namespace EnvisionAnalytics.Validators
{
    public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
    {
        public CreateOrderRequestValidator()
        {
            RuleFor(x => x.CustomerEmail).NotEmpty().EmailAddress();
            RuleFor(x => x.Items).NotEmpty();
            RuleForEach(x => x.Items!).SetValidator(new CreateOrderItemValidator());
        }
    }

    public class CreateOrderItemValidator : AbstractValidator<CreateOrderItem>
    {
        public CreateOrderItemValidator()
        {
            RuleFor(x => x.ProductId).NotEmpty();
            RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(100);
        }
    }
}
