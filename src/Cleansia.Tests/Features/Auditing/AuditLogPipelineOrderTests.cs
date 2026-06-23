using Cleansia.Config.Validation;
using Cleansia.Core.AppServices.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// ADR-0012 AC1 / reviewer check #1 — the pipeline-order gate. The IPipelineBehavior&lt;,&gt; open-generic
/// registration order IS the outer→inner execution order MediatR resolves at runtime. AuditLogBehavior
/// MUST be registered AFTER (inner to) UnitOfWorkPipelineBehavior so its next() (the handler) returns
/// before the UoW commit fires and the success-audit row rides the same SaveChangesAsync (atomic).
/// Moving it outer (post-commit) makes the success-audit non-atomic — a blocking finding this test
/// catches. The full chain is PostCommitDispatch → Validation → UnitOfWork → AuditLog → Handler.
/// </summary>
public sealed class AuditLogPipelineOrderTests
{
    private static List<Type?> RegisteredBehaviorTypes() =>
        new ServiceCollection().AddValidators()
            .Where(d => d.ServiceType == typeof(IPipelineBehavior<,>))
            .Select(d => d.ImplementationType)
            .ToList();

    [Fact]
    public void AuditLogBehavior_Is_Registered_Inner_To_UnitOfWorkPipelineBehavior()
    {
        var behaviorTypes = RegisteredBehaviorTypes();

        var unitOfWorkIndex = behaviorTypes.IndexOf(typeof(UnitOfWorkPipelineBehavior<,>));
        var auditIndex = behaviorTypes.IndexOf(typeof(AuditLogBehavior<,>));

        Assert.True(unitOfWorkIndex >= 0, "UnitOfWorkPipelineBehavior must be registered.");
        Assert.True(auditIndex >= 0, "AuditLogBehavior must be registered.");
        Assert.True(
            auditIndex > unitOfWorkIndex,
            "ADR-0012 D2: AuditLogBehavior must be registered AFTER UnitOfWorkPipelineBehavior so it is " +
            "INNER (its handler returns before the UoW commit, so the success-audit row rides the same " +
            "SaveChangesAsync — atomic). Moving it outer makes the success-audit non-atomic.");
    }

    [Fact]
    public void The_Full_OuterToInner_Order_Is_AuditFailureCapture_PostCommit_Validation_UnitOfWork_AuditLog()
    {
        var behaviorTypes = RegisteredBehaviorTypes();

        var failureCapture = behaviorTypes.IndexOf(typeof(AuditFailureCaptureBehavior<,>));
        var postCommit = behaviorTypes.IndexOf(typeof(PostCommitDispatchBehavior<,>));
        var validation = behaviorTypes.IndexOf(typeof(ValidationPipelineBehavior<,>));
        var unitOfWork = behaviorTypes.IndexOf(typeof(UnitOfWorkPipelineBehavior<,>));
        var audit = behaviorTypes.IndexOf(typeof(AuditLogBehavior<,>));

        Assert.True(failureCapture >= 0, "AuditFailureCaptureBehavior must be registered.");
        Assert.True(failureCapture < postCommit, "AuditFailureCapture must be outermost.");
        Assert.True(postCommit < validation, "PostCommitDispatch must be outer to Validation.");
        Assert.True(validation < unitOfWork, "Validation must be outer to UnitOfWork.");
        Assert.True(unitOfWork < audit, "UnitOfWork must be outer to AuditLog.");
    }

    [Fact]
    public void AuditFailureCaptureBehavior_Is_Registered_Outer_To_Validation_So_It_Sees_A_Validation_Reject()
    {
        var behaviorTypes = RegisteredBehaviorTypes();

        var failureCapture = behaviorTypes.IndexOf(typeof(AuditFailureCaptureBehavior<,>));
        var validation = behaviorTypes.IndexOf(typeof(ValidationPipelineBehavior<,>));

        Assert.True(failureCapture >= 0, "AuditFailureCaptureBehavior must be registered.");
        Assert.True(
            failureCapture < validation,
            "ADR-0012 D2.1: AuditFailureCaptureBehavior must be OUTER to ValidationPipelineBehavior so a " +
            "validation reject (which short-circuits without next()) is still observed and recorded " +
            "out-of-band — otherwise a validator-rejected admin command leaves no trail.");
    }
}
