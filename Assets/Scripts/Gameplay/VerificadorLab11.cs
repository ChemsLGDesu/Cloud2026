using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using UnityEngine;

public class VerificadorLab11 : MonoBehaviour
{
    [SerializeField] private RankingCliente ranking;
    [SerializeField] private ReproductorCombate reproductor;
    [Tooltip("Desmarcado = Corrida 1 (4 jugadores + trampa aceptada). Marcado = Corrida 2 (trampa rechazada tras S11-politica-ranking.ac).")]
    [SerializeField] private bool soloPruebaPostPolitica = false;

    private async void Start()
    {
        if (ranking == null) ranking = GetComponent<RankingCliente>();
        if (reproductor == null) reproductor = GetComponent<ReproductorCombate>();

        int intentos = 0;
        while ((UnityServices.State != ServicesInitializationState.Initialized || !AuthenticationService.Instance.IsSignedIn) && intentos < 30)
        {
            await Task.Delay(250);
            intentos++;
        }
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        if (!soloPruebaPostPolitica)
            await EjecutarCorridaCompletaAntesDePoliticaAsync();
        else
            await EjecutarCorridaDespuesDePoliticaAsync();
    }

    private async Task<string> CambiarAJugadorLimpioAsync(string prefijoPerfil, string nombreVisible)
    {
        if (AuthenticationService.Instance.IsSignedIn)
            AuthenticationService.Instance.SignOut(false);

        AuthenticationService.Instance.SwitchProfile(prefijoPerfil);
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        await AuthenticationService.Instance.UpdatePlayerNameAsync(nombreVisible);

        // Comprar guerrero para poder pelear
        await CloudCodeService.Instance.CallModuleEndpointAsync<object>(
            "Tienda", "ComprarHeroe",
            new Dictionary<string, object>
            {
                { "idCompra", "COMPRA_GUERRERO" },
                { "idOperacion", ReproductorCombate.NuevaOperacion() }
            });

        return AuthenticationService.Instance.PlayerId;
    }

    private async Task EjecutarCorridaCompletaAntesDePoliticaAsync()
    {
        string sufijo = Guid.NewGuid().ToString("N").Substring(0, 4);
        string pBruno = $"bruno_{sufijo}";
        string pCarla = $"carla_{sufijo}";
        string pDana = $"dana_{sufijo}";
        string pAna = $"ana_{sufijo}";
        PlayerPrefs.SetString("L11_PerfilAna", pAna);
        PlayerPrefs.Save();

        // 1. Bruno gana 3 combates contra bandidos (+5 -> 5 -> 10 -> 15)
        await CambiarAJugadorLimpioAsync(pBruno, "Bruno");
        int trofeosBruno = 0;
        for (int i = 0; i < 3; i++)
        {
            var r = await reproductor.PelearAsync(new[] { "HEROE_GUERRERO" }, "bandidos", ReproductorCombate.NuevaOperacion());
            int antes = trofeosBruno;
            trofeosBruno += r.TrofeosCambio;
            Debug.Log($"[L11] Bruno vs bandidos: {(r.Victoria ? "victoria" : "derrota")} · trofeos {(r.TrofeosCambio >= 0 ? "+" : "")}{r.TrofeosCambio} → {trofeosBruno} · puesto {r.PuestoEnRanking}º");
        }

        // 2. Carla gana 2 combates contra bandidos (+5 -> 5 -> 10)
        await CambiarAJugadorLimpioAsync(pCarla, "Carla");
        int trofeosCarla = 0;
        for (int i = 0; i < 2; i++)
        {
            var r = await reproductor.PelearAsync(new[] { "HEROE_GUERRERO" }, "bandidos", ReproductorCombate.NuevaOperacion());
            trofeosCarla += r.TrofeosCambio;
            Debug.Log($"[L11] Carla vs bandidos: {(r.Victoria ? "victoria" : "derrota")} · trofeos {(r.TrofeosCambio >= 0 ? "+" : "")}{r.TrofeosCambio} → {trofeosCarla} · puesto {r.PuestoEnRanking}º");
        }

        // 3. Dana pierde contra guardia_real con 0 trofeos (suelo en 0 -> 0)
        await CambiarAJugadorLimpioAsync(pDana, "Dana");
        var rDana = await reproductor.PelearAsync(new[] { "HEROE_GUERRERO" }, "guardia_real", ReproductorCombate.NuevaOperacion());
        Debug.Log($"[L11] Dana vs guardia_real: {(rDana.Victoria ? "victoria" : "derrota")} · trofeos {rDana.TrofeosCambio} → 0 · puesto {rDana.PuestoEnRanking}º");

        // 4. Ana gana a bandidos (+5 -> 5) y luego pierde contra guardia_real (-5 -> 0)
        await CambiarAJugadorLimpioAsync(pAna, "Ana");
        var rAna1 = await reproductor.PelearAsync(new[] { "HEROE_GUERRERO" }, "bandidos", ReproductorCombate.NuevaOperacion());
        Debug.Log($"[L11] Ana vs bandidos: {(rAna1.Victoria ? "victoria" : "derrota")} · trofeos +{rAna1.TrofeosCambio} → 5 · puesto {rAna1.PuestoEnRanking}º");

        var rAna2 = await reproductor.PelearAsync(new[] { "HEROE_GUERRERO" }, "guardia_real", ReproductorCombate.NuevaOperacion());
        Debug.Log($"[L11] Ana vs guardia_real: {(rAna2.Victoria ? "victoria" : "derrota")} · trofeos {rAna2.TrofeosCambio} → 0 · puesto {rAna2.PuestoEnRanking}º");

        // 5. Tres vistas del cliente (Pasos 5 y 6)
        await ImprimirVistasAsync();

        // 6. Trampa ANTES de la política (Paso 8)
        try
        {
            var trampaRes = await LeaderboardsService.Instance.AddPlayerScoreAsync(RankingCliente.IdTabla, 9999);
            Debug.LogWarning($"[L11] trampa (Ana envía 9999 desde el cliente) → ACEPTADA: puesto {trampaRes.Rank + 1}º con {trampaRes.Score}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[L11] trampa (Ana envía 9999 desde el cliente) → RECHAZADA: {ex.Message}");
        }

        await ImprimirVistasAsync();

        // 7. Vista de temporada con hora del servidor (Paso 9)
        var temp = await ranking.VistaTemporadaAsync();
        Debug.Log($"[L11] temporada «{temp.NombreTemporada}»: puesto {temp.Puesto}º · {temp.Trofeos} trofeos · nivel {temp.Tier} · premio {temp.Premio}");
        Debug.Log($"[L11] cierre {temp.CierreUtc} · faltan {temp.TiempoRestanteTexto} (hora del servidor {temp.HoraServidorUtc}; reloj local {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss'Z'})");
    }

    private async Task EjecutarCorridaDespuesDePoliticaAsync()
    {
        string pAna = PlayerPrefs.GetString("L11_PerfilAna", "");
        if (string.IsNullOrEmpty(pAna))
        {
            Debug.LogError("[L11] Primero ejecuta la corrida sin política para crear el perfil de Ana.");
            return;
        }

        if (AuthenticationService.Instance.IsSignedIn)
            AuthenticationService.Instance.SignOut(false);
        AuthenticationService.Instance.SwitchProfile(pAna);
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        // 1. Intentar trampa 9999 con la política desplegada
        // 6. Trampa ANTES de la política (Paso 8)
        try
        {
            var trampaRes = await ranking.IntentarEnviarPuntajeDesdeClienteAsync(9999);
            Debug.LogWarning($"[L11] trampa (Ana envía 9999 desde el cliente) → ACEPTADA: puesto {trampaRes.rank + 1}º con {trampaRes.score}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[L11] trampa (Ana envía 9999 desde el cliente) → RECHAZADA: {ex.Message}");
        }

        // 2. Combate real de Ana: el servidor publica su total real (5) y pisa el 9999
        var rAna = await reproductor.PelearAsync(new[] { "HEROE_GUERRERO" }, "bandidos", ReproductorCombate.NuevaOperacion());
        Debug.Log($"[L11] combate de Ana → {rAna.Mensaje} · trofeos +0 → 5 · puesto {rAna.PuestoEnRanking}º.");

        await ImprimirVistasAsync();
    }

    private async Task ImprimirVistasAsync()
    {
        var top = await ranking.RefrescarTopAsync(10);
        var filasTop = new List<string>();
        foreach (var e in top)
            filasTop.Add($"{e.rank + 1}º {e.playerName} {e.score} {e.tier}");
        Debug.Log($"[L11]   top {top.Count}: {string.Join(" · ", filasTop)}");

        var propio = await ranking.ObtenerPropioAsync();
        if (propio != null)
            Debug.Log($"[L11]   puesto propio: {propio.rank + 1}º {propio.playerName} {propio.score} {propio.tier}");
        else
            Debug.Log("[L11]   puesto propio: sin clasificar");

        var vecinos = await ranking.ObtenerVecinosAsync(5);
        var filasVec = new List<string>();
        foreach (var e in vecinos)
            filasVec.Add($"{e.rank + 1}º {e.playerName} {e.score} {e.tier}");
        Debug.Log($"[L11]   vecinos ({vecinos.Count}): {string.Join(" · ", filasVec)}");
    }
}