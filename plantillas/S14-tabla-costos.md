# Laboratorio 14 — Análisis de Consumo y Proyección de Costos (UGS)

- **Alumno:** James Deyvi Goñe Lozano
- **Fecha de consulta de tarifas:** 2026-11-22
- **Fuente oficial:** https://unity.com/products/gaming-services/pricing

## 1. Tarifas vigentes y unidades de cobro

| Servicio UGS | Capa gratuita mensual | Unidad de cobro excedente | Tarifa vigente (USD) |
| :--- | :--- | :--- | :--- |
| **Cloud Code (C# Modules)** | 1,000,000 invocaciones + 20 h cómputo | Por millón de invocaciones / por segundo GB-s | $1.50 / millón · $0.00002 / s |
| **Cloud Save (Lecturas)** | 1,000,000 lecturas | Bloque de 10,000 lecturas | $0.009 / 10,000 |
| **Cloud Save (Escrituras)** | 1,000,000 escrituras | Bloque de 10,000 escrituras | $0.025 / 10,000 |
| **Remote Config** | Ilimitado (incluido) | N/A | $0.00 |
| **Authentication** | Ilimitado (incluido) | N/A | $0.00 |
| **Leaderboards** | Incluido (promocional) | N/A | $0.00 |

## 2. Modelo matemático por sesión ($p$ combates)

Separando jugadores recurrentes de jugadores nuevos (la inicialización en `LeerEstado` añade 2 escrituras de Cloud Save una sola vez por cuenta) y sin mezclar unidades de cobro entre servicios:
- **Invocaciones Cloud Code:** $2 + p$ (`LeerEstado` + `BuscarRival` + $p$ combates; $+1$ si hay compra en `Tienda` y $+1$ por `VistaTemporada`).
- **Lecturas Cloud Save:** $2\text{ (LeerEstado)} + 2\text{ (BuscarRival)} + 2p\text{ (control\_estado + estado)}$.
- **Lecturas Remote Config:** $1 + 2p$.
- **Escrituras Cloud Save (Protected):** $p$ ($+1$ por cada `ComprarHeroe`; $+2$ únicas en el primer acceso).
- **Publicaciones al Ranking:** $p$.

## 3. Medición real de sesión declarada (`development`, $p = 3$ combates)

Mediciones ejecutadas el 2026-11-22 sobre el entorno `development` (`bce22f38-b39d-471d-b2c0-00d68642d667`):

| Momento de juego | Función Cloud Code | Invocaciones | Lecturas CS | Lecturas RC | Escrituras CS | Ranking | Latencia cliente (ms) |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| Inicio de sesión (1.ª vez) | `LeerEstado` | 1 | 2 | 1 | 2 | 0 | 452 ms |
| Inicio de sesión (2.ª vez) | `LeerEstado` | 1 | 2 | 1 | 0 | 0 | 329 ms |
| Compra de héroe | `ComprarHeroe` | 1 | 2 | 2 | 1 | 0 | 241 ms |
| Búsqueda de rival | `BuscarRival` | 1 | 2 | 0 | 0 | 0 | 252 ms |
| Combate 1 | `ResolverCombate` | 1 | 2 | 2 | 1 | 1 | 240 ms |
| Combate 2 | `ResolverCombate` | 1 | 2 | 2 | 1 | 1 | 250 ms |
| Combate 3 | `ResolverCombate` | 1 | 2 | 2 | 1 | 1 | 255 ms |
| Vista de temporada | `VistaTemporada` | 1 | 1 | 1 | 0 | 0 | 283 ms |
| **Total sesión recurrente ($p=3$)** | **Flujo completo** | **7** | **13** | **10** | **4** | **3** | **248.33 ms (media combate)** |

## 4. Proyección mensual (10,000 DAU · 2 sesiones/día · $p = 3$ combates)

- **Volumen mensual:** $10,000 \times 2 \times 30 = 600,000\text{ sesiones/mes}$.
- **Invocaciones Cloud Code:** $600,000 \times 7 = 4,200,000$ → excedente $3,200,000 \times (\$1.50 / 1,000,000) = \$4.80/\text{mes}$.
- **Lecturas Cloud Save:** $600,000 \times 13 = 7,800,000$ → excedente $6,800,000 \times (\$0.009 / 10,000) = \$6.12/\text{mes}$.
- **Escrituras Cloud Save:** $600,000 \times 4 = 2,400,000$ → excedente $1,400,000 \times (\$0.025 / 10,000) = \$3.50/\text{mes}$.
- **Costo mensual estimado base:** **$\$14.42/\text{mes}$** (el tiempo de cómputo permanece dentro de las 20 horas gratuitas).

## 5. Dos optimizaciones propuestas y ahorro estimado

1. **Caché en memoria con TTL corto (30 s) para Remote Config y lectura por lote en el módulo C#:**
   - **Ahorro técnico:** Reduce de $1 + 2p$ llamadas a Remote Config a una única lectura compartida por instancia caliente, recortando la latencia interna `duracionMs` de `ResolverCombate` en ~35–50 ms por combate.
   - **Salvaguarda de integridad:** Nunca se usa una configuración expirada para cobrar precios ni otorgar recompensas; al vencer el TTL se consulta de nuevo Remote Config como fuente autoritativa.
2. **Consolidación de carga inicial (`LeerEstado` + `VistaTemporada` en una sola petición):**
   - **Ahorro económico:** Elimina $1$ invocación de Cloud Code y $1$ lectura de Cloud Save por sesión ($600,000$ invocaciones y $600,000$ lecturas menos al mes), reduciendo el gasto mensual en **$\$1.44/\text{mes}$** (bajando de $\$14.42$ a **$\$12.98/\text{mes}$**).
