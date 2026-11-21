using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using UnityEngine;

public class VerificadorLab08 : MonoBehaviour
{
    [SerializeField] private ReproductorCombate reproductor;

    private async void Start()
    {
        if (reproductor == null)
            reproductor = GetComponent<ReproductorCombate>();

        reproductor.AccionReproducida += entrada =>
            Debug.Log($"[L08] ▶ turno {entrada.turno}: {entrada.atacante} golpea a {entrada.objetivo} por {entrada.dano} (le quedan {entrada.vidaRestante})");

        reproductor.CombateTerminado += r =>
            Debug.Log($"[L08] ▶ reproducción terminada: {(r.Victoria ? "VICTORIA" : "DERROTA")}");

        // Esperar a que GameBootstrap inicialice UGS e inicie sesión
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

        Debug.Log($"[L08] jugador = {AuthenticationService.Instance.PlayerId}");

        // 1. Pelea con inventario vacío (o antes de comprar)
        var sinHeroes = await reproductor.PelearAsync(new[] { "HEROE_GUERRERO" }, "bandidos");
        Debug.Log($"[L08] ResolverCombate sin héroes → exito {sinHeroes.Exito}: {sinHeroes.Mensaje}");

        // 2. Comprar HEROE_GUERRERO
        await CloudCodeService.Instance.CallModuleEndpointAsync<object>(
            "Tienda", "ComprarHeroe",
            new Dictionary<string, object>
            {
                { "idCompra", "COMPRA_GUERRERO" },
                { "idOperacion", Guid.NewGuid().ToString() }
            });

        // 3. Pelea válida contra bandidos
        var resultado = await reproductor.PelearAsync(new[] { "HEROE_GUERRERO" }, "bandidos");
        Debug.Log($"[L08] ResolverCombate([HEROE_GUERRERO], bandidos) → {resultado.Mensaje} | semilla {resultado.Semilla} | saldo {resultado.SaldoResultante}");

        // 4. Paso 7: RepetirCombate y verificar determinismo
        var repeticion = await reproductor.RepetirAsync();
        bool identico = ReproductorCombate.MismoBattleLog(resultado, repeticion);
        Debug.Log($"[L08] RepetirCombate → semilla {repeticion.Semilla} · {repeticion.BattleLog.Count} acciones · repeticion {repeticion.Repeticion}");
        Debug.Log($"[L08] ¿battle log IDÉNTICO al original? {(identico ? "SÍ" : "NO")}");

        // 5. Paso 8: Reproducir cada 0.6s
        StartCoroutine(reproductor.Reproducir(resultado, 0.6f));
        await Task.Delay(Mathf.CeilToInt((resultado.BattleLog.Count + 1) * 600));

        // 6. Paso 9: 5 intentos de trampa
        var trampa1Args = new Dictionary<string, object>
        {
            { "equipo", new[] { "HEROE_GUERRERO" } },
            { "idRival", "guardia_real" },
            { "dano", 9999 },
            { "victoria", true },
            { "semilla", resultado.Semilla }
        };
        var t1 = await CloudCodeService.Instance.CallModuleEndpointAsync<ResultadoCombateDto>("Tienda", "ResolverCombate", trampa1Args);
        Debug.LogWarning($"[L08] trampa 1 (dano=9999, victoria=true, semilla={resultado.Semilla}) → {t1.Mensaje} (semilla real {t1.Semilla})");

        var t2 = await reproductor.PelearAsync(new[] { "HEROE_COLOSO" }, "bandidos");
        Debug.LogWarning($"[L08] trampa 2 (HEROE_COLOSO sin tenerlo) → exito {t2.Exito}: {t2.Mensaje}");

        var t3 = await reproductor.PelearAsync(new[] { "HEROE_GUERRERO", "HEROE_GUERRERO" }, "bandidos");
        Debug.LogWarning($"[L08] trampa 3 (guerrero dos veces) → exito {t3.Exito}: {t3.Mensaje}");

        var t4 = await reproductor.PelearAsync(new[] { "HEROE_GUERRERO", "HEROE_GUERRERO", "HEROE_GUERRERO", "HEROE_GUERRERO" }, "bandidos");
        Debug.LogWarning($"[L08] trampa 4 (cuatro héroes) → exito {t4.Exito}: {t4.Mensaje}");

        var t5 = await reproductor.PelearAsync(new[] { "HEROE_GUERRERO" }, "dragon");
        Debug.LogWarning($"[L08] trampa 5 (rival inexistente) → exito {t5.Exito}: {t5.Mensaje}");
    }
}