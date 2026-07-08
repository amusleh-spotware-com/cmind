{{- define "cmind.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "cmind.fullname" -}}
{{- printf "%s-%s" .Release.Name (include "cmind.name" .) | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "cmind.labels" -}}
app.kubernetes.io/name: {{ include "cmind.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
helm.sh/chart: {{ .Chart.Name }}-{{ .Chart.Version }}
{{- end -}}

{{- define "cmind.secretName" -}}
{{- if .Values.secrets.existingSecret -}}
{{ .Values.secrets.existingSecret }}
{{- else -}}
{{ include "cmind.fullname" . }}-secrets
{{- end -}}
{{- end -}}

{{- define "cmind.image" -}}
{{- .Values.image.registry }}/{{ .Values.image.repository }}
{{- end -}}

{{- define "cmind.webUrl" -}}
http://{{ include "cmind.fullname" . }}-web:{{ .Values.web.port }}
{{- end -}}

{{- define "cmind.connectionString" -}}
{{- if .Values.postgres.enabled -}}
Host={{ include "cmind.fullname" . }}-postgres;Port=5432;Database=appdb;Username=postgres;Password=$(PG_PASSWORD)
{{- else -}}
{{ .Values.externalDatabase.connectionString }}
{{- end -}}
{{- end -}}
