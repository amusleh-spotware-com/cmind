---
description: "Cada horário exibido aparece no seu próprio fuso horário — detectado do navegador na primeira visita e alterável em Configurações. Armazenamento e APIs permanecem em UTC."
---

# Fuso horário

Cada horário exibido pelo app é renderizado no seu próprio fuso horário, não o do servidor. Sua escolha é salva no seu perfil e acompanha você entre dispositivos.

Na sua primeira visita o app adota automaticamente o fuso do seu navegador. Você pode alterá-lo a qualquer momento em Configurações → Fuso horário; o padrão da implantação é a opção white-label App:Branding:DefaultTimeZone (padrão UTC). Os horários são sempre armazenados e retornados pela API em UTC — apenas a exibição é convertida.

- Ordem de resolução: fuso do perfil, depois o cookie, depois o padrão da implantação, depois UTC.
- A detecção roda uma vez e nunca sobrepõe um fuso que você escolheu.
- A formatação segue seu idioma; rótulos relativos como «há 2 minutos» não são afetados.
