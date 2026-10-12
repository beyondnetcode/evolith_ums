using System;
using System.Collections.Generic;
using System.Linq;

using MenuNodeEntity = Evolith.Ums.Domain.Authorization.SystemSuite.MenuNode.MenuNode;

namespace Evolith.Ums.Application.Authorization.SystemSuite.DTOs;

public sealed record SystemSuiteDto(
    Guid SystemSuiteId,
    Guid TenantId,
    string Code,
    string Name,
    string Description,
    string Status,
    IReadOnlyList<SystemSuiteModuleDto> Modules,
    IReadOnlyList<SystemSuiteActionDto> Actions,
    IReadOnlyList<SystemSuiteDomainResourceDto> DomainResources)
{
    public static SystemSuiteDto Map(Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuite suite)
    {
        return new SystemSuiteDto(
            suite.Props.Id.GetValue(),
            suite.Props.TenantId.GetValue(),
            suite.Props.Code.GetValue(),
            suite.Props.Name.GetValue(),
            suite.Props.Description.GetValue(),
            suite.Props.Status.ToString(),
            suite.Modules.Select(m => new SystemSuiteModuleDto(
                m.Props.Id.GetValue(),          // Props.Id = stable DB GUID (Entity.Id is transient per-rehydration)
                m.Code.GetValue(),
                m.Name.GetValue(),
                m.Description.GetValue(),
                m.Status.ToString(),
                m.SortOrder,
                m.Nodes.Select(MapNode).ToList()
            )).ToList(),
            suite.Actions.Select(a => new SystemSuiteActionDto(
                a.Props.Id.GetValue(),          // Props.Id = stable DB GUID
                a.Code.GetValue(),
                a.Name.GetValue()
            )).ToList(),
            suite.DomainResources.Select(dr => new SystemSuiteDomainResourceDto(
                dr.Props.Id.GetValue(),
                dr.ModuleId?.GetValue(),
                dr.ParentResourceId?.GetValue(),
                dr.Type.Name,
                dr.Code.GetValue(),
                dr.Name.GetValue(),
                dr.Description.GetValue()
            )).ToList()
        );
    }

    private static SystemSuiteNodeDto MapNode(MenuNodeEntity node)
    {
        var meta = node.Metadata;
        return new SystemSuiteNodeDto(
            node.GetId().GetValue(),
            node.ParentNodeId?.GetValue(),
            node.Kind.ToString(),
            node.Code.GetValue(),
            node.Label.GetValue(),
            node.Description.GetValue(),
            node.Status.ToString(),
            node.SortOrder,
            node.ActionCodes.Select(a => a.GetValue()).ToList(),
            meta.IsEmpty
                ? null
                : new SystemSuiteNodeMetadataDto(
                    meta.Responsable, meta.Criticidad, meta.ProductoImpactado,
                    meta.ComponenteTecnico, meta.Dependencias, meta.Evidencias, meta.TrazabilidadSdlc),
            node.Children.Select(MapNode).ToList());
    }
}

public sealed record SystemSuiteModuleDto(
    Guid Id,
    string Code,
    string Name,
    string Description,
    string Status,
    int SortOrder,
    IReadOnlyList<SystemSuiteNodeDto> Nodes);

public sealed record SystemSuiteNodeDto(
    Guid Id,
    Guid? ParentNodeId,
    string Kind,
    string Code,
    string Label,
    string Description,
    string Status,
    int SortOrder,
    IReadOnlyList<string> ActionCodes,
    SystemSuiteNodeMetadataDto? Metadata,
    IReadOnlyList<SystemSuiteNodeDto> Children);

public sealed record SystemSuiteNodeMetadataDto(
    string? Responsable,
    string? Criticidad,
    string? ProductoImpactado,
    string? ComponenteTecnico,
    string? Dependencias,
    string? Evidencias,
    string? TrazabilidadSdlc);

public sealed record SystemSuiteActionDto(
    Guid Id,
    string Code,
    string Name);

public sealed record SystemSuiteDomainResourceDto(
    Guid Id,
    Guid? ModuleId,
    Guid? ParentResourceId,
    string Type,
    string Code,
    string Name,
    string Description);
