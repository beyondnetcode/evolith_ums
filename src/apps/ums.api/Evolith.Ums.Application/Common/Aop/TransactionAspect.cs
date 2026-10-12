using System.Reflection;
using Evolith.Ums.Application.Common.Interfaces;
using BeyondNetCode.Shell.Aop;

namespace Evolith.Ums.Application.Common.Aop;

public sealed class TransactionAspect : AbstractAspect<TransactionAspectAttribute>
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security Hotspot", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields",
        Justification = "ADR-UMS-099: reflexión sancionada para AOP transversal. DispatchProxy (ADR-UMS-060) es " +
                        "síncrono; resolver el wrapper genérico privado WrapAsyncOfT por reflexión es la única vía " +
                        "para preservar la async-correctness de Task<TResult>.")]
    private static readonly MethodInfo WrapAsyncOfTMethod =
        typeof(TransactionAspect).GetMethod(nameof(WrapAsyncOfT), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly IUnitOfWorkScope _unitOfWorkScope;

    public TransactionAspect(IUnitOfWorkScope unitOfWorkScope)
    {
        _unitOfWorkScope = unitOfWorkScope;
    }

    public override void Apply(IJoinPoint joinPoint)
    {
        var attribute = GetAttribute(joinPoint);
        if (attribute is null)
        {
            Proceed(joinPoint);
            return;
        }

        var tx = _unitOfWorkScope.BeginAsync().GetAwaiter().GetResult();

        try
        {
            Proceed(joinPoint);

            if (joinPoint.Return is Task returnedTask)
            {
                var returnType = joinPoint.MethodInfo.ReturnType;
                if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    var resultType = returnType.GetGenericArguments()[0];
                    joinPoint.Return = WrapAsyncOfTMethod
                        .MakeGenericMethod(resultType)
                        .Invoke(this, [joinPoint, returnedTask, tx])!;
                }
                else
                {
                    joinPoint.Return = WrapAsync(joinPoint, returnedTask, tx);
                }

                return;
            }

            tx.CommitAsync().GetAwaiter().GetResult();
        }
        catch
        {
            tx.RollbackAsync().GetAwaiter().GetResult();
            throw;
        }
        finally
        {
            if (joinPoint.Return is not Task)
            {
                tx.DisposeAsync().GetAwaiter().GetResult();
            }
        }
    }

    private void Proceed(IJoinPoint joinPoint)
    {
        if (GetNext() is null)
            joinPoint.Proceed();
        else
            GetNext()!.Apply(joinPoint);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Minor Code Smell", "S2325:Methods and properties that don't access instance data should be static",
        Justification = "Firma uniforme con WrapAsyncOfT, que se invoca por reflexión como método de " +
                        "instancia (BindingFlags.Instance). Se mantienen simétricos e instancia por diseño AOP.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell", "S1172:Unused method parameters should be removed",
        Justification = "'joinPoint' forma parte de la firma uniforme del wrapper AOP invocado por reflexión; " +
                        "se conserva para simetría con WrapAsyncOfT.")]
    private async Task WrapAsync(IJoinPoint joinPoint, Task task, ITransactionScope tx)
    {
        try
        {
            await task.ConfigureAwait(false);
            await tx.CommitAsync().ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            await tx.DisposeAsync().ConfigureAwait(false);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Minor Code Smell", "S2325:Methods and properties that don't access instance data should be static",
        Justification = "Se invoca por reflexión como método de instancia (BindingFlags.Instance, Invoke(this,...)); " +
                        "volverla estática rompería la resolución del MethodInfo en el ctor.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell", "S1172:Unused method parameters should be removed",
        Justification = "'joinPoint' se pasa en el arreglo de argumentos de la invocación por reflexión; " +
                        "forma parte de la firma uniforme del wrapper AOP.")]
    private async Task<TResult> WrapAsyncOfT<TResult>(IJoinPoint joinPoint, Task task, ITransactionScope tx)
    {
        try
        {
            var result = await ((Task<TResult>)task).ConfigureAwait(false);
            await tx.CommitAsync().ConfigureAwait(false);
            return result;
        }
        catch
        {
            await tx.RollbackAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            await tx.DisposeAsync().ConfigureAwait(false);
        }
    }
}
