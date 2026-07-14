---
description: "Każdy wyświetlany czas pojawia się w Twojej strefie czasowej — wykrytej z przeglądarki przy pierwszej wizycie i zmienianej w Ustawieniach. Przechowywanie i API pozostają w UTC."
---

# Strefa czasowa

Każdy czas wyświetlany przez aplikację jest renderowany w Twojej strefie czasowej, nie serwera. Twój wybór zapisuje się w profilu i podąża za Tobą między urządzeniami.

Przy pierwszej wizycie aplikacja automatycznie przyjmuje strefę Twojej przeglądarki. Możesz ją zmienić w każdej chwili w Ustawienia → Strefa czasowa; domyślną dla wdrożenia jest opcja white-label App:Branding:DefaultTimeZone (domyślnie UTC). Czasy zawsze są przechowywane i zwracane przez API w UTC — konwertowane jest tylko wyświetlanie.

- Kolejność ustalania: strefa profilu, potem plik cookie, potem domyślna wdrożenia, potem UTC.
- Wykrywanie uruchamia się raz i nigdy nie nadpisuje wybranej przez Ciebie strefy.
- Formatowanie zależy od Twojego języka; etykiety względne jak „2 minuty temu“ nie są zmieniane.
