namespace Evolith.Ums.Infrastructure.Persistence.Seeders;

using System.Reflection;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Identity.Tenant;
using Evolith.Ums.Domain.Identity.TenantSignupRequest;
using Evolith.Ums.Domain.Identity.UserAccount;
using Evolith.Ums.Domain.Identity.UserManagementDelegation;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Evolith.Ums.Infrastructure.Persistence.Identity;
using Microsoft.Extensions.DependencyInjection;
using TenantAggregate = Evolith.Ums.Domain.Identity.Tenant.Tenant;
using TenantSignupRequestAggregate = Evolith.Ums.Domain.Identity.TenantSignupRequest.TenantSignupRequest;
using UserAccountAggregate = Evolith.Ums.Domain.Identity.UserAccount.UserAccount;
using UserManagementDelegationAggregate = Evolith.Ums.Domain.Identity.UserManagementDelegation.UserManagementDelegation;

public static class IdentityDevDataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var tenantRepository = serviceProvider.GetService<ITenantRepository>();
        var inMemoryTenantRepository = serviceProvider.GetService<InMemoryTenantRepository>();

        var userAccountRepository = serviceProvider.GetService<IUserAccountRepository>();
        var inMemoryUserAccountRepository = serviceProvider.GetService<InMemoryUserAccountRepository>();

        var delegationRepository = serviceProvider.GetService<IUserManagementDelegationRepository>();
        var inMemoryDelegationRepository = serviceProvider.GetService<InMemoryUserManagementDelegationRepository>();

        var signupRequestRepository = serviceProvider.GetService<ITenantSignupRequestRepository>();
        var inMemorySignupRequestRepository = serviceProvider.GetService<InMemoryTenantSignupRequestRepository>();

        var passwordHasher = serviceProvider.GetService<IPasswordHashingService>();

        var actor = ActorId.Create(CoreDevDataSeeder.SystemActorId);

        // Seed Tenants (including Internal Admin Tenant)
        var tenants = BuildSeedTenants(actor);
        if (inMemoryTenantRepository is null && tenantRepository is not null)
        {
            var alreadySeeded = await tenantRepository.GetByCodeAsync("RANSA_PERU", cancellationToken);
            if (alreadySeeded is null)
            {
                foreach (var tenant in tenants)
                {
                    await tenantRepository.AddAsync(tenant, cancellationToken);
                }
                await tenantRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
            }
        }
        else if (inMemoryTenantRepository is not null)
        {
            foreach (var tenant in tenants)
            {
                inMemoryTenantRepository.Seed(tenant);
            }
        }

        // FS-25 Flujo B: reconcile a legacy EVOLITH snapshot (wrong RUC/branches) in place,
        // by its stable GUID — never recreate the tenant nor break existing references.
        if (inMemoryTenantRepository is null && tenantRepository is not null)
        {
            await ReconcileEvolithTenantAsync(tenantRepository, actor, cancellationToken);
        }

        // Branch GUIDs are generated inside the aggregate (Branch.Create), so we read them back
        // from the freshly built tenants by code and pass them to the users we associate (FS-25).
        var evolithCallaoBranchId = FindBranchId(tenants, "EVOLITH", "UNI_CALLAO");
        var evolithPaitaBranchId = FindBranchId(tenants, "EVOLITH", "UNI_PAITA");
        var impoAndinaSubBranchId = FindBranchId(tenants, CoreDevDataSeeder.ImpoAndinaSubTenantCode, CoreDevDataSeeder.ImpoAndinaSubBranchCode);

        // Seed / sync User Accounts (including SuperAdmin) so local password logins stay valid
        // even when the DB already contains an older dev snapshot.
        var userAccounts = BuildSeedUserAccounts(actor, passwordHasher, evolithCallaoBranchId, evolithPaitaBranchId, impoAndinaSubBranchId);
        if (inMemoryUserAccountRepository is null && userAccountRepository is not null)
        {
            foreach (var userAccount in userAccounts)
            {
                await UpsertUserAccountAsync(userAccountRepository, userAccount, actor, cancellationToken);
            }

            await userAccountRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        }
        else if (inMemoryUserAccountRepository is not null)
        {
            foreach (var userAccount in userAccounts)
            {
                await UpsertUserAccountAsync(inMemoryUserAccountRepository, userAccount, actor, cancellationToken);
            }
        }

        // Seed Delegations
        var delegations = BuildSeedDelegations(actor);
        if (inMemoryDelegationRepository is null && delegationRepository is not null)
        {
            var alreadySeeded = await delegationRepository.GetAllAsync(null, cancellationToken);
            if (alreadySeeded.Count == 0)
            {
                foreach (var delegation in delegations)
                {
                    await delegationRepository.AddAsync(delegation, cancellationToken);
                }
                await delegationRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
            }
        }
        else if (inMemoryDelegationRepository is not null)
        {
            foreach (var delegation in delegations)
            {
                inMemoryDelegationRepository.Seed(delegation);
            }
        }

        // Seed TenantSignupRequests (EP-09 onboarding inbox)
        var signupRequests = BuildSeedTenantSignupRequests(actor);
        if (inMemorySignupRequestRepository is null && signupRequestRepository is not null)
        {
            var alreadySeeded = await signupRequestRepository.GetAllAsync(cancellationToken);
            if (alreadySeeded.Count == 0)
            {
                foreach (var req in signupRequests)
                {
                    await signupRequestRepository.AddAsync(req, cancellationToken);
                }
                await signupRequestRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
            }
        }
        else if (inMemorySignupRequestRepository is not null)
        {
            foreach (var req in signupRequests)
            {
                inMemorySignupRequestRepository.Seed(req);
            }
        }
    }

    private static IReadOnlyList<TenantAggregate> BuildSeedTenants(ActorId actor)
    {
        // ── 0. Internal Admin Tenant (break-glass) ──────────────────────────────
        // ADR-0071 / FS-26: el Admin Root (propietario de gestión) es EVOLITH, no este
        // tenant sintético. INTERNAL_ADMIN queda como cuenta de emergencia degradada:
        // NO es propietario de gestión (isManagementOwner: false).
        var internalAdminTenantResult = TenantAggregate.Create(
            Code.Create(CoreDevDataSeeder.InternalAdminTenantCode),
            Name.Create(CoreDevDataSeeder.InternalAdminTenantName),
            OrganizationType.INTERNAL,
            actor,
            IdpStrategy.InternalBcrypt,
            null,
            null,
            TenantId.Load(Guid.Parse(CoreDevDataSeeder.InternalAdminTenantId)),
            isManagementOwner: false);

        if (internalAdminTenantResult.IsFailure)
        {
            throw new InvalidOperationException($"Unable to build internal admin tenant: {internalAdminTenantResult.Error}");
        }
        var internalAdminTenant = internalAdminTenantResult.Value;

        // ── Commercial Tenants ───────────────────────────────────────────────────
        var ransa = BuildTenant(Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "RANSA_PERU", "Ransa Comercial S.A.", "RUC-20101024645", OrganizationType.INTERNAL, null, false, actor,
            [
                ("RANSA_CALLAO_HQ", "Sede Principal Callao"),
                ("RANSA_PAITA", "Sucursal Paita"),
                ("RANSA_PIURA", "Sucursal Piura"),
                ("RANSA_AREQUIPA", "Sucursal Arequipa"),
                ("RANSA_TRUJILLO", "Sucursal Trujillo")
            ]);

        var neptunia = BuildTenant(Guid.Parse("c9b736b4-6a84-48f8-b34d-176bc5a6d542"), "NEPTUNIA", "Neptunia S.A. — Callao", "RUC-20330791684", OrganizationType.CLIENT, null, false, actor,
            [("NEP_CALLAO_DP1", "Depósito Portuario 1 — Av. Néstor Gambetta"), ("NEP_CALLAO_DP2", "Depósito Portuario 2 — Zona Industrial")]);

        var apm = BuildTenant(Guid.Parse("a3f5b9d2-7c3d-4c8e-a9b0-123456789abc"), "APM_CALLAO", "APM Terminals Callao S.A.", "RUC-20516357498", OrganizationType.CLIENT, null, false, actor,
            [("APM_MUELLE_N", "Muelle Norte — Terminal Contenedores")]);

        var paita = BuildTenant(Guid.Parse("9e8d7c6b-5a4f-3e2d-1c0b-9876543210fe"), "PAITA_PORT", "Terminal Portuario de Paita S.A.", "RUC-20512180098", OrganizationType.CLIENT, null, false, actor,
            [("PAITA_MUELLE", "Muelle de Transferencia — Puerto Paita"), ("PAITA_ALMACEN", "Almacén General Paita")]);

        // FS-25: EVOLITH operator reconciled to its real RUC (20100412447) and its two
        // real branches (Operaciones Callao, Sucursal Paita). Same stable tenant GUID.
        // ADR-0071 / FS-26: EVOLITH es el Tenant Raíz / Admin Root del ecosistema —
        // OrganizationType.INTERNAL + único propietario de gestión (isManagementOwner: true).
        // De ese flag deriva el login (AuthEndpoints) el privilegio transversal is_internal_admin.
        var evolith = BuildTenant(Guid.Parse(CoreDevDataSeeder.EvolithTenantId), "EVOLITH", "Evolith UMS", CoreDevDataSeeder.EvolithRuc, OrganizationType.INTERNAL, null, true, actor,
            [("UNI_CALLAO", "Operaciones Callao"), ("UNI_PAITA", "Sucursal Paita")]);

        var intradevco = BuildTenant(Guid.Parse("f3e2d1c0-b9a8-7f6e-5d4c-321098765432"), "INTRADEVCO", "Intradevco Industrial S.A.", "RUC-20101041268", OrganizationType.SUPPLIER, null, false, actor,
            [("INTRA_SJL", "Planta San Juan de Lurigancho"), ("INTRA_ATE", "Almacén Ate Vitarte — Carretera Central")]);

        // FS-25 §4.1: EVOLITH client companies (importer/exporter), one branch each.
        var comexAndina = BuildTenant(Guid.Parse(CoreDevDataSeeder.ComexAndinaTenantId), "COMEX_ANDINA", "Comercializadora Andina S.A.C.", "RUC-20512345671", OrganizationType.CLIENT, null, false, actor,
            [("CAND_LIMA", "Almacén Lima")]);

        var agronorte = BuildTenant(Guid.Parse(CoreDevDataSeeder.AgronorteTenantId), "AGRONORTE", "Agroexportadora del Norte S.A.C.", "RUC-20484123456", OrganizationType.CLIENT, null, false, actor,
            [("AGRN_PAITA", "Planta Paita")]);

        var frupiura = BuildTenant(Guid.Parse(CoreDevDataSeeder.FrupiuraTenantId), "FRUPIURA", "Frutícola Piura S.A.C.", "RUC-20526098765", OrganizationType.CLIENT, null, false, actor,
            [("FRPI_PAITA", "Packing Paita")]);

        // "Cliente de mi cliente": CLIENT tenant HIJO de COMEX_ANDINA (ParentTenantId), una sucursal.
        var impoAndinaSub = BuildTenant(Guid.Parse(CoreDevDataSeeder.ImpoAndinaSubTenantId), CoreDevDataSeeder.ImpoAndinaSubTenantCode, CoreDevDataSeeder.ImpoAndinaSubTenantName, "RUC-20609988771", OrganizationType.CLIENT, Guid.Parse(CoreDevDataSeeder.ComexAndinaTenantId), false, actor,
            [(CoreDevDataSeeder.ImpoAndinaSubBranchCode, CoreDevDataSeeder.ImpoAndinaSubBranchName)]);

        return [internalAdminTenant, ransa, neptunia, apm, paita, evolith, intradevco, comexAndina, agronorte, frupiura, impoAndinaSub];
    }

    private static TenantAggregate BuildTenant(
        Guid id,
        string code,
        string name,
        string companyRef,
        OrganizationType type,
        Guid? parentId,
        bool isManagementOwner,
        ActorId actor,
        (string Code, string Name)[] branches)
    {
        var companyReference = CompanyReference.Create(companyRef);
        var parentTenantId = parentId.HasValue ? TenantId.Load(parentId.Value) : null;

        var result = TenantAggregate.Create(
            Code.Create(code),
            Name.Create(name),
            type,
            actor,
            IdpStrategy.InternalBcrypt,
            companyReference,
            parentTenantId,
            TenantId.Load(id),
            isManagementOwner);

        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Unable to build dev tenant seed {code}: {result.Error}");
        }

        var tenant = result.Value;

        foreach (var (branchCode, branchName) in branches)
        {
            tenant.AddBranch(
                Code.Create(branchCode),
                Name.Create(branchName),
                actor);
        }

        if (code == "RANSA_PERU")
        {
            // IDP registered but NOT activated — dev mode uses InternalBcrypt (local password login).
            // Activate in production/staging when Azure AD SSO is configured.
            tenant.RegisterIdentityProvider(Code.Create("ENTRA_ID"), Name.Create("Azure AD Corporativo"), Description.Create("Directorio principal Ransa"), IdpStrategy.AzureAd, actor);
        }
        else if (code == "NEPTUNIA")
        {
            tenant.RegisterIdentityProvider(Code.Create("OKTA_CORP"), Name.Create("Okta Neptunia"), Description.Create("Directorio subsidiarias"), IdpStrategy.Okta, actor);
        }
        else if (code == "PAITA_PORT")
        {
            tenant.RegisterIdentityProvider(Code.Create("PAITA_IDP"), Name.Create("Paita Auth0"), Description.Create("Proveedor de identidad principal puerto"), IdpStrategy.Auth0, actor);
        }
        else if (code == "INTRADEVCO")
        {
            tenant.RegisterIdentityProvider(Code.Create("INTRA_SAML"), Name.Create("Intradevco AD FS"), Description.Create("Servicio federado Intradevco"), IdpStrategy.Saml2, actor);
        }

        return tenant;
    }

    private static BranchId? FindBranchId(IReadOnlyList<TenantAggregate> tenants, string tenantCode, string branchCode)
    {
        var tenant = tenants.FirstOrDefault(t => t.Code.GetValue() == tenantCode);
        var branch = tenant?.Branches.FirstOrDefault(b => b.Code.GetValue() == branchCode);
        return branch?.GetId();
    }

    private static IReadOnlyList<UserAccountAggregate> BuildSeedUserAccounts(
        ActorId actor,
        IPasswordHashingService? passwordHasher,
        BranchId? evolithCallaoBranchId = null,
        BranchId? evolithPaitaBranchId = null,
        BranchId? impoAndinaSubBranchId = null)
    {
        var internalAdminTenantId = TenantId.Load(Guid.Parse(CoreDevDataSeeder.InternalAdminTenantId));
        var ransaTenantId = TenantId.Load(Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"));
        var neptuniaTenantId = TenantId.Load(Guid.Parse("c9b736b4-6a84-48f8-b34d-176bc5a6d542"));
        var apmTenantId = TenantId.Load(Guid.Parse("a3f5b9d2-7c3d-4c8e-a9b0-123456789abc"));
        var paitaTenantId = TenantId.Load(Guid.Parse("9e8d7c6b-5a4f-3e2d-1c0b-9876543210fe"));
        var evolithTenantId = TenantId.Load(Guid.Parse("5f4e3d2c-1b0a-9f8e-7d6c-543210987654"));
        var intradevcoTenantId = TenantId.Load(Guid.Parse("f3e2d1c0-b9a8-7f6e-5d4c-321098765432"));

        var result = new List<UserAccountAggregate>();

        // ── 0. SuperAdmin User (Internal Admin Tenant) ─────────────────────────
        var superAdminResult = UserAccountAggregate.Create(
            internalAdminTenantId,
            Email.Create("admin@ums.local"),
            UserCategory.Internal,
            null,
            null,
            actor,
            userAccountId: UserAccountId.Load(Guid.Parse(CoreDevDataSeeder.SuperAdminUserId)));

        if (superAdminResult.IsSuccess)
        {
            var superAdmin = superAdminResult.Value;
            superAdmin.Activate(actor);
            // Add password
            if (passwordHasher != null)
            {
                var hash = PasswordHash.Create(passwordHasher.Hash(CoreDevDataSeeder.SuperAdminPassword));
                var passwordResult = superAdmin.AddPassword(hash, actor);
                if (passwordResult.IsFailure)
                {
                    throw new InvalidOperationException($"Unable to seed super admin password: {passwordResult.Error}");
                }
            }
            SeedVerifiedMfa(superAdmin, actor);
            result.Add(superAdmin);
        }

        // ── Internal Admin Inbox Demo User ────────────────────────────────────
        var internalAdminInboxUser = BuildUserAccount(
            Guid.Parse(CoreDevDataSeeder.InternalAdminPendingUserId),
            internalAdminTenantId,
            "bandeja.admin@ums.local",
            UserCategory.External,
            actor,
            "EXT-201");
        result.Add(internalAdminInboxUser);

        // ── Commercial Tenant Users ─────────────────────────────────────────────
        result.AddRange(BuildSeedUserAccountsForTenant(ransaTenantId, actor, passwordHasher));
        result.AddRange(BuildSeedUserAccountsForTenant(neptuniaTenantId, actor, passwordHasher));
        result.AddRange(BuildSeedUserAccountsForTenant(apmTenantId, actor, passwordHasher));
        result.AddRange(BuildSeedUserAccountsForTenant(paitaTenantId, actor, passwordHasher));
        result.AddRange(BuildSeedUserAccountsForTenant(intradevcoTenantId, actor, passwordHasher));

        // ── FS-25: EVOLITH operator internal users (by branch) and client users ──
        result.AddRange(BuildEvolithUserAccounts(evolithTenantId, actor, passwordHasher, evolithCallaoBranchId, evolithPaitaBranchId));

        // EVOLITH ROOT ADMIN — transversal operator admin, no branch (distinct from admin.callao).
        result.Add(BuildActiveLocalUser(
            evolithTenantId, CoreDevDataSeeder.EvolithRootAdminUserIndex,
            CoreDevDataSeeder.EffectiveEvolithRootAdminEmail, actor, passwordHasher));

        result.Add(BuildActiveLocalUser(
            TenantId.Load(Guid.Parse(CoreDevDataSeeder.ComexAndinaTenantId)), 1,
            "usuario.impo@comexandina.com.pe", actor, passwordHasher));
        result.Add(BuildActiveLocalUser(
            TenantId.Load(Guid.Parse(CoreDevDataSeeder.AgronorteTenantId)), 1,
            "usuario.expo@agronorte.com.pe", actor, passwordHasher));

        // "Cliente de mi cliente": single external user of the sub-client tenant, on its branch.
        result.Add(BuildActiveLocalUser(
            TenantId.Load(Guid.Parse(CoreDevDataSeeder.ImpoAndinaSubTenantId)), 1,
            CoreDevDataSeeder.ImpoAndinaSubUserEmail, actor, passwordHasher, impoAndinaSubBranchId));

        return result;
    }

    // FS-25 §4.4: the 12 EVOLITH internal users (Callao + Paita). Indices match the
    // profile→user mapping in AuthorizationDevDataSeeder (byte[0] = index). Each user is
    // associated to its branch (Callao 1-8, Paita 9-12) via BranchId.
    private static IReadOnlyList<UserAccountAggregate> BuildEvolithUserAccounts(
        TenantId evolithTenantId,
        ActorId actor,
        IPasswordHashingService? passwordHasher,
        BranchId? callaoBranchId,
        BranchId? paitaBranchId)
    {
        var users = new (byte Index, string Email, BranchId? BranchId)[]
        {
            (1, "admin.callao@evolith.example", callaoBranchId),
            (2, "agente.aduanas.callao@evolith.example", callaoBranchId),
            (3, "despachador.callao@evolith.example", callaoBranchId),
            (4, "jefe.almacen.callao@evolith.example", callaoBranchId),
            (5, "coordinador.transporte.callao@evolith.example", callaoBranchId),
            (6, "ejecutivo.cuenta.callao@evolith.example", callaoBranchId),
            (7, "analista.doc.callao@evolith.example", callaoBranchId),
            (8, "auditor.callao@evolith.example", callaoBranchId),
            (9, "jefe.almacen.paita@evolith.example", paitaBranchId),
            (10, "operario.almacen.paita@evolith.example", paitaBranchId),
            (11, "agente.aduanas.paita@evolith.example", paitaBranchId),
            (12, "ejecutivo.cuenta.paita@evolith.example", paitaBranchId),
        };

        return users
            .Select(u => BuildActiveLocalUser(evolithTenantId, u.Index, u.Email, actor, passwordHasher, u.BranchId))
            .ToList();
    }

    // Creates an Active user with the uniform dev password. No IdentityReference so the
    // federated-user invariant does not block the local password login (FS-25 §4.4).
    private static UserAccountAggregate BuildActiveLocalUser(TenantId tenantId, byte index, string email, ActorId actor, IPasswordHashingService? passwordHasher, BranchId? branchId = null)
    {
        var baseBytes = tenantId.GetValue().ToByteArray();
        baseBytes[0] = index;
        var userId = UserAccountId.Load(new Guid(baseBytes));

        var result = UserAccountAggregate.Create(
            tenantId, Email.Create(email),
            UserCategory.Internal, null, null, actor,
            branchId: branchId,
            userAccountId: userId);

        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Unable to build EVOLITH dev user seed {email}: {result.Error}");
        }

        var user = result.Value;
        user.Activate(actor);
        if (passwordHasher != null)
        {
            var hash = PasswordHash.Create(passwordHasher.Hash(CoreDevDataSeeder.EffectiveEvolithDevPassword));
            user.AddPassword(hash, actor);
        }

        return user;
    }

    private static IReadOnlyList<UserAccountAggregate> BuildSeedUserAccountsForTenant(TenantId tenantId, ActorId actor, IPasswordHashingService? passwordHasher = null)
    {
        var baseGuidBytes = tenantId.GetValue().ToByteArray();
        Guid DeriveGuid(byte index)
        {
            var bytes = (byte[])baseGuidBytes.Clone();
            bytes[0] = index;
            return new Guid(bytes);
        }

        var tenantStr = tenantId.GetValue().ToString();
        var domain = tenantStr switch
        {
            _ when tenantStr.StartsWith("3fa8") => "ransa.pe",
            _ when tenantStr.StartsWith("c9b7") => "neptunia.pe",
            _ when tenantStr.StartsWith("a3f5") => "apmterminals.com",
            _ when tenantStr.StartsWith("9e8d") => "tpp-paita.com.pe",
            _ when tenantStr.StartsWith("5f4e") => "evolith.example",
            _ when tenantStr.StartsWith("f3e2") => "intradevco.com.pe",
            _ => "logistics.pe"
        };

        // Admin uses local password — no IdentityReference so the federated-user invariant doesn't block AddPassword
        var adminResult = UserAccountAggregate.Create(
            tenantId, Email.Create($"gerente.operaciones@{domain}"),
            UserCategory.Internal, null, null, actor,
            userAccountId: UserAccountId.Load(DeriveGuid(1)));
        var admin = adminResult.Value;
        admin.Activate(actor);
        if (passwordHasher != null)
        {
            var hash = PasswordHash.Create(passwordHasher.Hash(CoreDevDataSeeder.SuperAdminPassword));
            admin.AddPassword(hash, actor);
        }
        SeedVerifiedMfa(admin, actor);

        var analyst = BuildUserAccount(DeriveGuid(2), tenantId, $"analista.inventario@{domain}", UserCategory.Internal, actor, "EMP-002");
        analyst.Activate(actor);

        var pending = BuildUserAccount(DeriveGuid(3), tenantId, $"coordinador.flota@{domain}", UserCategory.External, actor, "EXT-101");

        var blocked = BuildUserAccount(DeriveGuid(4), tenantId, $"ex.empleado@{domain}", UserCategory.External, actor, "EXT-102");
        blocked.Activate(actor);
        blocked.Block(Reason.Create("Desvinculación laboral"), actor);

        var partner = BuildUserAccount(DeriveGuid(5), tenantId, $"auditor.externo@aduanas.gob.pe", UserCategory.Partner, actor, "DNI-44556677");
        partner.Activate(actor);

        var supervisor = BuildUserAccount(DeriveGuid(6), tenantId, $"supervisor.planta@{domain}", UserCategory.Internal, actor, "EMP-006");
        supervisor.Activate(actor);

        var operator1 = BuildUserAccount(DeriveGuid(7), tenantId, $"operador.almacen@{domain}", UserCategory.Internal, actor, "EMP-007");
        operator1.Activate(actor);

        var operator2 = BuildUserAccount(DeriveGuid(8), tenantId, $"operador.despacho@{domain}", UserCategory.Internal, actor, "EMP-008");
        operator2.Activate(actor);

        var inspector = BuildUserAccount(DeriveGuid(9), tenantId, $"inspector.calidad@{domain}", UserCategory.Internal, actor, "EMP-009");
        inspector.Activate(actor);

        var manager = BuildUserAccount(DeriveGuid(10), tenantId, $"jefe.logistica@{domain}", UserCategory.Internal, actor, "EMP-010");
        manager.Activate(actor);

        var auditor = BuildUserAccount(DeriveGuid(11), tenantId, $"auditor.cumplimiento@{domain}", UserCategory.Internal, actor, "EMP-011");
        auditor.Activate(actor);

        var readonly_user = BuildUserAccount(DeriveGuid(12), tenantId, $"consulta.solo@{domain}", UserCategory.Internal, actor, "EMP-012");
        readonly_user.Activate(actor);

        return [admin, analyst, pending, blocked, partner, supervisor, operator1, operator2, inspector, manager, auditor, readonly_user];
    }

    private static UserAccountAggregate BuildUserAccount(
        Guid id,
        TenantId tenantId,
        string email,
        UserCategory category,
        ActorId actor,
        string identityReferenceStr)
    {
        var emailVo = Email.Create(email);
        var identityReference = IdentityReference.Create(identityReferenceStr);
        var identityReferenceType = IdentityReferenceType.HrId;

        var result = UserAccountAggregate.Create(
            tenantId,
            emailVo,
            category,
            identityReference,
            identityReferenceType,
            actor,
            userAccountId: UserAccountId.Load(id));

        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Unable to build dev user account seed: {result.Error}");
        }

        return result.Value;
    }

    private static async Task ReconcileEvolithTenantAsync(ITenantRepository tenantRepository, ActorId actor, CancellationToken cancellationToken)
    {
        var evolith = await tenantRepository.GetByIdAsync(Guid.Parse(CoreDevDataSeeder.EvolithTenantId), cancellationToken);
        if (evolith is null)
        {
            return;
        }

        var changed = false;

        // Correct the RUC (CompanyReference) if a legacy snapshot carries a wrong value.
        if (evolith.CompanyReference?.GetValue() != CoreDevDataSeeder.EvolithRuc)
        {
            SetCompanyReference(evolith, CompanyReference.Create(CoreDevDataSeeder.EvolithRuc));
            changed = true;
        }

        // Ensure the two real branches exist (Callao + Paita).
        var required = new[] { ("UNI_CALLAO", "Operaciones Callao"), ("UNI_PAITA", "Sucursal Paita") };
        foreach (var (code, name) in required)
        {
            if (evolith.Branches.All(b => b.Code.GetValue() != code))
            {
                evolith.AddBranch(Code.Create(code), Name.Create(name), actor);
                changed = true;
            }
        }

        // Cierra cualquier sucursal ajena al par real (Flujo B) — incluida la instantánea heredada
        // UNI_LIMA, hoy renombrada a UNI_CALLAO.
        //
        // ADR-0164: CIERRE, no borrado. La fila se queda y su código queda ocupado, que es lo que se
        // quiere: si UNI_LIMA vuelve a aparecer en un volcado antiguo, el alta chocará en vez de
        // crear una segunda sucursal con el mismo código. Se excluyen las ya cerradas para que el
        // sembrado siga siendo idempotente: sin ese filtro, cada ejecución reintentaría cerrarlas y
        // marcaría un cambio que no existe.
        var stale = evolith.Branches
            .Where(b => !b.IsClosed && b.Code.GetValue() != "UNI_CALLAO" && b.Code.GetValue() != "UNI_PAITA")
            .ToList();
        foreach (var branch in stale)
        {
            // Recuentos en cero: el sembrado corre sobre datos de desarrollo/UAT que él mismo
            // controla y no hay usuarios ni perfiles colgando de estas sucursales heredadas. La
            // guarda real vive en CloseBranchCommandHandler, que sí consulta la base.
            evolith.CloseBranch(branch.Props.Id, actor, reason: "Sucursal heredada fuera del par operativo real (siembra).");
            changed = true;
        }

        if (changed)
        {
            await tenantRepository.UpdateAsync(evolith, cancellationToken);
            await tenantRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security Hotspot", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields",
        Justification = "ADR-UMS-099: reflexión sancionada en seeder de desarrollo/UAT. Fija la referencia de empresa " +
                        "del tenant semilla sobre props no públicos; no se ejecuta en producción " +
                        "(SeedDevData && !IsProduction).")]
    private static void SetCompanyReference(TenantAggregate tenant, CompanyReference companyReference)
    {
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        var propsField = typeof(TenantAggregate).GetField("_props", privateInstance);
        var props = propsField?.GetValue(tenant) as TenantProps;
        if (props is null)
        {
            return;
        }

        var companyReferenceProperty = typeof(TenantProps).GetProperty(nameof(TenantProps.CompanyReference));
        companyReferenceProperty?.SetValue(props, companyReference);
    }

    private static IReadOnlyList<UserManagementDelegationAggregate> BuildSeedDelegations(ActorId actor)
    {
        var evolithTenantId = TenantId.Load(Guid.Parse("5f4e3d2c-1b0a-9f8e-7d6c-543210987654"));
        var evolithBaseBytes = evolithTenantId.GetValue().ToByteArray();
        Guid DeriveEvolithGuid(byte index)
        {
            var bytes = (byte[])evolithBaseBytes.Clone();
            bytes[0] = index;
            return new Guid(bytes);
        }

        var adminId = UserAccountId.Load(DeriveEvolithGuid(1));
        var analystId = UserAccountId.Load(DeriveEvolithGuid(2));
        var partnerId = UserAccountId.Load(DeriveEvolithGuid(5));

        // 1. Active Delegation
        var activeDel = UserManagementDelegationAggregate.Create(
            evolithTenantId, adminId, analystId, DelegationScopeType.Tenant, null,
            new[] { DelegatedAction.CreateUser, DelegatedAction.BlockUser },
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30), 90, false, actor).Value;
        activeDel.Activate(actor);

        // 2. Expired Delegation
        var expiredDel = UserManagementDelegationAggregate.Create(
            evolithTenantId, adminId, partnerId, DelegationScopeType.Tenant, null,
            new[] { DelegatedAction.CreateUser },
            DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow.AddDays(-1), 90, false, actor).Value;
        expiredDel.Activate(actor);
        expiredDel.Expire(actor);

        // 3. Revoked Delegation
        var revokedDel = UserManagementDelegationAggregate.Create(
            evolithTenantId, analystId, adminId, DelegationScopeType.Tenant, null,
            new[] { DelegatedAction.BlockUser },
            DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(20), 90, false, actor).Value;
        revokedDel.Activate(actor);
        revokedDel.Revoke("Cambio de rol", actor);

        // 4. Draft / Pending Approval
        var draftDel = UserManagementDelegationAggregate.Create(
            evolithTenantId, partnerId, analystId, DelegationScopeType.Tenant, null,
            new[] { DelegatedAction.CreateUser },
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(15), 90, true, actor).Value;
        // Not activated

        return [activeDel, expiredDel, revokedDel, draftDel];
    }

    private static IReadOnlyList<TenantSignupRequestAggregate> BuildSeedTenantSignupRequests(ActorId actor)
    {
        var results = new List<TenantSignupRequestAggregate>();

        // 1. Pending — shows in global onboarding inbox
        var techstart = TenantSignupRequestAggregate.Create(
            Name.Create("TechStart SRL"),
            CompanyReference.Create("RUC-20601234567"),
            Name.Create("Carlos Mendoza"),
            Email.Create("cmendoza@techstart.pe"),
            actor);
        if (techstart.IsSuccess) results.Add(techstart.Value);

        // 2. Pending — shows in global onboarding inbox
        var globalPartners = TenantSignupRequestAggregate.Create(
            Name.Create("Global Partners SA"),
            CompanyReference.Create("RUC-20507654321"),
            Name.Create("Ana Flores"),
            Email.Create("aflores@globalpartners.com.pe"),
            actor);
        if (globalPartners.IsSuccess) results.Add(globalPartners.Value);

        // 3. Approved — historical record (already processed)
        var acmeCorp = TenantSignupRequestAggregate.Create(
            Name.Create("Acme Corp Peru SAC"),
            CompanyReference.Create("RUC-20123456789"),
            Name.Create("Roberto Vidal"),
            Email.Create("rvidal@acmecorp.pe"),
            actor);
        if (acmeCorp.IsSuccess)
        {
            acmeCorp.Value.Approve(TenantId.Load(Guid.Parse(CoreDevDataSeeder.RansaTenantId)), actor);
            results.Add(acmeCorp.Value);
        }

        return results;
    }

    private static async Task UpsertUserAccountAsync(
        IUserAccountRepository userAccountRepository,
        UserAccountAggregate seedUserAccount,
        ActorId actor,
        CancellationToken cancellationToken)
    {
        var tenantId = seedUserAccount.Props.TenantId.GetValue();
        var existing = await userAccountRepository.GetByTenantAndEmailAsync(
            tenantId,
            seedUserAccount.Email,
            includeDeleted: true,
            cancellationToken);

        if (existing is null)
        {
            await userAccountRepository.AddAsync(seedUserAccount, cancellationToken);
            return;
        }

        // Dev snapshots can keep a user with the right email but a stale GUID.
        // Profiles reference the deterministic seed GUIDs, so we normalize by
        // replacing the old row when the identifiers do not match.
        if (existing.GetId().GetValue() != seedUserAccount.GetId().GetValue())
        {
            await userAccountRepository.SoftDeleteAsync(existing.GetId().GetValue(), actor.GetValue(), cancellationToken);
            await userAccountRepository.AddAsync(seedUserAccount, cancellationToken);
            return;
        }

        await SyncLocalPasswordAsync(existing, seedUserAccount, actor);
        await SyncVerifiedMfaAsync(existing, seedUserAccount, actor);
        await userAccountRepository.UpdateAsync(existing, cancellationToken);
    }

    private static async Task UpsertUserAccountAsync(
        InMemoryUserAccountRepository userAccountRepository,
        UserAccountAggregate seedUserAccount,
        ActorId actor,
        CancellationToken cancellationToken)
    {
        var existing = await userAccountRepository.GetByEmailAsync(seedUserAccount.Email, cancellationToken);

        if (existing is null)
        {
            userAccountRepository.Seed(seedUserAccount);
            return;
        }

        if (existing.GetId().GetValue() != seedUserAccount.GetId().GetValue())
        {
            await userAccountRepository.SoftDeleteAsync(existing.GetId().GetValue(), actor.GetValue(), cancellationToken);
            userAccountRepository.Seed(seedUserAccount);
            return;
        }

        await SyncLocalPasswordAsync(existing, seedUserAccount, actor);
        await SyncVerifiedMfaAsync(existing, seedUserAccount, actor);
        await userAccountRepository.UpdateAsync(existing, cancellationToken);
    }

    private static Task SyncVerifiedMfaAsync(
        UserAccountAggregate existingUserAccount,
        UserAccountAggregate seedUserAccount,
        ActorId actor)
    {
        foreach (var seedEnrollment in seedUserAccount.MfaEnrollments.Where(e => e.Status == MfaEnrollmentStatus.Verified))
        {
            var existingEnrollment = existingUserAccount.MfaEnrollments.FirstOrDefault(e => e.Method == seedEnrollment.Method);

            if (existingEnrollment is null)
            {
                var enrollResult = existingUserAccount.EnrollMfa(seedEnrollment.Method, actor);
                if (enrollResult.IsFailure)
                {
                    throw new InvalidOperationException(
                        $"Unable to sync MFA enrollment for {seedUserAccount.Email.GetValue()}: {enrollResult.Error}");
                }

                existingEnrollment = existingUserAccount.MfaEnrollments.LastOrDefault(e => e.Method == seedEnrollment.Method);
            }

            if (existingEnrollment is not null && existingEnrollment.Status != MfaEnrollmentStatus.Verified)
            {
                var verifyResult = existingEnrollment.Verify(actor);
                if (verifyResult.IsFailure)
                {
                    throw new InvalidOperationException(
                        $"Unable to verify MFA enrollment for {seedUserAccount.Email.GetValue()}: {verifyResult.Error}");
                }
            }
        }

        return Task.CompletedTask;
    }

    private static Task SyncLocalPasswordAsync(
        UserAccountAggregate existingUserAccount,
        UserAccountAggregate seedUserAccount,
        ActorId actor)
    {
        if (seedUserAccount.Status == UserStatus.Active)
        {
            if (existingUserAccount.Status == UserStatus.Pending)
            {
                var activateResult = existingUserAccount.Activate(actor);
                if (activateResult.IsFailure)
                {
                    throw new InvalidOperationException(
                        $"Unable to activate dev user account {seedUserAccount.Email.GetValue()}: {activateResult.Error}");
                }
            }
            else if (existingUserAccount.Status == UserStatus.Blocked)
            {
                var restoreResult = existingUserAccount.Restore(actor);
                if (restoreResult.IsFailure)
                {
                    throw new InvalidOperationException(
                        $"Unable to restore dev user account {seedUserAccount.Email.GetValue()}: {restoreResult.Error}");
                }
            }
        }

        if (seedUserAccount.ActivePasswordHash is not null)
        {
            var passwordResult = existingUserAccount.AddPassword(seedUserAccount.ActivePasswordHash, actor);
            if (passwordResult.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Unable to sync dev password for {seedUserAccount.Email.GetValue()}: {passwordResult.Error}");
            }
        }

        return Task.CompletedTask;
    }

    private static void SeedVerifiedMfa(UserAccountAggregate userAccount, ActorId actor)
    {
        var enrollResult = userAccount.EnrollMfa(MfaMethod.Totp, actor);
        if (enrollResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"Unable to seed MFA enrollment for {userAccount.Email.GetValue()}: {enrollResult.Error}");
        }

        var enrollment = userAccount.MfaEnrollments.LastOrDefault(e => e.Method == MfaMethod.Totp);
        if (enrollment is null)
        {
            throw new InvalidOperationException(
                $"Unable to locate seeded MFA enrollment for {userAccount.Email.GetValue()}.");
        }

        var verifyResult = enrollment.Verify(actor);
        if (verifyResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"Unable to verify seeded MFA enrollment for {userAccount.Email.GetValue()}: {verifyResult.Error}");
        }
    }
}
