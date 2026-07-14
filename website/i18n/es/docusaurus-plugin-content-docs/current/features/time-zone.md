---
description: "Cada hora mostrada aparece en tu propia zona horaria — detectada del navegador en la primera visita y modificable en Ajustes. El almacenamiento y las API siguen en UTC."
---

# Zona horaria

Cada hora que muestra la app se representa en tu propia zona horaria, no la del servidor. Tu elección se guarda en tu perfil y te acompaña entre dispositivos.

En tu primera visita la app adopta automáticamente la zona de tu navegador. Puedes cambiarla en cualquier momento en Ajustes → Zona horaria; el valor por defecto del despliegue es la opción white-label App:Branding:DefaultTimeZone (por defecto UTC). Las horas siempre se almacenan y se devuelven por la API en UTC — solo se convierte la presentación.

- Orden de resolución: zona del perfil, luego la cookie, luego el valor por defecto del despliegue, luego UTC.
- La detección se ejecuta una vez y nunca anula una zona que hayas elegido.
- El formato sigue tu idioma; las etiquetas relativas como «hace 2 minutos» no se ven afectadas.
