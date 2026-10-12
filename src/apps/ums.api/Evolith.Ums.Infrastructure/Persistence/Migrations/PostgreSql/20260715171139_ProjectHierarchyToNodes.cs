using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evolith.Ums.Infrastructure.Persistence.Migrations.PostgreSql
{
    /// <summary>
    /// Proyección de datos (ADR-0090): copia la jerarquía rígida
    /// Menús → Submenús → Opciones al árbol recursivo `SystemSuiteNodes`,
    /// reusando los `Id` para que la jerarquía se enlace sola
    /// (submenu.MenuId ⇒ nodo-menú; option.SubMenuId ⇒ nodo-submenú) y las
    /// funcionalidades de las opciones al puente N:M `SystemSuiteNodeActions`.
    /// Idempotente (NOT EXISTS). No borra el modelo rígido (se retira en Fase E).
    /// </summary>
    public partial class ProjectHierarchyToNodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Menús → nodos raíz (NodeKindId = 1, sin padre).
            migrationBuilder.Sql(@"
INSERT INTO ums_authorization.""SystemSuiteNodes""
    (""Id"", ""ModuleId"", ""ParentNodeId"", ""NodeKindId"", ""Code"", ""Label"", ""Description"",
     ""StatusId"", ""SortOrder"", ""CreatedBy"", ""CreatedAtUtc"", ""UpdatedBy"", ""UpdatedAtUtc"", ""AuditTimeSpan"")
SELECT m.""Id"", m.""ModuleId"", NULL, 1, m.""Code"", m.""Label"", m.""Description"",
       1, m.""SortOrder"", m.""CreatedBy"", m.""CreatedAtUtc"", m.""UpdatedBy"", m.""UpdatedAtUtc"", m.""AuditTimeSpan""
FROM ums_authorization.""SystemSuiteMenus"" m
WHERE NOT EXISTS (SELECT 1 FROM ums_authorization.""SystemSuiteNodes"" n WHERE n.""Id"" = m.""Id"");");

            // 2) Submenús → nodos (NodeKindId = 2, padre = nodo-menú; módulo del menú).
            migrationBuilder.Sql(@"
INSERT INTO ums_authorization.""SystemSuiteNodes""
    (""Id"", ""ModuleId"", ""ParentNodeId"", ""NodeKindId"", ""Code"", ""Label"", ""Description"",
     ""StatusId"", ""SortOrder"", ""CreatedBy"", ""CreatedAtUtc"", ""UpdatedBy"", ""UpdatedAtUtc"", ""AuditTimeSpan"")
SELECT sm.""Id"", mn.""ModuleId"", sm.""MenuId"", 2, sm.""Code"", sm.""Label"", sm.""Description"",
       1, sm.""SortOrder"", sm.""CreatedBy"", sm.""CreatedAtUtc"", sm.""UpdatedBy"", sm.""UpdatedAtUtc"", sm.""AuditTimeSpan""
FROM ums_authorization.""SystemSuiteSubMenus"" sm
JOIN ums_authorization.""SystemSuiteMenus"" mn ON mn.""Id"" = sm.""MenuId""
WHERE NOT EXISTS (SELECT 1 FROM ums_authorization.""SystemSuiteNodes"" n WHERE n.""Id"" = sm.""Id"");");

            // 3) Opciones → nodos hoja (NodeKindId = 3, padre = nodo-submenú; módulo del menú).
            migrationBuilder.Sql(@"
INSERT INTO ums_authorization.""SystemSuiteNodes""
    (""Id"", ""ModuleId"", ""ParentNodeId"", ""NodeKindId"", ""Code"", ""Label"", ""Description"",
     ""StatusId"", ""SortOrder"", ""CreatedBy"", ""CreatedAtUtc"", ""UpdatedBy"", ""UpdatedAtUtc"", ""AuditTimeSpan"")
SELECT o.""Id"", mn.""ModuleId"", o.""SubMenuId"", 3, o.""Code"", o.""Label"", o.""Description"",
       1, o.""SortOrder"", o.""CreatedBy"", o.""CreatedAtUtc"", o.""UpdatedBy"", o.""UpdatedAtUtc"", o.""AuditTimeSpan""
FROM ums_authorization.""SystemSuiteOptions"" o
JOIN ums_authorization.""SystemSuiteSubMenus"" sm ON sm.""Id"" = o.""SubMenuId""
JOIN ums_authorization.""SystemSuiteMenus"" mn ON mn.""Id"" = sm.""MenuId""
WHERE NOT EXISTS (SELECT 1 FROM ums_authorization.""SystemSuiteNodes"" n WHERE n.""Id"" = o.""Id"");");

            // 4) ActionCode de cada opción → puente N:M.
            migrationBuilder.Sql(@"
INSERT INTO ums_authorization.""SystemSuiteNodeActions"" (""Id"", ""NodeId"", ""ActionCode"")
SELECT gen_random_uuid(), o.""Id"", o.""ActionCode""
FROM ums_authorization.""SystemSuiteOptions"" o
WHERE o.""ActionCode"" IS NOT NULL AND o.""ActionCode"" <> ''
AND NOT EXISTS (
    SELECT 1 FROM ums_authorization.""SystemSuiteNodeActions"" na
    WHERE na.""NodeId"" = o.""Id"" AND na.""ActionCode"" = o.""ActionCode"");");
        }

        /// <inheritdoc />
        #pragma warning disable S1186
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revierte solo los nodos proyectados (cuyo Id proviene del modelo rígido).
            migrationBuilder.Sql(@"
DELETE FROM ums_authorization.""SystemSuiteNodeActions"" na
USING ums_authorization.""SystemSuiteOptions"" o
WHERE na.""NodeId"" = o.""Id"";");

            migrationBuilder.Sql(@"
DELETE FROM ums_authorization.""SystemSuiteNodes"" n
WHERE n.""Id"" IN (SELECT ""Id"" FROM ums_authorization.""SystemSuiteOptions"")
   OR n.""Id"" IN (SELECT ""Id"" FROM ums_authorization.""SystemSuiteSubMenus"")
   OR n.""Id"" IN (SELECT ""Id"" FROM ums_authorization.""SystemSuiteMenus"");");
        }
#pragma warning restore S1186
    }
}
