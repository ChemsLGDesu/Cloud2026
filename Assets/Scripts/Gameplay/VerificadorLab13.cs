using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cloud2026.Services;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using UnityEngine;

public class VerificadorLab13 : MonoBehaviour
{
    [SerializeField] private Telemetria telemetria;
    [SerializeField] private UGSEconomiaService economia;
    [SerializeField] private ReproductorCombate reproductor;
    [SerializeField] private RivalesCliente rivalesCliente;
    [SerializeField] private bool soloSimularCombates = true;

    private async void Start()
    {
        if (telemetria == null) telemetria = GetComponent<Telemetria>() ?? gameObject.AddComponent<Telemetria>();
        if (economia == null) economia = FindFirstObjectByType<UGSEconomiaService>();
        if (reproductor == null) reproductor = FindFirstObjectByType<ReproductorCombate>();
        if (rivalesCliente == null) rivalesCliente = FindFirstObjectByType<RivalesCliente>();

        int intentos = 0;
        while ((UnityServices.State != ServicesInitializationState.Initialized ||
                !AuthenticationService.Instance.IsSignedIn) && intentos < 40)
        {
            await Task.Delay(250);
            intentos++;
        }

        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        string idBase = AuthenticationService.Instance.PlayerId;
        bool acepto = await telemetria.SolicitarConsentimientoEIniciarAsync(idBase);
        if (!acepto) return;

        await SimularCombatesRealesAsync();
    }

    private async Task CambiarPerfilAsync(string perfil)
    {
        if (AuthenticationService.Instance.IsSignedIn)
            AuthenticationService.Instance.SignOut(false);
        AuthenticationService.Instance.SwitchProfile(perfil);
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    private async Task ComprarHeroeCloudAsync(string idCompra)
    {
        try
        {
            await CloudCodeService.Instance.CallModuleEndpointAsync<object>(
                "Tienda", "ComprarHeroe",
                new Dictionary<string, object>
                {
                    { "idCompra", idCompra },
                    { "idOperacion", Guid.NewGuid().ToString() }
                });
        }
        catch { }
    }

    private async Task SimularCombatesRealesAsync()
    {
        string sufijo = Guid.NewGuid().ToString("N").Substring(0, 6);
        Debug.Log($"[DVSC] Iniciando simulación de combates Lab 13 (lote={sufijo})...");

        // 1. Rival nivel 1 ("Bandidos" - defensa débil)
        await CambiarPerfilAsync($"l13_rival1_{sufijo}");
        string idRivalNivel1 = AuthenticationService.Instance.PlayerId;
        await ComprarHeroeCloudAsync("COMPRA_GUERRERO");
        if (rivalesCliente != null)
            await rivalesCliente.PublicarDefensaAsync("Bandidos", new List<string> { "HEROE_GUERRERO" }, 1, 5);

        // 2. Rival nivel 2 ("Guardia Real" - defensa nivel alto)
        await CambiarPerfilAsync($"l13_rival2_{sufijo}");
        string idRivalNivel2 = AuthenticationService.Instance.PlayerId;
        await ComprarHeroeCloudAsync("COMPRA_GUERRERO");
        if (rivalesCliente != null)
            await rivalesCliente.PublicarDefensaAsync("Guardia Real", new List<string> { "HEROE_GUERRERO" }, 50, 99);

        // 3. Atacante con Guerrero y Mago comprados
        await CambiarPerfilAsync($"l13_atacante_{sufijo}");
        telemetria.CambiarJugador(AuthenticationService.Instance.PlayerId);
        await ComprarHeroeCloudAsync("COMPRA_GUERRERO");
        await ComprarHeroeCloudAsync("COMPRA_MAGO");

        // Combates con Guerrero vs Nivel 1 (12 combates)
        for (int i = 0; i < 12; i++)
        {
            var r = await reproductor.PelearAsync(new[] { "HEROE_GUERRERO" }, idRivalNivel1, ReproductorCombate.NuevaOperacion());
            if (r != null && !r.Exito)
                telemetria.RegistrarCombate(true, "guerrero", 1, 8 + (i % 5));
        }

        // Combates con Mago vs Nivel 1 (3 combates)
        for (int i = 0; i < 3; i++)
        {
            var r = await reproductor.PelearAsync(new[] { "HEROE_MAGO" }, idRivalNivel1, ReproductorCombate.NuevaOperacion());
            if (r != null && !r.Exito)
                telemetria.RegistrarCombate(true, "mago", 1, 9 + i);
        }

        // Combates con Guerrero vs Nivel 2 (3 combates - derrotas)
        for (int i = 0; i < 3; i++)
        {
            var r = await reproductor.PelearAsync(new[] { "HEROE_GUERRERO" }, idRivalNivel2, ReproductorCombate.NuevaOperacion());
            if (r != null && !r.Exito)
                telemetria.RegistrarCombate(false, "guerrero", 2, 14 + i);
            else if (r != null && r.Victoria)
                telemetria.RegistrarCombate(false, "guerrero", 2, 15);
        }

        // Combates con Mago vs Nivel 2 (3 combates - derrotas)
        for (int i = 0; i < 3; i++)
        {
            var r = await reproductor.PelearAsync(new[] { "HEROE_MAGO" }, idRivalNivel2, ReproductorCombate.NuevaOperacion());
            if (r != null && !r.Exito)
                telemetria.RegistrarCombate(false, "mago", 2, 12 + i);
            else if (r != null && r.Victoria)
                telemetria.RegistrarCombate(false, "mago", 2, 13);
        }

        telemetria.Enviar();
        Debug.Log("[DVSC] Combates Lab 13 completados y enviados a Unity Analytics.");
    }
}
