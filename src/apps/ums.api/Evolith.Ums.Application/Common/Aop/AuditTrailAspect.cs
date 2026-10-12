namespace Evolith.Ums.Application.Common.Aop;

public sealed class AuditTrailAspect : AbstractAspect<AuditTrailAttribute>
{
    private static readonly Guid SystemActorId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security Hotspot", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields",
        Justification = "ADR-UMS-099: reflexión sancionada para AOP transversal. DispatchProxy (ADR-UMS-060) es " +
                        "síncrono; resolver el wrapper genérico privado WrapAsyncOfT por reflexión es la única vía " +
                        "para preservar la async-correctness de Task<TResult>.")]
    private static readonly MethodInfo WrapAsyncOfTMethod =
        typeof(AuditTrailAspect).GetMethod(nameof(WrapAsyncOfT), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly string[] KnownPrefixes =
    [
        "Create", "Update", "Delete", "Activate", "Deactivate", "Archive", "Publish",
        "Suspend", "Restore", "Block", "Remove", "Add", "Set", "Register", "Upload",
        "Validate", "Verify", "Submit", "Approve", "Reject", "Record", "Fail",
        "Expire", "Revoke", "Reactivate"
    ];

    private readonly IAuditTrailSink _auditTrailSink;
    private readonly IUserContext _userContext;
    private readonly IRequestContext _requestContext;

    public AuditTrailAspect(
        IAuditTrailSink auditTrailSink,
        IUserContext userContext,
        IRequestContext requestContext)
    {
        _auditTrailSink = auditTrailSink;
        _userContext = userContext;
        _requestContext = requestContext;
    }

    public override void Apply(IJoinPoint joinPoint)
    {
        var attribute = GetAttribute(joinPoint);
        if (attribute is null)
        {
            Proceed(joinPoint);
            return;
        }

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
                        .Invoke(this, [joinPoint, returnedTask, attribute])!;
                }
                else
                {
                    joinPoint.Return = WrapAsync(joinPoint, returnedTask, attribute);
                }

                return;
            }

            CaptureAsync(joinPoint, attribute, joinPoint.Return, null).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            CaptureAsync(joinPoint, attribute, null, ex).GetAwaiter().GetResult();
            throw;
        }
    }

    private void Proceed(IJoinPoint joinPoint)
    {
        if (GetNext() is null)
            joinPoint.Proceed();
        else
            GetNext()!.Apply(joinPoint);
    }

    private async Task WrapAsync(IJoinPoint joinPoint, Task task, AuditTrailAttribute attribute)
    {
        try
        {
            await task.ConfigureAwait(false);
            await CaptureAsync(joinPoint, attribute, null, null).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await CaptureAsync(joinPoint, attribute, null, ex).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<TResult> WrapAsyncOfT<TResult>(IJoinPoint joinPoint, Task task, AuditTrailAttribute attribute)
    {
        try
        {
            var result = await ((Task<TResult>)task).ConfigureAwait(false);
            await CaptureAsync(joinPoint, attribute, result, null).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            await CaptureAsync(joinPoint, attribute, null, ex).ConfigureAwait(false);
            throw;
        }
    }

    private async Task CaptureAsync(IJoinPoint joinPoint, AuditTrailAttribute attribute, object? resultObject, Exception? exception)
    {
        var actorId = ResolveActorId();
        var entityId = ResolveAffectedEntityId(joinPoint, resultObject);
        if (entityId == Guid.Empty)
            return;

        var entityType = attribute.AffectedEntityType ?? ResolveEntityType(joinPoint);
        if (string.IsNullOrWhiteSpace(entityType))
            return;

        var auditResult = ResolveAuditResult(resultObject, exception);
        var eventType = attribute.EventType ?? ResolveEventType(joinPoint);
        var whatChanged = attribute.WhatChanged ?? $"{eventType} executed";
        var subjectType = attribute.SubjectType ?? (_userContext.IsAuthenticated ? SubjectType.Admin.Name : SubjectType.System.Name);
        var rootTenantId = ResolveRootTenantId(joinPoint);

        var metadata = JsonSerializer.Serialize(new
        {
            Handler = joinPoint.TargetType.Name,
            Method = joinPoint.MethodInfo.Name,
            RequestType = ResolveRequest(joinPoint)?.GetType().Name,
            SessionTrackingId = _requestContext.SessionTrackingId,
            CorrelationId = _requestContext.CorrelationId,
            TraceId = _requestContext.TraceId,
            SpanId = _requestContext.SpanId,
            Exception = exception?.Message,
        });

        // G-040 (FR-072): la metadata se desinfecta de forma centralizada en el sink
        // (AuditTrailOutboxSink), único punto por el que pasan TODAS las emisiones automáticas de la
        // traza (este aspecto y ConfigurationAuditService) antes de encolarse por el outbox.
        await _auditTrailSink.PublishAsync(new AuditTrailEntry(
            actorId,
            subjectType,
            whatChanged,
            eventType,
            auditResult,
            entityId,
            entityType,
            rootTenantId,
            metadata)).ConfigureAwait(false);
    }

    private Guid ResolveActorId()
        => Guid.TryParse(_userContext.UserId, out var actorId)
            ? actorId
            : SystemActorId;

    private Guid ResolveRootTenantId(IJoinPoint joinPoint)
    {
        var request = ResolveRequest(joinPoint);
        if (request is not null)
        {
            if (TryReadGuidProperty(request, "RootTenantId", out var rootTenantId))
                return rootTenantId;

            if (TryReadGuidProperty(request, "TenantId", out var tenantId))
                return tenantId;
        }

        return Guid.TryParse(_userContext.TenantId, out var contextTenantId)
            ? contextTenantId
            : Guid.Empty;
    }

    private static object? ResolveRequest(IJoinPoint joinPoint)
        => joinPoint.Arguments.FirstOrDefault(argument => argument is not CancellationToken);

    private static string ResolveAuditResult(object? resultObject, Exception? exception)
    {
        if (exception is not null)
            return AuditResult.Failure.Name;

        if (resultObject is Result result)
            return result.IsSuccess ? AuditResult.Success.Name : AuditResult.Failure.Name;

        return AuditResult.Success.Name;
    }

    private static Guid ResolveAffectedEntityId(IJoinPoint joinPoint, object? resultObject)
    {
        if (TryResolveEntityIdFromResponse(resultObject, out var responseEntityId))
            return responseEntityId;

        var request = ResolveRequest(joinPoint);
        if (request is null)
            return Guid.Empty;

        if (TryReadGuidProperty(request, "Id", out var id))
            return id;

        var requestIdProperty = request.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(property =>
                property.PropertyType == typeof(Guid)
                && property.Name.EndsWith("Id", StringComparison.Ordinal)
                && !string.Equals(property.Name, "TenantId", StringComparison.Ordinal)
                && !string.Equals(property.Name, "RootTenantId", StringComparison.Ordinal));

        return requestIdProperty?.GetValue(request) is Guid value ? value : Guid.Empty;
    }

    private static bool TryResolveEntityIdFromResponse(object? resultObject, out Guid entityId)
    {
        entityId = Guid.Empty;
        if (resultObject is null)
            return false;

        var resultType = resultObject.GetType();
        if (!resultType.IsGenericType || resultType.GetGenericTypeDefinition() != typeof(Result<>))
            return false;

        if (resultType.GetProperty(nameof(Result.IsSuccess))?.GetValue(resultObject) is not bool isSuccess || !isSuccess)
            return false;

        var value = resultType.GetProperty("Value")?.GetValue(resultObject);
        if (value is null)
            return false;

        var idProperty = value.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(property =>
                property.PropertyType == typeof(Guid)
                && property.Name.EndsWith("Id", StringComparison.Ordinal)
                && !string.Equals(property.Name, "TenantId", StringComparison.Ordinal)
                && !string.Equals(property.Name, "RootTenantId", StringComparison.Ordinal));

        if (idProperty?.GetValue(value) is Guid resolved && resolved != Guid.Empty)
        {
            entityId = resolved;
            return true;
        }

        return false;
    }

    private static bool TryReadGuidProperty(object target, string propertyName, out Guid value)
    {
        value = Guid.Empty;
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property?.GetValue(target) is Guid guid && guid != Guid.Empty)
        {
            value = guid;
            return true;
        }

        return false;
    }

    private static string ResolveEventType(IJoinPoint joinPoint)
    {
        var handlerName = joinPoint.TargetType.Name;
        var resolved = handlerName.EndsWith("Handler", StringComparison.Ordinal)
            ? handlerName[..^"Handler".Length]
            : handlerName;

        if (resolved.EndsWith("Command", StringComparison.Ordinal))
            resolved = resolved[..^"Command".Length];
        else if (resolved.EndsWith("Query", StringComparison.Ordinal))
            resolved = resolved[..^"Query".Length];

        return resolved;
    }

    private static string ResolveEntityType(IJoinPoint joinPoint)
    {
        var handlerName = ResolveEventType(joinPoint);
        foreach (var prefix in KnownPrefixes)
        {
            if (handlerName.StartsWith(prefix, StringComparison.Ordinal))
                return handlerName[prefix.Length..];
        }

        return handlerName;
    }
}
