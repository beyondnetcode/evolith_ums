namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class SystemParameterCode : StringValueObject
{
    public static readonly SystemParameterCode AccessMode = new("ACCESS_MODE");
    public static readonly SystemParameterCode InternalAdminTenantId = new("INTERNAL_ADMIN_TENANT_ID");
    public static readonly SystemParameterCode EnableCrossTenantAudit = new("ENABLE_CROSS_TENANT_AUDIT");
    public static readonly SystemParameterCode MaxTenantsPerAdmin = new("MAX_TENANTS_PER_ADMIN");
    public static readonly SystemParameterCode DefaultAdminRoleId = new("DEFAULT_ADMIN_ROLE_ID");

    private SystemParameterCode(string value) : base(value) { }

    public static SystemParameterCode Create(string value) => new(value);
}