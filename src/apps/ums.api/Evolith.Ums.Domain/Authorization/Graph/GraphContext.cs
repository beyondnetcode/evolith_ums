using System.Text.Json.Serialization;

namespace Evolith.Ums.Domain.Authorization.Graph;

/// <summary>
/// Identity and location context of the authenticated principal — who they are
/// and in which tenant / system / role / branch they are operating.
/// </summary>
public sealed record GraphContext(
    GraphUser        User,
    GraphTenant      Tenant,
    GraphSystemSuite? SystemSuite,   // null en el grafo lobby (onboarding pendiente, G-043)
    GraphRole?        Role,          // null en el grafo lobby
    GraphProfile?     Profile,       // null en el grafo lobby
    GraphBranch?     Branch,        // null when Scope == OrgWide
    GraphRequestedSystem? RequestedSystem = null);  // eco del sistema pedido (ADR-0156 §2.5)

/// <summary>
/// Eco literal del sistema que el cliente pidió al autenticarse, ya normalizado. Es `null` cuando
/// no pidió ninguno (portal multiproducto).
///
/// NO es una lectura del catálogo: no revela nada que el llamante no supiera ya, porque es su
/// propia entrada devuelta. Existe para que el cliente pueda decir «no tiene acceso a SDLC» sin
/// llevar su configuración al navegador, y para que un grafo capturado como evidencia sea
/// autodescriptivo — ante un grafo de otra suite, hoy no hay forma de saber si se pidió algo
/// distinto.
/// </summary>
public sealed record GraphRequestedSystem(string Code);

public sealed record GraphUser(
    [property: JsonIgnore] Guid Id,
    string Email,
    string Username,
    [property: JsonPropertyName("value")] string DisplayName,
    string Status);

public sealed record GraphTenant(
    [property: JsonIgnore] Guid Id,
    string Code,
    [property: JsonPropertyName("value")] string Name,
    string Status,
    bool   IsManagementOwner);

public sealed record GraphSystemSuite(
    [property: JsonIgnore] Guid Id,
    string Code,
    [property: JsonPropertyName("value")] string Name,
    string Status);

public sealed record GraphRole(
    [property: JsonIgnore] Guid Id,
    string Code,
    [property: JsonPropertyName("value")] string Name,
    int    HierarchyLevel,
    [property: JsonIgnore]
    Guid?  ParentRoleId);

public sealed record GraphProfile(
    [property: JsonIgnore] Guid Id,
    string Scope,       // "OrgWide" | "BranchScoped"
    bool   IsActive);

public sealed record GraphBranch(
    [property: JsonIgnore] Guid Id,
    string Code,
    [property: JsonPropertyName("value")] string Name);

/// <summary>
/// Un perfil al que el usuario autenticado tiene acceso: la combinación de un rol dentro de un
/// sistema, opcionalmente acotada a una sucursal.
///
/// Viaja la lista completa para que el cliente pueda ofrecer el cambio de perfil sin preguntar de
/// nuevo (G-177). Antes se elegía uno y los demás se descartaban sin dejar rastro: el usuario no
/// sabía siquiera que existían.
///
/// El sistema NO sale del perfil —que no lo guarda— sino de su rol, que pertenece a exactamente
/// uno. Se proyecta aquí porque el cliente lo necesita para rotular el selector.
/// </summary>
public sealed record GraphProfileOption(
    [property: JsonIgnore] Guid Id,
    string  SystemCode,
    [property: JsonPropertyName("systemValue")] string SystemName,
    string  RoleCode,
    [property: JsonPropertyName("roleValue")] string RoleName,
    int     HierarchyLevel,
    string? BranchCode,
    [property: JsonPropertyName("branchValue")] string? BranchName,
    string  Scope,
    bool    IsCurrent);
