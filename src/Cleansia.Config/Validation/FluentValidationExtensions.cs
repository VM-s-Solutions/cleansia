using FluentValidation;
using Cleansia.Core.AppServices;
using Cleansia.Core.AppServices.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Config.Validation;

public static class FluentValidationExtensions
{
    public static IServiceCollection AddValidators(this IServiceCollection services)
    {
        // ADR-0002 D4 (F11) + D1 + ADR-0012 D2/D2.1: registration order = outer → inner for MediatR. The
        // order MUST be AuditFailureCapture → PostCommitDispatch → Validation → UnitOfWork → AuditLog →
        // Handler.
        //   • AuditFailureCapture (ADR-0012 D2.1) is OUTERMOST: it observes the final outcome of the WHOLE
        //     inner pipeline, so it captures the two failed-admin-action shapes the inner AuditLog cannot
        //     see — a validation reject (Validation short-circuits without next(), so neither UnitOfWork
        //     nor the inner AuditLog ever runs) and a commit-throw (the inner AuditLog has already returned
        //     its success-add before the OUTER UnitOfWork.CommitAsync throws). It writes the failure row
        //     out-of-band, sharing the per-request IAuditContext latch so a handler-returned business
        //     failure the inner AuditLog already recorded is not double-written.
        //   • PostCommitDispatch: it drains IPendingDispatch and puts messages on the wire ONLY after the
        //     inner pipeline returns a committed success — never before the commit (the F2/SEC-W1 fix),
        //     never on a commit-throw (the guard is unreached).
        //   • Validation is OUTER to UnitOfWork: a failing validator returns the failure result
        //     without calling next(), so control never reaches the UoW commit on a rejected command.
        //   • AuditLog (ADR-0012 D2) is INNER to UnitOfWork: its next() (the handler) returns before
        //     the UoW commit fires, so the success-audit row added to the scoped DbContext rides that
        //     single SaveChangesAsync and is atomic with the action. Moving it outer (post-commit) makes
        //     the success-audit non-atomic — a blocking finding, caught by the pipeline-order unit test.
        // A re-swap that breaks this order is caught by the pipeline-order unit test (verify #4).
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditFailureCaptureBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PostCommitDispatchBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkPipelineBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditLogBehavior<,>));
        services.AddValidatorsFromAssembly(AssemblyReference.Assembly, includeInternalTypes: true);
        return services;
    }
}