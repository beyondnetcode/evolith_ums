using System.Reflection;
using BeyondNetCode.Shell.Aop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Evolith.Ums.Sdk.Authorization;
using Evolith.Ums.Sdk.Contracts;

namespace Evolith.Ums.Sdk.Authorization.Aop;

/// <summary>
/// Single aspect implementing all four authorization primitives. Activated by Shell.Aop's
/// attribute-driven PointCut on any method carrying a <see cref="RequiresAuthorizationAttribute"/>
/// subclass — <see cref="RequiresScopeAttribute"/>, <see cref="RequiresMenuOptionAttribute"/>,
/// <see cref="RequiresDomainAccessAttribute"/> or <see cref="RequiresFeatureFlagAttribute"/>.
/// </summary>
public sealed class AuthorizationAspect : AbstractAspect<RequiresAuthorizationAttribute>
{
    private readonly IAuthorizationValidator _validator;
    private readonly IAuthGraphAccessor _accessor;
    private readonly IOptionsMonitor<AuthorizationOptions> _options;
    private readonly ILogger<AuthorizationAspect>? _logger;

    public AuthorizationAspect(
        IAuthorizationValidator validator,
        IAuthGraphAccessor accessor,
        IOptionsMonitor<AuthorizationOptions> options,
        ILogger<AuthorizationAspect>? logger = null)
    {
        _validator = validator;
        _accessor = accessor;
        _options = options;
        _logger = logger;
    }

    public override void Apply(IJoinPoint joinPoint)
    {
        var attribute = GetAttribute(joinPoint);
        if (attribute is null)
        {
            Proceed(joinPoint);
            return;
        }

        var graph = _accessor.Current;
        var decision = Evaluate(attribute, graph);
        if (decision.IsGranted)
        {
            Proceed(joinPoint);
            return;
        }

        var globalMode = _options.CurrentValue.Mode;
        var effectiveAuditOnly = attribute.AuditOnly || globalMode == AuthorizationMode.AuditOnly;
        LogDenial(decision, effectiveAuditOnly);

        if (effectiveAuditOnly)
        {
            Proceed(joinPoint);
            return;
        }

        if (attribute.OnDenied == DenialBehavior.ReturnFailure)
        {
            joinPoint.Return = BuildFailureReturn(joinPoint, decision);
            return;
        }

        throw new AuthorizationDeniedException(decision);
    }

    private void Proceed(IJoinPoint joinPoint)
    {
        if (GetNext() is null)
            joinPoint.Proceed();
        else
            GetNext()!.Apply(joinPoint);
    }

    private AuthorizationDecision Evaluate(RequiresAuthorizationAttribute attribute, AuthorizationGraph? graph) =>
        attribute switch
        {
            RequiresScopeAttribute s        => _validator.RequireScope(graph, s.Scope),
            RequiresMenuOptionAttribute m   => _validator.RequireMenuOption(graph, m.OptionCode),
            RequiresDomainAccessAttribute d => _validator.RequireDomainAccess(graph, d.ResourceCode, d.ActionCode),
            RequiresFeatureFlagAttribute f  => _validator.RequireFeatureFlag(graph, f.FlagCode),
            _ => throw new InvalidOperationException(
                $"Unknown UMS authorization attribute: {attribute.GetType().FullName}")
        };

    private void LogDenial(AuthorizationDecision decision, bool auditOnly)
    {
        if (_logger is null) return;
        var eventName = auditOnly ? "AuthorizationDeniedEvent (audit-only)" : "AuthorizationDeniedEvent";
        _logger.LogWarning(
            "{Event}: primitive={Primitive} target={Target} code={Code} reason={Reason}",
            eventName, decision.Primitive, decision.Target, decision.ErrorCode, decision.Reason);
    }

    private static object? BuildFailureReturn(IJoinPoint joinPoint, AuthorizationDecision decision)
    {
        var returnType = joinPoint.MethodInfo.ReturnType;
        var code = decision.ErrorCode ?? "AUTH_UNKNOWN";
        var msg  = decision.Reason ?? "Denied.";

        if (returnType == typeof(Result))
            return Result.Failure(code, msg);

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var inner = returnType.GenericTypeArguments[0];
            return typeof(Result<>).MakeGenericType(inner)
                .GetMethod(nameof(Result<int>.Failure), BindingFlags.Public | BindingFlags.Static)!
                .Invoke(null, new object[] { code, msg });
        }

        if (returnType == typeof(Task<Result>))
            return Task.FromResult(Result.Failure(code, msg));

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var taskArg = returnType.GenericTypeArguments[0];
            if (taskArg.IsGenericType && taskArg.GetGenericTypeDefinition() == typeof(Result<>))
            {
                var inner = taskArg.GenericTypeArguments[0];
                var failureMethod = typeof(Result<>).MakeGenericType(inner)
                    .GetMethod(nameof(Result<int>.Failure), BindingFlags.Public | BindingFlags.Static)!;
                var resultInstance = failureMethod.Invoke(null, new object[] { code, msg })!;
                return typeof(Task).GetMethod(nameof(Task.FromResult))!
                    .MakeGenericMethod(taskArg)
                    .Invoke(null, new[] { resultInstance });
            }
        }

        throw new InvalidOperationException(
            $"OnDenied=ReturnFailure requires the method to return Result, Result<T>, Task<Result> or Task<Result<T>>. " +
            $"Method '{joinPoint.MethodInfo.DeclaringType?.Name}.{joinPoint.MethodInfo.Name}' returns '{returnType}'.");
    }
}
