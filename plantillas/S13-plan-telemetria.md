# Plan de Telemetría — Semana 13 (Lab 13)

- **Estudiante:** James Deyvi Goñe Lozano
- **Curso:** Desarrollo de Videojuegos en Soluciones Cloud (4-C26-A)
- **SDK:** `com.unity.services.analytics` 6.3.0 (Entorno: `production`)

## 1. Convención de nombres
- **Formato:** `snake_case` en español, sin tildes ni caracteres especiales.
- **Estructura semántica:** `sustantivo + participio` (ejemplo: `juego_iniciado`, `combate_ganado`, `personaje_comprado`).
- **Centralización:** Todos los identificadores se definen como constantes en `Eventos.cs` (`Cloud2026.Services.Eventos`) para evitar fragmentar las series históricas por renombrados accidentales.
- **Privacidad y consentimiento:** El SDK permanece apagado hasta que el jugador acepta explícitamente la pantalla de consentimiento con `AnalyticsService.Instance.PrivacyUrl` (`StartDataCollection()`). Nunca se envían datos personales en parámetros personalizados; la identidad se separa mediante `UnityServices.ExternalUserId`.

## 2. Catálogo de Parámetros Personalizados (8)

| Parámetro | Tipo en Dashboard | Descripción |
| :--- | :--- | :--- |
| `heroe` | `String` | Identificador del personaje utilizado en combate o compra (`guerrero`, `mago`). |
| `origen` | `String` | Pantalla o acceso desde donde se abre la tienda (`menu`, `hud`). |
| `moneda` | `String` | Tipo de divisa utilizada en la transacción (`ORO`). |
| `nivel_rival` | `Integer` | Nivel de dificultad del enemigo enfrentado (`1` = bandidos, `2` = guardia_real). |
| `turnos` | `Integer` | Cantidad total de turnos que duró el combate según `ResultadoCombate`. |
| `segundos` | `Integer` | Tiempo en segundos empleado para completar el tutorial. |
| `precio` | `Integer` | Importe efectivo cobrado por la compra del personaje. |
| `faltante` | `Integer` | Cantidad de moneda faltante en una compra rechazada por saldo insuficiente. |

## 3. Catálogo de Eventos Personalizados (9)

| Evento (`Eventos.*`) | Disparador en el juego | Parámetros enviados | Uso analítico |
| :--- | :--- | :--- | :--- |
| `juego_iniciado` | Al finalizar la inicialización de UGS y autenticación. | *(ninguno)* | Escalón 1 del embudo inicial (base de usuarios). |
| `tutorial_completado` | Al cerrar el último paso del tutorial. | `segundos` | Escalón 2 del embudo inicial y fricción inicial. |
| `primer_combate_iniciado` | Al lanzar el primer combate de la cuenta. | *(ninguno)* | Escalón 3 del embudo inicial (activación de gameplay). |
| `combate_iniciado` | Cada vez que el jugador inicia un combate. | `nivel_rival` | Volumen total de partidas iniciadas por dificultad. |
| `combate_ganado` | Cuando `ResultadoCombate.Victoria == true`. | `heroe`, `nivel_rival`, `turnos` | Numerador de tasa de victoria por héroe y rival. |
| `combate_perdido` | Cuando `ResultadoCombate.Victoria == false`. | `heroe`, `nivel_rival`, `turnos` | Denominador de tasa de victoria y detección de picos de dificultad. |
| `tienda_abierta` | Al abrir la interfaz de la tienda. | `origen` | Intención de compra y navegación hacia economía. |
| `personaje_comprado` | Cuando `ComprarEnServidorAsync` / `RealizarCompra` confirma éxito. | `heroe`, `moneda`, `precio` | Escalón 4 del embudo inicial e ingresos virtuales efectivos. |
| `compra_fallida_sin_fondos` | Cuando la compra se rechaza por saldo insuficiente. | `heroe`, `faltante` | Diagnóstico de precios altos o escasez de moneda. |
