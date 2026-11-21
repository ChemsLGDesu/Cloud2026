using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using UnityEngine;

public class VerificadorLab10 : MonoBehaviour
{
    [SerializeField] private ProgresionCliente progresion;
    [SerializeField] private ReproductorCombate reproductor;

    private async void Start()
    {
        if (progresion == null) progresion = GetComponent<ProgresionCliente>();
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

        // Perfil limpio para ver el ANTES y DESPUÉS desde nivel 1
        string perfilLimpio = $"lab10_{Guid.NewGuid().ToString("N").Substring(0, 6)}";
        if (AuthenticationService.Instance.IsSignedIn)
            AuthenticationService.Instance.SignOut(false);
        AuthenticationService.Instance.SwitchProfile(perfilLimpio);
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        Debug.Log($"[L10] ▶ jugador = {AuthenticationService.Instance.PlayerId}");

        // Comprar guerrero (deja 500 ORO)
        await CloudCodeService.Instance.CallModuleEndpointAsync<object>(
            "Tienda", "ComprarHeroe",
            new Dictionary<string, object>
            {
                { "idCompra", "COMPRA_GUERRERO" },
                { "idOperacion", ReproductorCombate.NuevaOperacion() }
            });

        await progresion.RefrescarAsync();
        Debug.Log($"[L10] ANTES · {progresion.ResumenCorto()}");

        // Combate 1 con idOperacion explícito + reintento con el MISMO idOperacion
        string op1 = ReproductorCombate.NuevaOperacion();
        var r1 = await reproductor.PelearAsync(new[] { "HEROE_GUERRERO" }, "bandidos", op1);
        Debug.Log($"[L10] combate 1 → {r1.Mensaje} · saldo {r1.SaldoResultante}");

        var r1Repetida = await reproductor.PelearAsync(new[] { "HEROE_GUERRERO" }, "bandidos", op1);
        bool mismoLog = ReproductorCombate.MismoBattleLog(r1, r1Repetida);
        Debug.Log($"[L10] reintento MISMO idOperacion → Repetida={r1Repetida.Repetida} · mismo battle log={(mismoLog ? "SÍ" : "NO")} · saldo {r1Repetida.SaldoResultante} · {r1Repetida.Mensaje}");

        // Combates adicionales para subir de nivel y probar nodos
        for (int i = 2; i <= 4; i++)
        {
            var rk = await reproductor.PelearAsync(new[] { "HEROE_GUERRERO" }, "bandidos", ReproductorCombate.NuevaOperacion());
            Debug.Log($"[L10] combate {i} → {rk.Mensaje} · cuenta n{rk.NivelCuenta} ({rk.ExperienciaCuenta} xp) · saldo {rk.SaldoResultante}");
        }

        // Pruebas de nodos (Paso 9)
        var nAtaque2SinPrev = await progresion.DesbloquearNodoAsync("TALENTO_ATAQUE_2");
        Debug.Log($"[L10] DesbloquearNodo(TALENTO_ATAQUE_2) sin previo → exito {nAtaque2SinPrev.Exito}: {nAtaque2SinPrev.Mensaje}");

        var nMaestria = await progresion.DesbloquearNodoAsync("TALENTO_MAESTRIA");
        Debug.Log($"[L10] DesbloquearNodo(TALENTO_MAESTRIA) → exito {nMaestria.Exito}: {nMaestria.Mensaje}");

        var nAtaque1 = await progresion.DesbloquearNodoAsync("TALENTO_ATAQUE_1");
        Debug.Log($"[L10] DesbloquearNodo(TALENTO_ATAQUE_1) → exito {nAtaque1.Exito}: {nMaestria.Mensaje}");

        var nAtaque1OtraVez = await progresion.DesbloquearNodoAsync("TALENTO_ATAQUE_1");
        Debug.Log($"[L10] DesbloquearNodo(TALENTO_ATAQUE_1) repetido → exito {nAtaque1OtraVez.Exito}: {nAtaque1OtraVez.Mensaje}");

        var nAtaque2 = await progresion.DesbloquearNodoAsync("TALENTO_ATAQUE_2");
        Debug.Log($"[L10] DesbloquearNodo(TALENTO_ATAQUE_2) → exito {nAtaque2.Exito}: {nAtaque2.Mensaje}");

        // Trampa: enviar experiencia/nivel/victoria falsos en ResolverCombate
        var trampa = await CloudCodeService.Instance.CallModuleEndpointAsync<ResultadoCombateDto>(
            "Tienda", "ResolverCombate",
            new Dictionary<string, object>
            {
                { "equipo", new[] { "HEROE_GUERRERO" } },
                { "idRival", "bandidos" },
                { "idOperacion", ReproductorCombate.NuevaOperacion() },
                { "experiencia", 99999 },
                { "nivel", 10 },
                { "victoria", true }
            });
        Debug.LogWarning($"[L10] trampa (experiencia=99999, nivel=10, victoria=true) → {trampa.Mensaje}");

        await progresion.RefrescarAsync();
        Debug.Log($"[L10] DESPUÉS · {progresion.ResumenCorto()}");
    }
}