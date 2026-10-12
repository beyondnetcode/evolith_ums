namespace Evolith.Ums.Domain.Authorization.SystemSuite;

/// <summary>
/// Referencias VIVAS a un sistema desde FUERA de su agregado: las que impiden eliminarlo
/// lógicamente (G-246).
///
/// <para><b>Qué cuenta como viva.</b> La regla del propietario es la de una cascada de base de datos
/// trasladada al plano lógico: no se puede eliminar lógicamente algo a lo que todavía apuntan
/// referencias que no se han eliminado lógicamente primero. Una fila ya eliminada lógicamente es una
/// lápida y no estorba; una fila viva sí, porque quedaría colgando de un sistema ilegible.</para>
///
/// <para><b>Y aquí está el matiz incómodo.</b> De los siete referentes, hoy solo el inquilino
/// (<c>Tenants.IsDeleted</c>) tiene eliminación lógica de verdad. Roles, plantillas, banderas,
/// configuraciones de IdP, app-configs y flujos de aprobación no la tienen: sus estados —<c>IsActive</c>,
/// <c>Deprecated</c>, <c>Archived</c>— son ciclo de vida (suspendido, archivado), no borrado, y esa
/// distinción es precisamente la que este cambio establece para el sistema mismo (<c>Deprecated</c>
/// sigue leyéndose; <c>Deleted</c> no). Reinterpretar «archivado» como «eliminado» sería inventarse
/// una semántica que nadie decidió. Así que, mientras no exista la marca, para ellos TODA fila
/// existente cuenta como viva: fail-closed. La consecuencia práctica —un sistema con roles no se
/// puede eliminar hasta que los roles sepan eliminarse— es real y está reportada, no disimulada.</para>
///
/// <para><b>Qué NO se cuenta, y por qué.</b> Módulos, nodos, acciones, ajustes y recursos de dominio
/// son la composición del propio agregado: no existen fuera de él ni nadie de fuera los referencia,
/// y solo se leen a través del sistema, así que se ocultan con él. Contarlos convertiría la
/// operación en inservible —habría que desmontar el árbol nodo a nodo—. Los perfiles tampoco
/// aparecen: no referencian el sistema, cuelgan de un rol, y los roles ya bloquean; contarlos
/// exigiría una consulta por rol —el N+1 que ADR-0041 proscribe— para llegar al mismo bloqueo.</para>
/// </summary>
public readonly record struct SystemSuiteDependents(
    int Roles,
    int PermissionTemplates,
    int FeatureFlags,
    int IdpConfigurations,
    int AppConfigurations,
    int ApprovalWorkflows,
    int TenantsUsingAsDefault)
{
    /// <summary>Sin referencias vivas: el único caso en que se puede eliminar lógicamente.</summary>
    public static SystemSuiteDependents None => default;

    /// <summary>Total de referencias vivas. Cero es la condición de la guarda de cascada.</summary>
    public int LiveReferences =>
        Roles + PermissionTemplates + FeatureFlags + IdpConfigurations +
        AppConfigurations + ApprovalWorkflows + TenantsUsingAsDefault;

    /// <summary>
    /// Traduce los conteos no nulos al detalle estructurado que viaja en el 409
    /// (<c>BlockedOperationResponse</c>). Vive aquí, y no en el manejador, para que quien añada un
    /// referente nuevo al conteo lo vea aparecer en la respuesta sin tener que acordarse de nada.
    /// </summary>
    public IReadOnlyList<BlockingDependency> ToBlockingDependencies()
    {
        var blockers = new List<BlockingDependency>(7);

        Add(blockers, "Role", Roles);
        Add(blockers, "PermissionTemplate", PermissionTemplates);
        Add(blockers, "FeatureFlag", FeatureFlags);
        Add(blockers, "IdpConfiguration", IdpConfigurations);
        Add(blockers, "AppConfiguration", AppConfigurations);
        Add(blockers, "ApprovalWorkflow", ApprovalWorkflows);
        Add(blockers, "Tenant", TenantsUsingAsDefault);

        return blockers;
    }

    private static void Add(List<BlockingDependency> blockers, string entityType, int count)
    {
        if (count > 0)
        {
            // «Live» y no «Active»: lo que bloquea no es que la referencia esté activa —un rol
            // desactivado también bloquea—, sino que no se haya eliminado lógicamente.
            blockers.Add(new BlockingDependency(entityType, "Live", count));
        }
    }
}
