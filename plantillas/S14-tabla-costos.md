# Laboratorio 14 — Análisis de Consumo y Proyección de Costos (UGS)

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

## 2. Desglose por operación y modelo por sesión ($p$ combates)

Fórmulas base para un jugador recurrente con $p$ combates por sesión (separando jugadores nuevos que ejecutan una única inicialización):
- **Invocaciones Cloud Code:** $2 + p$ (incluye `LeerEstado`, `BuscarRival` y $p$ combates; $+1$ si compra héroe).
- **Lecturas Cloud Save:** $2\text{ (LeerEstado)} + 2\text{ (BuscarRival)} + 2p\text{ (control\_estado + estado)}$.
- **Lecturas Remote Config:** $1 + 2p$.
- **Escrituras Cloud Save:** $p\text{ (lote Protected)}$ ($+1$ si realiza compra; $+2$ únicas si es jugador nuevo).
- **Publicaciones al Ranking:** $p$.

## 3. Medición real de sesión declarada (`development`, $p = 3$ combates)

| Momento de juego | Endpoint / Acción | Invocaciones | Lecturas CS | Lecturas RC | Escrituras CS | Ranking | Duración cliente (ms) | `duracionMs` servidor |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| Inicio de sesión (nuevo) | `LeerEstado` (1.ª vez) | 1 | 2 | 1 | 2 | 0 | — | — |
| Compra de héroe | `ComprarHeroe` (`guerrero`) | 1 | 2 | 2 | 1 | 0 | — | — |
| Búsqueda de rival | `BuscarRival` | 1 | 2 | 0 | 0 | 0 | — | — |
| Combate 1 | `ResolverCombate` | 1 | 2 | 2 | 1 | 1 | — | — |
| Combate 2 | `ResolverCombate` | 1 | 2 | 2 | 1 | 1 | — | — |
| Combate 3 | `ResolverCombate` | 1 | 2 | 2 | 1 | 1 | — | — |
| Vista de temporada | `VistaTemporada` | 1 | 1 | 1 | 0 | 0 | — | — |
| **Total sesión ($p=3$)** | **Sesión completa** | **7** | **13** | **10** | **6** | **3** | — | — |

## 4. Proyección mensual (10,000 DAU · 2 sesiones/día · $p = 3$ combates)

- **Sesiones mensuales:** $10,000 \times 2 \times 30 = 600,000\text{ sesiones/mes}$.
- **Invocaciones Cloud Code:** $600,000 \times 6 = 3,600,000$ → excedente $2,600,000 \times (\$1.50 / 1,000,000) = \$3.90/\text{mes}$.
- **Lecturas Cloud Save:** $600,000 \times 10 = 6,000,000$ → excedente $5,000,000 \times (\$0.009 / 10,000) = \$4.50/\text{mes}$.
- **Escrituras Cloud Save:** $600,000 \times 3 = 1,800,000$ → excedente $800,000 \times (\$0.025 / 10,000) = \$2.00/\text{mes}$.
- **Costo total estimado sin optimizar:** **$\$10.40/\text{mes}$** (más tiempo de cómputo dentro de las 20 h gratuitas).

## 5. Dos optimizaciones propuestas y ahorro estimado

1. **Caché en memoria con TTL corto (30 s) para Remote Config dentro del módulo C# y lectura combinada de claves:**
   - **Impacto:** Elimina $2$ llamadas de red a Remote Config por cada combate (de $1 + 2p$ a $1$ lectura por instancia caliente), reduciendo la latencia `duracionMs` de `ResolverCombate` en ~35–50 ms por combate.
   - **Regla de consistencia:** Nunca usar caché expirada para validar precios o recompensas; ante expiración del TTL, refrescar desde Remote Config antes de cobrar.
2. **Consolidación de lectura inicial (`LeerEstado` + progresión de temporada en una sola invocación):**
   - **Impacto:** Fusiona `LeerEstado` y `VistaTemporada` al arrancar, ahorrando $1$ invocación de Cloud Code y $1$ lectura de Cloud Save por sesión ($600,000$ invocaciones y $600,000$ lecturas menos al mes, equivalente a un ahorro directo de **$\$1.44/\text{mes}$** en excedentes de cuota).
