using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evolith.Ums.Infrastructure.Persistence.Migrations.PostgreSql
{
    /// <summary>
    /// G-173: reescribe el predicado de la politica <c>tenant_isolation_policy</c> creada en
    /// <c>20260720152552_EnableRowLevelSecurity</c> para que la comparacion por inquilino sea
    /// del mismo tipo por ambos lados y deje de costar una llamada por fila.
    ///
    /// <para><b>Que estaba mal.</b> El predicado original era
    /// <c>current_setting('app.current_organization_id', true) = '' OR "TenantId"::text = current_setting(...)</c>.
    /// Dos defectos independientes:</para>
    /// <list type="number">
    ///   <item><description>
    ///     <c>"TenantId"::text</c> compara uuid convertido a texto contra texto. El indice
    ///     <c>IX_*_TenantId</c> esta construido sobre el uuid, no sobre su representacion textual,
    ///     asi que el predicado nunca puede resolverse por indice; ademas obliga a materializar
    ///     una cadena de 36 bytes por cada fila evaluada.
    ///   </description></item>
    ///   <item><description>
    ///     <c>current_setting</c> es STABLE, no IMMUTABLE: PostgreSQL no la pliega en tiempo de
    ///     planificacion y la invoca <b>una vez por fila</b>. Con el <c>OR</c> por delante, el
    ///     coste se paga sobre todas las filas de la tabla, no sobre las del inquilino.
    ///   </description></item>
    /// </list>
    ///
    /// <para><b>Que hace este arreglo.</b> Envuelve cada lectura de la GUC en una subconsulta
    /// escalar sin correlacion. PostgreSQL la convierte en un <c>InitPlan</c> y la evalua
    /// <b>una sola vez por sentencia</b>, dejando en el filtro una simple comparacion contra un
    /// parametro de ejecucion; y compara <c>uuid = uuid</c>, sin conversion. Medido sobre 600.000
    /// filas y 300 inquilinos, el plan de la ruta caliente pasa de
    /// <c>Filter: (current_setting(...) = '' OR ("TenantId")::text = current_setting(...))</c>
    /// a <c>Filter: (($0 = ''::text) OR ("TenantId" = $1))</c>: mismo <c>Index Cond</c> sobre
    /// <c>IX_*_TenantId</c>, entre 3x y 5x menos tiempo de ejecucion.
    ///
    /// <para><b>Por que se conserva el <c>OR</c>.</b> La compuerta "GUC vacia = sin restriccion"
    /// es contractual (admin interno, sistema y siembra; ver <c>OrganizationDbContextInterceptor</c>)
    /// y no puede expresarse como igualdad. Se probo sustituirla por un rango conjuntivo
    /// (<c>"TenantId" BETWEEN $0 AND $1</c>), que si convierte el propio predicado RLS en
    /// <c>Index Cond</c>; se descarto porque el planificador no conoce los parametros y elige ese
    /// indice tambien en modo bypass, donde debe leer la tabla entera: 129.354 buffers frente a
    /// 7.999 del recorrido secuencial. Se prefiere no degradar siembra ni migraciones.</para>
    ///
    /// <para><b>Semantica preservada, bit a bit.</b> En particular el caso <i>fail-closed</i>:
    /// si la GUC no se fijo nunca en la sesion, <c>current_setting(..., true)</c> devuelve NULL,
    /// <c>NULL = ''</c> es NULL y la fila queda oculta. Por eso la compuerta compara el texto
    /// crudo contra <c>''</c> y NO usa <c>NULLIF(...) IS NULL</c>: esa variante, aparentemente
    /// equivalente, trata la GUC ausente como "sin restriccion" y abre la tabla completa
    /// (verificado: 600.000 filas visibles en lugar de 0).</para>
    ///
    /// <para>Efecto colateral positivo: al evaluarse en el lider y propagarse como parametro
    /// (<c>Params Evaluated</c>), los planes paralelos dejan de depender de que cada worker
    /// reciba la GUC, un caso en el que la politica anterior devolvia recuentos incompletos.</para>
    ///
    /// <para>Referencia: ADR-0111 (arquitectura de datos PostgreSQL) y ADR-0010 (el aislamiento
    /// vive en RBAC/ABAC de aplicacion; la RLS es defensa en profundidad, no el control primario).</para>
    /// </summary>
    public partial class FixRlsTenantComparisonTypeMismatch : Migration
    {
        // Mismo conjunto de tablas que 20260720152552_EnableRowLevelSecurity: si esa lista cambia,
        // esta debe cambiar con ella. El flag indica TenantId anulable (filas globales).
        private static readonly (string Schema, string TableName, bool IsGlobalNullable)[] Tables =
        [
            ("ums_identity", "TenantBranches", false),
            ("ums_identity", "TenantParameters", false),
            ("ums_identity", "TenantIdentityProviders", false),
            ("ums_identity", "UserAccounts", false),
            ("ums_authorization", "Profiles", false),
            ("ums_authorization", "Roles", false),
            ("ums_authorization", "SystemSuites", false),
            ("ums_authorization", "PermissionTemplates", false),
            ("ums_identity", "UserManagementDelegations", false),
            ("ums_configuration", "IdpConfigurations", false),
            ("ums_configuration", "AppConfigurations", true),
            ("ums_configuration", "ParameterTenantValues", false),
            ("approvals", "ApprovalWorkflows", false),
            ("approvals", "NotificationRules", false),
            ("approvals", "DocumentTypes", false),
            ("approvals", "AccessEnforcementPolicies", false),
            ("iga", "RoleMaturityStatuses", false),
            ("iga", "RolePromotionRequests", false)
        ];

        // Subconsulta escalar sin correlacion: PostgreSQL la resuelve como InitPlan, una vez por
        // sentencia, en lugar de invocar current_setting() una vez por fila.
        private const string GucRaw =
            "(SELECT current_setting('app.current_organization_id', true))";

        // NULLIF(...,'') distingue "sin restriccion" (cadena vacia) de un identificador real; el
        // cast a uuid se aplica al PARAMETRO, no a la columna, que es lo que mantiene el indice usable.
        private const string GucAsUuid =
            "(SELECT NULLIF(current_setting('app.current_organization_id', true), '')::uuid)";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var t in Tables)
            {
                // El predicado nuevo. El orden de los terminos importa poco al planificador, pero
                // deja primero la compuerta para que el modo bypass corte sin tocar el InitPlan uuid.
                var policyUsing = t.IsGlobalNullable
                    ? $@"{GucRaw} = '' OR ""TenantId"" IS NULL OR ""TenantId"" = {GucAsUuid}"
                    : $@"{GucRaw} = '' OR ""TenantId"" = {GucAsUuid}";

                // DROP + CREATE en lugar de ALTER POLICY: la migracion corre dentro de una
                // transaccion, asi que no existe ventana en la que la tabla quede sin politica
                // (con FORCE ROW LEVEL SECURITY eso equivaldria a denegar cualquier lectura).
                migrationBuilder.Sql(
                    $@"DROP POLICY IF EXISTS tenant_isolation_policy ON ""{t.Schema}"".""{t.TableName}"";");

                migrationBuilder.Sql($@"
                    CREATE POLICY tenant_isolation_policy ON ""{t.Schema}"".""{t.TableName}""
                    FOR ALL
                    USING ({policyUsing});
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restaura literalmente el predicado de 20260720152552_EnableRowLevelSecurity,
            // incluida la conversion a texto: el Down debe devolver la base al estado anterior,
            // no a uno "mejor".
            foreach (var t in Tables)
            {
                var policyUsing = t.IsGlobalNullable
                    ? @"current_setting('app.current_organization_id', true) = '' OR ""TenantId"" IS NULL OR ""TenantId""::text = current_setting('app.current_organization_id', true)"
                    : @"current_setting('app.current_organization_id', true) = '' OR ""TenantId""::text = current_setting('app.current_organization_id', true)";

                migrationBuilder.Sql(
                    $@"DROP POLICY IF EXISTS tenant_isolation_policy ON ""{t.Schema}"".""{t.TableName}"";");

                migrationBuilder.Sql($@"
                    CREATE POLICY tenant_isolation_policy ON ""{t.Schema}"".""{t.TableName}""
                    FOR ALL
                    USING ({policyUsing});
                ");
            }
        }
    }
}
