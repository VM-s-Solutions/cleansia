using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cleansia.Tests.Behaviors;

/// <summary>
/// What key a rejected request comes back under.
///
/// The defect this pins: a customer whose account carried no phone number submitted an order and got
/// <c>400 { "NotEmptyValidator": "common.required" }</c>. FluentValidation always populates
/// <c>ErrorCode</c>, defaulting it to the type name of the rule that fired — so the response named
/// the RULE, never the field, and the wizard could neither highlight an input nor say which one.
/// Worse, <c>CreateProblemDetails</c> groups by this key: two empty fields failing the same rule
/// collapsed into ONE entry with their messages "; "-joined, so a form with three gaps reported one.
///
/// The fix keys on the property — but 58 rules set a code deliberately, and a few of those name
/// something other than a property on purpose (<c>nameof(BlobFileDto)</c> groups a file's four
/// rules; <c>"Language"</c>). Those must survive, which is what makes those two tests here the
/// point rather than padding.
///
/// The second defect: the behavior was constrained to a <c>BusinessResult</c> response, so a paged
/// read's validator was registered and never ran. It now runs for every response type; where the
/// response cannot carry the failure the reject is thrown, with the same keyed errors.
/// </summary>
public class ValidationPipelineBehaviorTests
{
    public sealed record FakeCommand(string CustomerPhone, string CustomerEmail, string Language)
        : IRequest<BusinessResult>;

    private static Error[] Reject(IValidator<FakeCommand> validator, FakeCommand request)
    {
        var behavior = new ValidationPipelineBehavior<FakeCommand, BusinessResult>(
            [validator],
            NullLogger<ValidationPipelineBehavior<FakeCommand, BusinessResult>>.Instance);

        var result = behavior.Handle(
            request,
            _ => Task.FromResult(BusinessResult.Success()),
            CancellationToken.None).GetAwaiter().GetResult();

        var validationResult = Assert.IsType<ValidationResult>(result);
        return validationResult.Errors;
    }

    private sealed class TwoEmptyFields : AbstractValidator<FakeCommand>
    {
        public TwoEmptyFields()
        {
            RuleFor(c => c.CustomerPhone).NotEmpty().WithMessage(BusinessErrorMessage.Required);
            RuleFor(c => c.CustomerEmail).NotEmpty().WithMessage(BusinessErrorMessage.Required);
        }
    }

    /// <summary>
    /// The report, exactly: two blank fields, one useless key. They are separate errors now, each
    /// naming its own field.
    /// </summary>
    [Fact]
    public void Two_Empty_Fields_Report_Two_Errors_Each_Naming_Its_Field()
    {
        var errors = Reject(new TwoEmptyFields(), new FakeCommand("", "", "cs"));

        Assert.Equal(2, errors.Length);
        Assert.Contains(errors, e => e.Code == nameof(FakeCommand.CustomerPhone));
        Assert.Contains(errors, e => e.Code == nameof(FakeCommand.CustomerEmail));
        // The VALUE is untouched — it is still the constant every client localizes from, under
        // `api.*`. Only the key moved.
        Assert.All(errors, e => Assert.Equal(BusinessErrorMessage.Required, e.Message));
        Assert.DoesNotContain(errors, e => e.Code.EndsWith("Validator", StringComparison.Ordinal));
    }

    private sealed class DeliberateCode : AbstractValidator<FakeCommand>
    {
        public DeliberateCode()
        {
            // The shape LanguageValidator and the two file validators use: a code that names a
            // GROUP rather than the property it hangs off, so several rules report as one thing.
            RuleFor(c => c.Language)
                .NotEmpty()
                .WithErrorCode("Language")
                .WithMessage(BusinessErrorMessage.Required);
        }
    }

    [Fact]
    public void A_Deliberately_Set_Error_Code_Wins_Over_The_Property_Name()
    {
        var errors = Reject(new DeliberateCode(), new FakeCommand("+420123456789", "a@b.com", ""));

        var error = Assert.Single(errors);
        Assert.Equal("Language", error.Code);
    }

    private sealed class WholeCommandRule : AbstractValidator<FakeCommand>
    {
        public WholeCommandRule() =>
            RuleFor(c => c)
                .Must(c => c.CustomerPhone.Length > 0 || c.CustomerEmail.Length > 0)
                .WithMessage(BusinessErrorMessage.Required);
    }

    /// <summary>
    /// A rule on the whole command has no property to name. It keeps FluentValidation's code rather
    /// than reporting an empty one — a key a client cannot read is still better than no key at all.
    /// </summary>
    [Fact]
    public void A_Whole_Command_Rule_Keeps_The_Default_Code()
    {
        var errors = Reject(new WholeCommandRule(), new FakeCommand("", "", "cs"));

        var error = Assert.Single(errors);
        Assert.False(string.IsNullOrEmpty(error.Code));
    }

    private sealed class NoRules : AbstractValidator<FakeCommand>;

    public sealed record FakePagedRequest(int Limit) : IRequest<PagedData<string>>;

    private sealed class LimitCap : AbstractValidator<FakePagedRequest>
    {
        public LimitCap() =>
            RuleFor(r => r.Limit)
                .LessThanOrEqualTo(100)
                .WithMessage(BusinessErrorMessage.PageSizeExceeded);
    }

    private static ValidationPipelineBehavior<FakePagedRequest, PagedData<string>> PagedBehavior(IValidator<FakePagedRequest> validator) =>
        new([validator], NullLogger<ValidationPipelineBehavior<FakePagedRequest, PagedData<string>>>.Instance);

    private static readonly PagedData<string> EmptyPage = new(1, 50, 0, []);

    [Fact]
    public async Task A_Rejected_Request_Whose_Response_Cannot_Carry_A_Failure_Throws_The_Same_Keyed_Errors()
    {
        var reached = false;

        var thrown = await Assert.ThrowsAsync<RequestValidationException>(() =>
            PagedBehavior(new LimitCap()).Handle(
                new FakePagedRequest(1000),
                _ =>
                {
                    reached = true;
                    return Task.FromResult(EmptyPage);
                },
                CancellationToken.None));

        Assert.False(reached);
        var error = Assert.Single(thrown.Errors);
        Assert.Equal(nameof(FakePagedRequest.Limit), error.Code);
        Assert.Equal(BusinessErrorMessage.PageSizeExceeded, error.Message);
    }

    [Fact]
    public async Task A_Valid_Request_Whose_Response_Cannot_Carry_A_Failure_Reaches_The_Handler()
    {
        var reached = false;

        var result = await PagedBehavior(new LimitCap()).Handle(
            new FakePagedRequest(100),
            _ =>
            {
                reached = true;
                return Task.FromResult(EmptyPage);
            },
            CancellationToken.None);

        Assert.True(reached);
        Assert.Same(EmptyPage, result);
    }

    [Fact]
    public async Task A_Valid_Request_Reaches_The_Handler()
    {
        var behavior = new ValidationPipelineBehavior<FakeCommand, BusinessResult>(
            [new NoRules()],
            NullLogger<ValidationPipelineBehavior<FakeCommand, BusinessResult>>.Instance);
        var reached = false;

        var result = await behavior.Handle(
            new FakeCommand("+420123456789", "a@b.com", "cs"),
            _ =>
            {
                reached = true;
                return Task.FromResult(BusinessResult.Success());
            },
            CancellationToken.None);

        Assert.True(reached);
        Assert.True(result.IsSuccess);
    }
}
