using System.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.TestUtilities;

public class BaseTransactionalPostgresSqlTest<TDbContext> where TDbContext : DbContext
{
    protected virtual async Task TestMethod<TResult>(
        Action<IServiceCollection>? setup = null,
        Action<TDbContext>? arrange = null,
        Func<IServiceProvider, Task<TResult>>? act = null,
        Action<TDbContext, TResult>? assert = null,
        Func<TDbContext, Task>? cleanup = null,
        bool transactional = true)
    {
        await TestMethod(
            setup: setup,
            arrange: arrange,
            act: act,
            assert: (context, result) =>
            {
                assert?.Invoke(context, result);
                return Task.CompletedTask;
            },
            cleanup,
            transactional);
    }

    protected virtual async Task TestMethod<TResult>(
        Action<IServiceCollection>? setup = null,
        Action<TDbContext>? arrange = null,
        Func<IServiceProvider, Task<TResult>>? act = null,
        Func<TDbContext, TResult, Task>? assert = null,
        Func<TDbContext, Task>? cleanup = null,
        bool transactional = true)
    {
        await TestMethod(
            setup: setup,
            arrange: context =>
            {
                arrange?.Invoke(context);
                return Task.CompletedTask;
            },
            act: act,
            assert: assert,
            cleanup,
            transactional);
    }

    protected virtual async Task TestMethod<TResult>(
        Action<IServiceCollection>? setup = null,
        Func<TDbContext, Task>? arrange = null,
        Func<IServiceProvider, Task<TResult>>? act = null,
        Func<TDbContext, TResult, Task>? assert = null,
        Func<TDbContext, Task>? cleanup = null,
        bool transactional = true)
    {
        await TestMethod(
            setup: services =>
            {
                setup?.Invoke(services);
                return Task.CompletedTask;
            },
            arrange: arrange,
            act: act,
            assert: assert,
            cleanup,
            transactional);
    }

    protected virtual async Task TestMethod<TResult>(
        Action<IServiceCollection>? setup = null,
        Func<TDbContext, Task>? arrange = null,
        Func<IServiceProvider, TResult>? act = null,
        Func<TDbContext, TResult, Task>? assert = null,
        Func<TDbContext, Task>? cleanup = null,
        bool transactional = true)
    {
        await TestMethod(
            setup: services =>
            {
                setup?.Invoke(services);
                return Task.CompletedTask;
            },
            arrange: arrange,
            act: provider =>
            {
                TResult? result = default;
                if (act != null)
                {
                    result = act(provider);
                }

                return Task.FromResult(result)!;
            },
            assert: assert,
            cleanup,
            transactional);
    }

    protected virtual async Task TestMethod<TResult>(
        Action<IServiceCollection>? setup = null,
        Action<TDbContext>? arrange = null,
        Func<IServiceProvider, TResult>? act = null,
        Action<TDbContext, TResult?>? assert = null,
        Func<TDbContext, Task>? cleanup = null,
        bool transactional = true)
    {
        await TestMethod(
            setup: setup,
            arrange: async context =>
            {
                arrange?.Invoke(context);
                await Task.CompletedTask;
            },
            act: provider =>
            {
                TResult? result = default;
                if (act != null)
                {
                    result = act(provider);
                }

                return Task.FromResult(result);
            },
            assert: (context, result) =>
            {
                assert?.Invoke(context, result);
                return Task.CompletedTask;
            },
            cleanup,
            transactional);
    }

    protected virtual async Task TestMethod<TResult>(
        Func<IServiceCollection, Task>? setup = null,
        Func<TDbContext, Task>? arrange = null,
        Func<IServiceProvider, Task<TResult>>? act = null,
        Func<TDbContext, TResult, Task>? assert = null,
        Func<TDbContext, Task>? cleanup = null,
        bool transactional = true)
    {
        Func<IServiceProvider, Task>? arr = null;
        if (arrange != null)
        {
            arr = async provider =>
            {
                var dbContext = provider.GetService<TDbContext>()!;

                await arrange(dbContext);
                await dbContext.SaveChangesAsync();
            };
        }

        await TestMethod(setup, arr, act, assert, cleanup, transactional);
    }

    protected virtual async Task TestMethod<TResult>(
        Func<IServiceCollection, Task>? setup = null,
        Func<IServiceProvider, Task>? arrange = null,
        Func<IServiceProvider, Task<TResult>>? act = null,
        Func<TDbContext, TResult, Task>? assert = null,
        Func<TDbContext, Task>? cleanup = null,
        bool transactional = true)
    {
        if (typeof(Task).IsAssignableFrom(typeof(TResult)))
        {
            throw new Exception("""
                                'TResult' cannot be of type 'Task'.
                                                                   If you see this warning, your unit test is probably wrong.
                                                                   The compiler can't determine the type of the result you return in the 'act'.
                                                                   To solve this issue, make sure you return something in the 'act' and that you assert on existing property of 'TResult'.
                                """);
        }

        TResult? result = default;

        var services = new ServiceCollection();

        if (setup is not null)
        {
            await setup(services);
        }

        var provider = services.BuildServiceProvider();
        TransactionScope? transactionScope = null;

        try
        {
            if (transactional)
            {
                transactionScope = new TransactionScope(TransactionScopeOption.Required,
                    new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
                    TransactionScopeAsyncFlowOption.Enabled);
            }

            if (arrange is not null)
            {
                using var scope = provider.CreateScope();
                await arrange(scope.ServiceProvider);
            }

            if (act is not null)
            {
                using var scope = provider.CreateScope();
                result = await act(scope.ServiceProvider);
            }

            if (assert is not null)
            {
                using var scope = provider.CreateScope();
                var dbContext = scope.ServiceProvider.GetService<TDbContext>()!;
                await assert(dbContext, result!);
            }
        }
        finally
        {
            if (cleanup is not null)
            {
                using var scope = provider.CreateScope();
                var dbContext = scope.ServiceProvider.GetService<TDbContext>()!;
                await cleanup(dbContext);
            }

            transactionScope?.Dispose();

        }
    }
}