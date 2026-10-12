{{/*
Expand the name of the chart.
*/}}
{{- define "ums-helm.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
If release name contains chart name it will be used as a full name.
*/}}
{{- define "ums-helm.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "ums-helm.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "ums-helm.labels" -}}
helm.sh/chart: {{ include "ums-helm.chart" . }}
{{ include "ums-helm.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/*
Selector labels
*/}}
{{- define "ums-helm.selectorLabels" -}}
app.kubernetes.io/name: {{ include "ums-helm.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Create the name of the service account to use
*/}}
{{- define "ums-helm.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "ums-helm.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

{{/*
Cadena de conexion, SEGUN EL MOTOR (G-281, G-282).

Vive aqui y no en la plantilla del Secret porque la consumen DOS recursos —el secreto del release y
la copia efimera que lee el Job de migracion— y dos copias del mismo `printf` divergirian el dia que
alguien toque una sola. Que el Job y las replicas apunten a bases distintas es exactamente el fallo
que G-282 registra.
*/}}
{{- define "ums-helm.cadenaDeConexion" -}}
{{- if eq (.Values.backend.persistenceProvider | default "PostgreSql") "SqlServer" -}}
Server={{ .Values.secrets.db.host }},{{ .Values.secrets.db.port }};Database={{ .Values.secrets.db.database }};User Id={{ .Values.secrets.db.username }};Password={{ .Values.secrets.db.postgresPassword }};TrustServerCertificate=True
{{- else -}}
Host={{ .Values.secrets.db.host }};Port={{ .Values.secrets.db.port }};Database={{ .Values.secrets.db.database }};Username={{ .Values.secrets.db.username }};Password={{ .Values.secrets.db.postgresPassword }};
{{- end -}}
{{- end }}
