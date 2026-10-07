{{/*
BankSwitch v25 — Helm template helpers
*/}}

{{- define "bankswitch.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{- define "bankswitch.fullname" -}}
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

{{- define "bankswitch.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/* Common labels for all resources */}}
{{- define "bankswitch.labels" -}}
helm.sh/chart: {{ include "bankswitch.chart" . }}
app.kubernetes.io/name: {{ include "bankswitch.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
app.kubernetes.io/part-of: bankswitch
{{- end }}

{{/* Selector labels for Engine pods */}}
{{- define "bankswitch.engine.selectorLabels" -}}
app.kubernetes.io/name: {{ include "bankswitch.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/component: engine
{{- end }}

{{/* Selector labels for Admin pods */}}
{{- define "bankswitch.admin.selectorLabels" -}}
app.kubernetes.io/name: {{ include "bankswitch.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/component: admin
{{- end }}

{{/* Service account name */}}
{{- define "bankswitch.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "bankswitch.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}
