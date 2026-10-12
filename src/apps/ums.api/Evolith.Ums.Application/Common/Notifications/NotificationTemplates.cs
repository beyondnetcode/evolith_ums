namespace Evolith.Ums.Application.Common.Notifications;

using B = BloqueCorreo;

/// <summary>
/// Las plantillas de notificación de UMS.
///
/// <para>Cada una devuelve DOS cuerpos que dicen lo mismo: el de texto —que es el que sobrevive
/// a un cliente sin HTML, a las imágenes bloqueadas y a un lector de pantalla— y el HTML con la
/// marca, que arma <see cref="NotificationEmailLayout"/>. Editar uno sin el otro deja el mensaje
/// diciendo dos cosas distintas según quién lo abra.</para>
/// </summary>
public static class NotificationTemplates
{
    private const string Firma = "— Equipo UMS";

    /// <summary>
    /// Solicitud anónima de restablecimiento. El secreto viaja SOLO por aquí —el buzón— porque
    /// es justo lo que prueba la posesión de la cuenta; la respuesta HTTP no lo lleva. La cuenta
    /// conserva su contraseña actual hasta que este token se canjea, de modo que una solicitud
    /// ajena molesta pero no deja a nadie fuera.
    /// </summary>
    /// <param name="enlaceCanje">
    /// Enlace directo a la pantalla de canje con el código ya puesto. Opcional: si el despliegue
    /// no declara <c>Notifications:PortalBaseUrl</c> llega vacío y el mensaje se apoya solo en el
    /// código, que es el mecanismo que de verdad importa. Un correo de restablecimiento no puede
    /// dejar de enviarse porque falte una URL de conveniencia.
    /// </param>
    public static UmsNotification PasswordResetRequested(
        string recipient,
        string recipientName,
        string resetToken,
        int expiresInMinutes,
        string? enlaceCanje = null) =>
        new(
            Recipient: recipient,
            Subject: "Restablecimiento de Contraseña — UMS",
            Body: $"""
                Hola {recipientName},

                Recibimos una solicitud para restablecer la contraseña de su cuenta.

                Código de restablecimiento:

                    {resetToken}
                {(enlaceCanje is not null ? $"\nAbra este enlace para definir su nueva contraseña:\n\n    {enlaceCanje}\n" : string.Empty)}
                Ingréselo en la pantalla de recuperación del portal para definir su nueva contraseña.
                El código vence en {expiresInMinutes} minutos y solo puede usarse una vez.

                Su contraseña actual sigue vigente: si no solicitó este cambio, ignore este mensaje
                y no será necesario hacer nada. Si recibe estos avisos con frecuencia, avise a su
                administrador.

                {Firma}
                """,
            RecipientName: recipientName,
            HtmlBody: NotificationEmailLayout.Render(
                "Restablecimiento de contraseña",
                $"Su código vence en {expiresInMinutes} minutos.",
                new B.Parrafo($"Hola {recipientName},"),
                new B.Parrafo("Recibimos una solicitud para restablecer la contraseña de su cuenta."),
                new B.Codigo("Código de restablecimiento", resetToken),
                enlaceCanje is not null
                    ? new B.Boton("Definir mi nueva contraseña", enlaceCanje)
                    : new B.Nota("Ingréselo en la pantalla de recuperación del portal para definir su nueva contraseña."),
                new B.Nota($"El código vence en {expiresInMinutes} minutos y solo puede usarse una vez."),
                new B.Nota(
                    "Su contraseña actual sigue vigente: si no solicitó este cambio, ignore este mensaje y "
                    + "no será necesario hacer nada. Si recibe estos avisos con frecuencia, avise a su administrador.")));

    /// <summary>
    /// Aviso posterior al canje, y la única señal que recibe el titular legítimo si alguien llegó
    /// a canjear un token con su buzón comprometido. <paramref name="tenantLabel"/> nombra el
    /// INQUILINO en el que se aplicó el cambio.
    ///
    /// <para>Un mismo correo puede tener cuenta en varios inquilinos —la unicidad se declara sobre
    /// <c>(TenantId, Email)</c>, no sobre el correo—, así que «su contraseña fue actualizada», a
    /// secas, no le dice al titular DÓNDE. Quien tiene dos cuentas cambia una y sigue sin poder
    /// entrar en la otra, sin nada que se lo explique. Y para el «si no fue usted» el inquilino es
    /// justamente el dato accionable: sin él, el aviso no dice a qué administrador acudir.</para>
    ///
    /// <para>Nombrarlo aquí no abre enumeración: este correo solo llega al buzón cuyo token de
    /// restablecimiento ya se canjeó, es decir, a quien probó poseerlo.</para>
    /// </summary>
    public static UmsNotification PasswordChanged(string recipient, string recipientName, string? tenantLabel = null) =>
        new(
            Recipient: recipient,
            Subject: "Su contraseña fue actualizada — UMS",
            Body: $"""
                Hola {recipientName},

                La contraseña de su cuenta{EnInquilino(tenantLabel)} acaba de ser actualizada y sus sesiones activas se cerraron.

                Si no fue usted, contacte a su administrador de inmediato.

                {Firma}
                """,
            RecipientName: recipientName,
            HtmlBody: NotificationEmailLayout.Render(
                "Su contraseña fue actualizada",
                "Si no fue usted, avise a su administrador.",
                new B.Parrafo($"Hola {recipientName},"),
                new B.Parrafo($"La contraseña de su cuenta{EnInquilino(tenantLabel)} acaba de ser actualizada y sus sesiones activas se cerraron."),
                new B.Nota("Si no fue usted, contacte a su administrador de inmediato.")));

    /// <summary>Coletilla « en el inquilino X», vacía si no se pudo resolver el inquilino.</summary>
    private static string EnInquilino(string? tenantLabel) =>
        string.IsNullOrWhiteSpace(tenantLabel) ? string.Empty : $" en el inquilino {tenantLabel}";

    public static UmsNotification PasswordReset(string recipient, string recipientName, string temporaryPassword) =>
        new(
            Recipient: recipient,
            Subject: "Restablecimiento de Contraseña — UMS",
            Body: $"""
                Hola {recipientName},

                Se ha generado una contraseña temporal para su cuenta:

                    {temporaryPassword}

                Esta clave es válida para un único inicio de sesión. Al ingresar, el sistema le solicitará establecer una nueva contraseña.

                Si no solicitó este cambio, contacte a su administrador de inmediato.

                {Firma}
                """,
            RecipientName: recipientName,
            HtmlBody: NotificationEmailLayout.Render(
                "Contraseña temporal",
                "Válida para un único inicio de sesión.",
                new B.Parrafo($"Hola {recipientName},"),
                new B.Parrafo("Se ha generado una contraseña temporal para su cuenta:"),
                new B.Codigo("Contraseña temporal", temporaryPassword),
                new B.Nota("Es válida para un único inicio de sesión. Al ingresar, el sistema le pedirá establecer una nueva."),
                new B.Nota("Si no solicitó este cambio, contacte a su administrador de inmediato.")));

    public static UmsNotification UserSignupRequestReceived(string tenantAdminEmail, string applicantEmail, string tenantName) =>
        new(
            Recipient: tenantAdminEmail,
            Subject: $"Nueva solicitud de acceso — {tenantName}",
            Body: $"""
                Se recibió una solicitud de registro para su organización:

                    Solicitante : {applicantEmail}
                    Organización: {tenantName}

                Ingrese al portal de administración para revisar y aprobar o rechazar la solicitud.

                {Firma}
                """,
            HtmlBody: NotificationEmailLayout.Render(
                "Nueva solicitud de acceso",
                $"{applicantEmail} pide acceso a {tenantName}.",
                new B.Parrafo("Se recibió una solicitud de registro para su organización:"),
                new B.Datos([("Solicitante", applicantEmail), ("Organización", tenantName)]),
                new B.Nota("Ingrese al portal de administración para revisar y aprobar o rechazar la solicitud.")));

    public static UmsNotification UserSignupConfirmation(string applicantEmail, string tenantName) =>
        new(
            Recipient: applicantEmail,
            Subject: "Solicitud de acceso recibida — UMS",
            Body: $"""
                Su solicitud de acceso a {tenantName} fue recibida correctamente.

                El administrador de su organización la revisará y recibirá una notificación cuando sea aprobada.

                {Firma}
                """,
            HtmlBody: NotificationEmailLayout.Render(
                "Solicitud de acceso recibida",
                $"Su solicitud a {tenantName} está en revisión.",
                new B.Parrafo($"Su solicitud de acceso a {tenantName} fue recibida correctamente."),
                new B.Nota("El administrador de su organización la revisará y recibirá una notificación cuando sea aprobada.")));

    public static UmsNotification UserSignupApproved(string applicantEmail, string recipientName, string tenantName) =>
        new(
            Recipient: applicantEmail,
            Subject: $"Acceso aprobado — {tenantName}",
            Body: $"""
                Hola {recipientName},

                Su solicitud de acceso a {tenantName} fue aprobada.
                Ya puede ingresar al sistema con su correo electrónico.

                {Firma}
                """,
            RecipientName: recipientName,
            HtmlBody: NotificationEmailLayout.Render(
                "Acceso aprobado",
                $"Ya puede ingresar a {tenantName}.",
                new B.Parrafo($"Hola {recipientName},"),
                new B.Parrafo($"Su solicitud de acceso a {tenantName} fue aprobada."),
                new B.Nota("Ya puede ingresar al sistema con su correo electrónico.")));

    public static UmsNotification TenantSignupRequestReceived(string superAdminEmail, string companyName, string contactEmail) =>
        new(
            Recipient: superAdminEmail,
            Subject: $"Nueva solicitud de onboarding — {companyName}",
            Body: $"""
                Se recibió una solicitud de registro de nuevo cliente:

                    Empresa  : {companyName}
                    Contacto : {contactEmail}

                Ingrese al panel de Super Admin para revisar y aprobar la solicitud.

                — Sistema UMS
                """,
            HtmlBody: NotificationEmailLayout.Render(
                "Nueva solicitud de onboarding",
                $"{companyName} pide incorporarse a la plataforma.",
                new B.Parrafo("Se recibió una solicitud de registro de nuevo cliente:"),
                new B.Datos([("Empresa", companyName), ("Contacto", contactEmail)]),
                new B.Nota("Ingrese al panel de Super Admin para revisar y aprobar la solicitud.")));

    public static UmsNotification TenantSignupConfirmation(string contactEmail, string companyName) =>
        new(
            Recipient: contactEmail,
            Subject: "Solicitud de registro recibida — UMS",
            Body: $"""
                Hola,

                La solicitud de registro de {companyName} en la plataforma UMS fue recibida.

                Nuestro equipo la revisará y se pondrá en contacto a la brevedad para completar el proceso de onboarding.

                — Equipo Evolith
                """,
            HtmlBody: NotificationEmailLayout.Render(
                "Solicitud de registro recibida",
                $"La solicitud de {companyName} está en revisión.",
                new B.Parrafo("Hola,"),
                new B.Parrafo($"La solicitud de registro de {companyName} en la plataforma UMS fue recibida."),
                new B.Nota("Nuestro equipo la revisará y se pondrá en contacto a la brevedad para completar el proceso de onboarding.")));

    public static UmsNotification UserSignupDenied(string applicantEmail, string recipientName, string tenantName, string? reason = null) =>
        new(
            Recipient: applicantEmail,
            Subject: $"Solicitud de acceso denegada — {tenantName}",
            Body: $"""
                Hola {recipientName},

                Su solicitud de acceso a {tenantName} fue denegada.
                {(reason is not null ? $"\nMotivo: {reason}\n" : string.Empty)}
                Si considera que esto es un error, comuníquese con el administrador de su organización.

                {Firma}
                """,
            RecipientName: recipientName,
            HtmlBody: NotificationEmailLayout.Render(
                "Solicitud de acceso denegada",
                $"Su solicitud a {tenantName} fue denegada.",
                new B.Parrafo($"Hola {recipientName},"),
                new B.Parrafo($"Su solicitud de acceso a {tenantName} fue denegada."),
                reason is not null
                    ? new B.Datos([("Motivo", reason)])
                    : new B.Nota("No se indicó un motivo."),
                new B.Nota("Si considera que esto es un error, comuníquese con el administrador de su organización.")));

    public static UmsNotification ProfileRequestApproved(string applicantEmail, string recipientName, string tenantName, string systemName, string grantedRole) =>
        new(
            Recipient: applicantEmail,
            Subject: $"Perfil asignado — {tenantName}",
            Body: $"""
                Hola {recipientName},

                Su solicitud de perfil en {tenantName} fue aprobada.

                    Sistema: {systemName}
                    Rol    : {grantedRole}

                Ya puede acceder al sistema con los permisos asignados.

                {Firma}
                """,
            RecipientName: recipientName,
            HtmlBody: NotificationEmailLayout.Render(
                "Perfil asignado",
                $"{grantedRole} en {systemName}.",
                new B.Parrafo($"Hola {recipientName},"),
                new B.Parrafo($"Su solicitud de perfil en {tenantName} fue aprobada."),
                new B.Datos([("Sistema", systemName), ("Rol", grantedRole)]),
                new B.Nota("Ya puede acceder al sistema con los permisos asignados.")));

    public static UmsNotification ProfileRequestDenied(string applicantEmail, string recipientName, string tenantName, string systemName, string? reason = null) =>
        new(
            Recipient: applicantEmail,
            Subject: $"Solicitud de perfil denegada — {tenantName}",
            Body: $"""
                Hola {recipientName},

                Su solicitud de perfil en {tenantName} para el sistema {systemName} fue denegada.
                {(reason is not null ? $"\nMotivo: {reason}\n" : string.Empty)}
                Puede enviar una nueva solicitud desde el portal o comunicarse con su administrador.

                {Firma}
                """,
            RecipientName: recipientName,
            HtmlBody: NotificationEmailLayout.Render(
                "Solicitud de perfil denegada",
                $"Su solicitud para {systemName} fue denegada.",
                new B.Parrafo($"Hola {recipientName},"),
                new B.Parrafo($"Su solicitud de perfil en {tenantName} para el sistema {systemName} fue denegada."),
                reason is not null
                    ? new B.Datos([("Motivo", reason)])
                    : new B.Nota("No se indicó un motivo."),
                new B.Nota("Puede enviar una nueva solicitud desde el portal o comunicarse con su administrador.")));

    public static UmsNotification UserDocumentRejected(string recipientEmail, string recipientName, string documentType, string reason) =>
        new(
            Recipient: recipientEmail,
            Subject: $"Documento rechazado — {documentType}",
            Body: $"""
                Hola {recipientName},

                Su documento de tipo "{documentType}" fue rechazado por el revisor.

                Motivo: {reason}

                Por favor, cargue un nuevo documento válido para continuar con el proceso.

                {Firma}
                """,
            RecipientName: recipientName,
            HtmlBody: NotificationEmailLayout.Render(
                "Documento rechazado",
                $"{documentType}: {reason}",
                new B.Parrafo($"Hola {recipientName},"),
                new B.Parrafo($"Su documento de tipo «{documentType}» fue rechazado por el revisor."),
                new B.Datos([("Motivo", reason)]),
                new B.Nota("Por favor, cargue un nuevo documento válido para continuar con el proceso.")));

    public static UmsNotification UserDocumentValidated(string recipientEmail, string recipientName, string documentType) =>
        new(
            Recipient: recipientEmail,
            Subject: $"Documento validado — {documentType}",
            Body: $"""
                Hola {recipientName},

                Su documento de tipo "{documentType}" fue validado correctamente.

                Ya puede continuar con los siguientes pasos del proceso.

                {Firma}
                """,
            RecipientName: recipientName,
            HtmlBody: NotificationEmailLayout.Render(
                "Documento validado",
                $"{documentType} quedó validado.",
                new B.Parrafo($"Hola {recipientName},"),
                new B.Parrafo($"Su documento de tipo «{documentType}» fue validado correctamente."),
                new B.Nota("Ya puede continuar con los siguientes pasos del proceso.")));

    public static UmsNotification TenantSignupApproved(string contactEmail, string companyName, string adminEmail, string temporaryPassword) =>
        new(
            Recipient: contactEmail,
            Subject: $"¡Bienvenido a UMS! — {companyName}",
            Body: $"""
                Hola,

                Su organización {companyName} fue incorporada exitosamente a la plataforma UMS.

                Credenciales de administrador:

                    Usuario   : {adminEmail}
                    Contraseña: {temporaryPassword}

                Por seguridad, cambie la contraseña en su primer inicio de sesión.

                — Equipo Evolith
                """,
            HtmlBody: NotificationEmailLayout.Render(
                $"Bienvenido a UMS, {companyName}",
                "Sus credenciales de administrador.",
                new B.Parrafo("Hola,"),
                new B.Parrafo($"Su organización {companyName} fue incorporada exitosamente a la plataforma UMS."),
                new B.Datos([("Usuario", adminEmail)]),
                new B.Codigo("Contraseña de administrador", temporaryPassword),
                new B.Nota("Por seguridad, cambie la contraseña en su primer inicio de sesión.")));
}
