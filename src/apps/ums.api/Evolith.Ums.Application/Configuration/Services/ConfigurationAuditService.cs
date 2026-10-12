namespace Evolith.Ums.Application.Configuration.Services;

using Evolith.Ums.Application.Common.Aop;

/// <summary>
/// Registra en la pista de auditoría los cambios de parámetros de configuración
/// (crear/modificar/eliminar/override), emitiéndolos por <see cref="IAuditTrailSink"/>.
///
/// <para>
/// <b>G-105 (residual de G-040#5) — redacción de nivel-valor por clasificación.</b> El saneador
/// <see cref="AuditMetadataSanitizer"/> redacta <b>por nombre de clave</b> y NO cubre este caso: aquí
/// el valor de un parámetro aterriza bajo las claves <c>PreviousValue</c>/<c>NewValue</c>, que no son
/// nombres sensibles. Si el parámetro está <b>clasificado como cifrado/secreto</b>
/// (<see cref="Evolith.Ums.Domain.Configuration.AppConfiguration.AppConfigurationProps.IsEncrypted"/>,
/// la misma bandera que cifra el valor en reposo y lo redacta por REST), su valor en claro se filtraría
/// a la traza. Es una fuga de <b>nivel-valor</b> gobernada por la <i>clasificación</i> del parámetro,
/// no por el nombre de la clave. Por eso, cuando el parámetro es secreto, su valor anterior y nuevo se
/// sustituyen por <see cref="AuditMetadataSanitizer.RedactionPlaceholder"/> ANTES de escribirlos en la
/// metadata. La traza es append-only e inmutable (G-081): un secreto filtrado a ella no se puede borrar
/// después.
/// </para>
///
/// <para>
/// Los parámetros <b>no secretos</b> siguen registrando su valor en claro: la auditoría de cambios de
/// configuración sigue siendo útil. Esto <b>complementa</b> —no duplica— al saneador por-clave: aquel
/// es por nombre de clave; éste es por clasificación de nivel-valor.
/// </para>
/// </summary>
public sealed class ConfigurationAuditService
{
    private readonly IAuditTrailSink _auditTrailSink;

    public ConfigurationAuditService(IAuditTrailSink auditTrailSink)
    {
        _auditTrailSink = auditTrailSink;
    }

    public async Task RecordConfigurationChangeAsync(
        Guid userId,
        string parameterCode,
        Guid? tenantId,
        string? previousValue,
        string? newValue,
        string operationType,
        Guid rootTenantId,
        bool isEncrypted,
        CancellationToken cancellationToken = default)
    {
        // G-105: el resultado (DELETED/MODIFIED) se deriva del valor ORIGINAL para preservar la
        // semántica; la redacción sólo afecta a lo que se persiste como metadata, no a la clasificación
        // del evento.
        var auditResult = string.IsNullOrEmpty(newValue) ? "DELETED" : "MODIFIED";

        var entry = new AuditTrailEntry(
            WhoActed: userId,
            SubjectType: "AppConfiguration",
            WhatChanged: $"Parameter '{parameterCode}' {operationType}",
            EventType: operationType,
            AuditResult: auditResult,
            AffectedEntityId: Guid.Empty,
            AffectedEntityType: "AppConfiguration",
            RootTenantId: rootTenantId,
            Metadata: System.Text.Json.JsonSerializer.Serialize(new
            {
                ParameterCode = parameterCode,
                TenantId = tenantId,
                PreviousValue = RedactIfSecret(previousValue, isEncrypted),
                NewValue = RedactIfSecret(newValue, isEncrypted),
                Operation = operationType
            }));

        await _auditTrailSink.PublishAsync(entry, cancellationToken);
    }

    public Task RecordParameterOverrideAsync(
        Guid userId,
        string parameterCode,
        Guid tenantId,
        string? previousValue,
        string? newValue,
        Guid rootTenantId,
        bool isEncrypted,
        CancellationToken cancellationToken = default)
        => RecordConfigurationChangeAsync(
            userId,
            parameterCode,
            tenantId,
            previousValue,
            newValue,
            "OVERRIDE",
            rootTenantId,
            isEncrypted,
            cancellationToken);

    // G-105: si el parámetro está clasificado como cifrado/secreto, su valor no debe aterrizar en claro
    // en la traza inmutable → se redacta. Un valor nulo/vacío no tiene nada que ocultar (y redactarlo
    // fingiría que existía un valor), así que se deja tal cual; sólo se redacta lo que sí es contenido.
    private static string? RedactIfSecret(string? value, bool isEncrypted)
        => isEncrypted && !string.IsNullOrEmpty(value)
            ? AuditMetadataSanitizer.RedactionPlaceholder
            : value;
}
