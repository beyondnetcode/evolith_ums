export const authorizationTranslations = {
  es: {
    // Context labels
    authorizationContext: 'Autorización',
    systemSuites: 'Suites del Sistema',
    systemSuitesNav: 'Sistemas',
    permissionTemplates: 'Plantillas de Permisos',
    profilesHeader: 'Perfiles de Autorización',
    observabilityGrafana: 'Grafana (métricas y trazas)',
    observabilityLogs: 'Logs (Loki)',
    permissionTemplateMaintenance: 'Plantillas de Permisos',
    permissionTemplateMaintenanceSubtitle:
      'Configure plantillas de permisos por rol y suite. Asigne efectos (Permitir/Denegar/Neutro) a cada recurso.',

    // Template list
    templateDraft: 'Borrador',
    templatePublished: 'Publicada',
    templateDeprecated: 'Descontinuada',
    selectTemplateToView: 'Seleccione una plantilla para ver sus detalles.',
    noPermissionTemplates: 'Sin plantillas de permisos',
    noPermissionTemplatesMsg: 'Cree la primera plantilla para este contexto de autorización.',

    // Template form
    createTemplateTitle: 'Nueva Plantilla de Permisos',
    createTemplateDraftNote:
      'La plantilla se crea en estado Borrador. Agrega los ítems de permiso antes de publicarla.',

    // Template detail
    templateGeneral: 'General',
    templateItems: 'Permisos',
    templatePublishBtn: 'Publicar',
    templateDeprecateBtn: 'Descontinuar',
    templateItemsEmpty: 'Sin ítems de permiso.',
    templateAddItem: 'Agregar ítem de permiso',

    // Effects
    effectAllow: 'Permitir',
    effectDeny: 'Denegar',
    effectNeutral: 'Neutro',
    systemSuiteMaintenance: 'Mantenimiento de Suites del Sistema',
    systemSuiteMaintenanceSubtitle:
      'Explore y gestione los suites del sistema, sus módulos y catálogos de acciones.',

    // System Suite Form
    createSystemSuiteTitle: 'Crear Nuevo Suite del Sistema',
    systemSuiteCode: 'Código del Suite',
    systemSuiteCodeHelper: 'Solo caracteres alfanuméricos en mayúsculas y guiones bajos.',
    systemSuiteName: 'Nombre del Suite',
    systemSuiteNameHelper: 'Nombre descriptivo del suite del sistema.',
    registerSystemSuiteBtn: 'Registrar Suite',

    // System Suite list / controls
    bySystemSuiteId: 'Por ID del Suite (GUID)',
    noSystemSuitesFound: 'No se encontraron suites del sistema con el criterio seleccionado.',
    noSystemSuitesTitle: 'Sin suites del sistema',
    selectSystemSuiteToView:
      'Seleccione un suite del sistema en el panel para cargar sus detalles.',
    systemSuiteDetails: 'Detalles del Suite',

    // Tabs
    modules: 'Módulos',
    domainResources: 'Recursos de Dominio',
    systemActions: 'Acciones de Sistema',
    actions: 'Acciones',
    roles: 'Roles',
    noModulesConfigured: 'No hay módulos configurados para este suite del sistema.',
    noDomainResourcesConfigured:
      'No hay recursos de dominio configurados para este suite del sistema.',
    noActionsConfigured: 'No hay acciones configuradas para este suite del sistema.',
    noRolesConfigured: 'No hay roles configurados para este suite del sistema.',

    // Permission tree
    inheritedPermission: 'Heredado',
    directPermission: 'Directo',
    partialPermission: 'Parcial',
    noPermission: 'Sin configurar',
    permissionLegend: 'Leyenda de permisos',
    inheritedFromParent: 'Permiso heredado del padre',

    // CRUD operations
    crudCreate: 'Crear',
    crudRead: 'Leer/Obtener',
    crudSearch: 'Buscar/Listar',
    crudUpdate: 'Actualizar',
    crudDelete: 'Eliminar/Desactivar',

    // Domain resources
    aggregate: 'Agregado',
    entity: 'Entidad',
    crudOperations: 'Operaciones CRUD',
    customActions: 'Acciones personalizadas',
    parentResource: 'Recurso padre',
    addRole: 'Agregar rol',
    newRole: 'Nuevo rol',
    saveRole: 'Guardar rol',
    roleCode: 'Código',
    roleValue: 'Nombre visible',
    parentRole: 'Rol padre',
    rootRole: 'Rol raíz',
    roleLevel: 'Nivel',
    promotionOrder: 'Orden de promoción',
    roleCodeAndValueRequired: 'Código y nombre visible son requeridos.',
    loading: 'Cargando...',
    inactive: 'Inactivo',
    noDescription: 'Sin descripción',
    edit: 'Editar',
    activate: 'Activar',

    // Status controls
    maintenanceBtn: 'Mantenimiento',
    deprecateBtn: 'Descontinuar',
    maintenance: 'Mantenimiento',
    deprecated: 'Descontinuado',

    // Notifications
    notifSystemSuiteCreated: 'Suite del Sistema Creado',
    notifSystemSuiteCreatedMsg: (id: string) => `Suite del sistema registrado con ID: ${id}.`,
    notifSystemSuiteCreateFailed: 'Error al Crear Suite del Sistema',
    notifSystemSuiteUpdated: 'Suite del Sistema Actualizado',
    notifSystemSuiteUpdatedMsg: (name: string) =>
      `Los datos del suite '${name}' fueron actualizados.`,
    notifStatusChanged: 'Estado Cambiado',
    notifStatusChangedMsg: 'El estado del suite del sistema fue actualizado.',
    notifStatusChangeFailed: 'Error al Cambiar Estado',
    notifStatusChangeFailedMsg: 'No se pudo actualizar el estado del suite del sistema.',
    notifStatusSetTo: (status: string) => `Estado del suite actualizado a ${status}.`,
    notifRoleCreated: 'Rol registrado',
    notifRoleCreatedMsg: 'El rol fue registrado correctamente.',
    notifRoleCreateFailed: 'Error al registrar rol',
    notifRoleCreateFailedMsg:
      'No se pudo registrar el rol. Revise el código y la jerarquía seleccionada.',
    notifRoleUpdated: 'Rol actualizado',
    notifRoleUpdatedMsg: 'Los datos del rol fueron actualizados.',
    notifRoleUpdateFailed: 'Error al actualizar rol',
    notifRoleUpdateFailedMsg: 'No se pudo actualizar el rol. Revise la jerarquía seleccionada.',
    notifRoleStatusChanged: 'Estado del rol actualizado',
    notifRoleStatusChangedMsg: 'El estado del rol fue actualizado correctamente.',
    notifRoleStatusFailed: 'Error al cambiar estado del rol',
    notifRoleStatusFailedMsg: 'No se pudo cambiar el estado del rol.',

    // Feature Flags
    featureFlags: 'Feature Flags',
    featureFlagMaintenance: 'Feature Flags',
    featureFlagMaintenanceSubtitle:
      'Gestione feature flags para controlar funcionalidades por tenant, rol, ambiente y más.',
    flagCode: 'Código del Flag',
    flagType: 'Tipo de Flag',
    flagStatus: 'Estado',
    flagTargets: 'Targeting Rules',
    rolloutPercentage: 'Rollout %',
    linkedResource: 'Recurso Vinculado',
    criteriaCount: 'Criterios',
    criteriaType: 'Tipo de Criterio',
    criteriaOperator: 'Operador',
    criteriaValue: 'Valor',
    addCriteria: 'Agregar Criterio',
    noFeatureFlags: 'Sin feature flags',
    noFeatureFlagsMsg: 'Cree el primer feature flag para controlar funcionalidades.',
    activateFlag: 'Activar',
    deactivateFlag: 'Desactivar',
    archiveFlag: 'Archivar',
    flagInactive: 'Inactivo',
    flagActive: 'Activo',
    flagArchived: 'Archivado',
    createFlagTitle: 'Nuevo Feature Flag',
    createFlagDraftNote:
      'El flag se crea en estado Inactivo. Active después de configurar targeting y criterios.',
  },
  en: {
    // Context labels
    authorizationContext: 'Authorization',
    systemSuites: 'System Suites',
    systemSuitesNav: 'Systems',
    permissionTemplates: 'Permission Templates',
    profilesHeader: 'Authorization Profiles',
    observabilityGrafana: 'Grafana (metrics & traces)',
    observabilityLogs: 'Logs (Loki)',
    permissionTemplateMaintenance: 'Permission Templates',
    permissionTemplateMaintenanceSubtitle:
      'Configure permission templates by role and system suite. Assign Allow/Deny/Neutral effects per resource.',

    // Template list
    templateDraft: 'Draft',
    templatePublished: 'Published',
    templateDeprecated: 'Deprecated',
    selectTemplateToView: 'Select a template to view its details.',
    noPermissionTemplates: 'No permission templates',
    noPermissionTemplatesMsg: 'Create the first template for this authorization context.',

    // Template form
    createTemplateTitle: 'New Permission Template',
    createTemplateDraftNote:
      'The template is created in Draft state. Add permission items before publishing.',

    // Template detail
    templateGeneral: 'General',
    templateItems: 'Permissions',
    templatePublishBtn: 'Publish',
    templateDeprecateBtn: 'Deprecate',
    templateItemsEmpty: 'No permission items.',
    templateAddItem: 'Add permission item',

    // Effects
    effectAllow: 'Allow',
    effectDeny: 'Deny',
    effectNeutral: 'Neutral',
    systemSuiteMaintenance: 'System Suite Maintenance',
    systemSuiteMaintenanceSubtitle:
      'Explore and manage system suites, their modules, and action catalogs.',

    // System Suite Form
    createSystemSuiteTitle: 'Create New System Suite',
    systemSuiteCode: 'System Suite Code',
    systemSuiteCodeHelper: 'Uppercase alphanumeric characters and underscores only.',
    systemSuiteName: 'System Suite Name',
    systemSuiteNameHelper: 'Descriptive name for the system suite.',
    registerSystemSuiteBtn: 'Register System Suite',

    // System Suite list / controls
    bySystemSuiteId: 'By System Suite ID (GUID)',
    noSystemSuitesFound: 'No system suites found with the selected criteria.',
    noSystemSuitesTitle: 'No system suites',
    selectSystemSuiteToView: 'Select a system suite in the panel to load its details.',
    systemSuiteDetails: 'System Suite Details',

    // Tabs
    modules: 'Modules',
    domainResources: 'Domain Resources',
    systemActions: 'System Actions',
    actions: 'Actions',
    roles: 'Roles',
    noModulesConfigured: 'No modules configured for this system suite.',
    noDomainResourcesConfigured: 'No domain resources configured for this system suite.',
    noActionsConfigured: 'No actions configured for this system suite.',
    noRolesConfigured: 'No roles configured for this system suite.',

    // Permission tree
    inheritedPermission: 'Inherited',
    directPermission: 'Direct',
    partialPermission: 'Partial',
    noPermission: 'Not configured',
    permissionLegend: 'Permission legend',
    inheritedFromParent: 'Permission inherited from parent',

    // CRUD operations
    crudCreate: 'Create',
    crudRead: 'Read/Get',
    crudSearch: 'Search/List',
    crudUpdate: 'Update',
    crudDelete: 'Delete/Deactivate',

    // Domain resources
    aggregate: 'Aggregate',
    entity: 'Entity',
    crudOperations: 'CRUD Operations',
    customActions: 'Custom Actions',
    parentResource: 'Parent resource',
    addRole: 'Add role',
    newRole: 'New role',
    saveRole: 'Save role',
    roleCode: 'Code',
    roleValue: 'Display name',
    parentRole: 'Parent role',
    rootRole: 'Root role',
    roleLevel: 'Level',
    promotionOrder: 'Promotion order',
    roleCodeAndValueRequired: 'Code and display name are required.',
    loading: 'Loading...',
    inactive: 'Inactive',
    noDescription: 'No description',
    edit: 'Edit',
    activate: 'Activate',

    // Status controls
    maintenanceBtn: 'Maintenance',
    deprecateBtn: 'Deprecate',
    maintenance: 'Maintenance',
    deprecated: 'Deprecated',

    // Notifications
    notifSystemSuiteCreated: 'System Suite Created',
    notifSystemSuiteCreatedMsg: (id: string) => `System suite registered with ID: ${id}.`,
    notifSystemSuiteCreateFailed: 'System Suite Creation Failed',
    notifSystemSuiteUpdated: 'System Suite Updated',
    notifSystemSuiteUpdatedMsg: (name: string) =>
      `System suite '${name}' data was updated successfully.`,
    notifStatusChanged: 'Status Changed',
    notifStatusChangedMsg: 'The system suite status was successfully updated.',
    notifStatusChangeFailed: 'Status Change Failed',
    notifStatusChangeFailedMsg: 'Could not update the system suite status.',
    notifStatusSetTo: (status: string) =>
      `The system suite status was successfully set to ${status}.`,
    notifRoleCreated: 'Role registered',
    notifRoleCreatedMsg: 'The role was registered successfully.',
    notifRoleCreateFailed: 'Could not register role',
    notifRoleCreateFailedMsg:
      'The role could not be registered. Check its code and selected hierarchy.',
    notifRoleUpdated: 'Role updated',
    notifRoleUpdatedMsg: 'The role details were updated.',
    notifRoleUpdateFailed: 'Could not update role',
    notifRoleUpdateFailedMsg: 'The role could not be updated. Check the selected hierarchy.',
    notifRoleStatusChanged: 'Role status updated',
    notifRoleStatusChangedMsg: 'The role status was updated successfully.',
    notifRoleStatusFailed: 'Could not change role status',
    notifRoleStatusFailedMsg: 'The role status could not be changed.',

    // Feature Flags
    featureFlags: 'Feature Flags',
    featureFlagMaintenance: 'Feature Flags',
    featureFlagMaintenanceSubtitle:
      'Manage feature flags to control functionality by tenant, role, environment and more.',
    flagCode: 'Flag Code',
    flagType: 'Flag Type',
    flagStatus: 'Status',
    flagTargets: 'Targeting Rules',
    rolloutPercentage: 'Rollout %',
    linkedResource: 'Linked Resource',
    criteriaCount: 'Criteria',
    criteriaType: 'Criteria Type',
    criteriaOperator: 'Operator',
    criteriaValue: 'Value',
    addCriteria: 'Add Criteria',
    noFeatureFlags: 'No feature flags',
    noFeatureFlagsMsg: 'Create the first feature flag to control functionality.',
    activateFlag: 'Activate',
    deactivateFlag: 'Deactivate',
    archiveFlag: 'Archive',
    flagInactive: 'Inactive',
    flagActive: 'Active',
    flagArchived: 'Archived',
    createFlagTitle: 'New Feature Flag',
    createFlagDraftNote:
      'The flag is created in Inactive state. Activate after configuring targeting and criteria.',
  },
} as const;
