namespace Evolith.Ums.Application.Authorization.Template.Commands;

public sealed class AddTemplateItemCommandValidator : AbstractValidator<AddTemplateItemCommand>
{
    /// <summary>
    /// Destinos admitidos, DERIVADOS de <see cref="ExclusiveArcTarget"/>.
    ///
    /// G-192: aquí había una copia a mano —SystemSuite, Module, Submodule, Option— que dejó fuera
    /// a <c>Aggregate</c> y <c>Entity</c>. El dominio sí los admite (<c>PermissionTemplate.AddItem</c>
    /// no restringe el arco), <c>Profile.AssignTemplate</c> los copia tal cual al permiso del perfil
    /// y <c>AuthorizationGraphBuilderService.BuildDomainPermissions</c> los proyecta casando
    /// (TargetId, ActionId) contra los recursos de dominio de la suite; el seeder canónico los usa.
    /// O sea: el único que los negaba era este validador, y con él ninguna concesión sobre un objeto
    /// de dominio podía darse de alta por API, dejando `domainPermissions` vacío en todo perfil
    /// provisionado así. Derivar la lista del enumerado hace que la divergencia no pueda repetirse.
    /// </summary>
    internal static readonly IReadOnlyList<string> ValidTargetTypes =
        DomainEnumerationParser.NamesOf<ExclusiveArcTarget>();

    internal static readonly string ValidTargetTypesMessage =
        $"TargetType must be one of: {string.Join(", ", ValidTargetTypes)}.";

    public AddTemplateItemCommandValidator()
    {
        RuleFor(x => x.TemplateId).NotEmpty();
        RuleFor(x => x.TargetType)
            .NotEmpty()
            // Se resuelve con el MISMO criterio que usará el manejador (recorte y sin distinguir
            // mayúsculas): si el validador aceptara una forma que el manejador no sabe resolver, el
            // alta fallaría después con un error opaco en vez de con un 400 explícito.
            .Must(v => DomainEnumerationParser.FromName<ExclusiveArcTarget>(v) is not null)
            .WithMessage(ValidTargetTypesMessage);
        RuleFor(x => x.TargetId).NotEmpty();
        RuleFor(x => x.ActionId).NotEmpty();
        RuleFor(x => x).Must(x => x.IsAllowed || x.IsDenied || (!x.IsAllowed && !x.IsDenied))
            .WithMessage("IsAllowed and IsDenied cannot both be true simultaneously.")
            .When(x => x.IsAllowed && x.IsDenied);
    }
}
