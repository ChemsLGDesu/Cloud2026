using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using UnityEngine;

public class VerificadorLab09 : MonoBehaviour
{
    [SerializeField] private RivalesCliente rivalesCliente;
    [SerializeField] private ReproductorCombate reproductor;

    private async void Start()
    {
        if (rivalesCliente == null) rivalesCliente = GetComponent<RivalesCliente>();
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

        string sufijo = Guid.NewGuid().ToString("N").Substring(0, 6);

        // --- JUGADOR B: Bruno (nivel 21, compra y publica HEROE_GUERRERO) ---
        await CambiarPerfilAsync($"lab09_B_{sufijo}");
        string idB = AuthenticationService.Instance.PlayerId;
        Debug.Log($"[L09] ▶ jugador B = {idB}");
        await ComprarGuerreroAsync();
        await rivalesCliente.PublicarDefensaAsync("Bruno", new List<string> { "HEROE_GUERRERO" }, 21, 15);

        // --- JUGADOR C: Carla (nivel 28, compra y publica HEROE_GUERRERO) ---
        await CambiarPerfilAsync($"lab09_C_{sufijo}");
        string idC = AuthenticationService.Instance.PlayerId;
        Debug.Log($"[L09] ▶ jugador C = {idC}");
        await ComprarGuerreroAsync();
        await rivalesCliente.PublicarDefensaAsync("Carla", new List<string> { "HEROE_GUERRERO" }, 28, 30);

        // --- JUGADOR D: Dana (nivel 20, publica defensa vacía []) ---
        await CambiarPerfilAsync($"lab09_D_{sufijo}");
        string idD = AuthenticationService.Instance.PlayerId;
        Debug.Log($"[L09] ▶ jugador D = {idD}");
        await rivalesCliente.PublicarDefensaAsync("Dana", new List<string>(), 20, 0);

        // --- JUGADOR A: Ana (nivel 20, compra y publica HEROE_GUERRERO) ---
        await CambiarPerfilAsync($"lab09_A_{sufijo}");
        string idA = AuthenticationService.Instance.PlayerId;
        Debug.Log($"[L09] ▶ jugador A = {idA}");
        await ComprarGuerreroAsync();
        await rivalesCliente.PublicarDefensaAsync("Ana", new List<string> { "HEROE_GUERRERO" }, 20, 10);

        // Paso 2: A lee la defensa pública de B y prueba escribir en ella (debe dar HTTP 403)
        var defB = await rivalesCliente.LeerDefensaDeAsync(idB);
        Debug.Log($"[L09] A lee la defensa pública de B: «{defB.nombre}», nivel {defB.nivelCuenta}, equipo [{string.Join(", ", defB.equipo)}]");

        var (httpCode, httpBody) = await rivalesCliente.IntentarEscribirDefensaAjenaAsync(idB);
        Debug.LogWarning($"[L09] escribir la defensa pública de B con el token de A (POST crudo) → HTTP {httpCode}: {httpBody}");

        // Pasos 3 a 5: Búsqueda de rival a nivel 20 (franja ±2, excluye a A)
        var busqueda20 = await rivalesCliente.BuscarRivalAsync();
        Debug.Log($"[L09] BuscarRival (A, nivel {busqueda20.NivelSolicitante}) → hubo {busqueda20.Hubo} · «{busqueda20.Rival?.Nombre}» nivel {busqueda20.Rival?.Nivel} · franja ±{busqueda20.Franja} · candidatos {busqueda20.Candidatos} · consultas {busqueda20.Consultas} · lecturas {busqueda20.LecturasConsumidas}");
        Debug.Log($"[L09] ¿me excluyó a mí? {(busqueda20.Rival?.PlayerId != idA ? "SÍ" : "NO")} · rival elegido: {busqueda20.Rival?.Nombre}");

        // Paso 6: Pelear contra la defensa real de Bruno (idB)
        var peleaB = await reproductor.PelearAsync(new[] { "HEROE_GUERRERO" }, idB);
        Debug.Log($"[L09] ResolverCombate(A vs B «{peleaB.Rival}») → exito {peleaB.Exito}: {peleaB.Mensaje} +{peleaB.Recompensa?.cantidad} {peleaB.Recompensa?.moneda} · trofeos +{peleaB.TrofeosCambio} · saldo {peleaB.SaldoResultante}");

        // Paso 8 (caso límite 1): Pelear contra Dana (defensa vacía)
        var peleaVacia = await reproductor.PelearAsync(new[] { "HEROE_GUERRERO" }, idD);
        Debug.Log($"[L09] ResolverCombate(A vs D «Dana», defensa vacía) → exito {peleaVacia.Exito}: «{peleaVacia.Mensaje}»");

        // Paso 8 (caso límite 2) y Paso 5: Subir A a nivel 50 y buscar sin nadie en ±10
        await rivalesCliente.PublicarDefensaAsync("Ana", new List<string> { "HEROE_GUERRERO" }, 50, 20);
        var busqueda50 = await rivalesCliente.BuscarRivalAsync();
        Debug.Log($"[L09] BuscarRival (A, nivel {busqueda50.NivelSolicitante}) → hubo {busqueda50.Hubo}: «{busqueda50.Mensaje}» · franja ±{busqueda50.Franja} · candidatos {busqueda50.Candidatos} · consultas {busqueda50.Consultas} · lecturas {busqueda50.LecturasConsumidas}");

        // Paso 8 (trampa 1): B publica HEROE_COLOSO sin tenerlo en su inventario protegido
        await CambiarPerfilAsync($"lab09_B_{sufijo}");
        await rivalesCliente.PublicarDefensaAsync("Bruno", new List<string> { "HEROE_COLOSO", "HEROE_MAGO" }, 21, 15);

        await CambiarPerfilAsync($"lab09_A_{sufijo}");
        var trampaInventario = await reproductor.PelearAsync(new[] { "HEROE_GUERRERO" }, idB);
        Debug.LogWarning($"[L09] trampa: B publica coloso + mago sin tenerlos → exito {trampaInventario.Exito}: «{trampaInventario.Mensaje}»");

        // Paso 8 (trampa 2): A intenta pelear contra su propia defensa
        var trampaPropio = await reproductor.PelearAsync(new[] { "HEROE_GUERRERO" }, idA);
        Debug.LogWarning($"[L09] trampa: A contra su propia defensa → exito {trampaPropio.Exito}: «{trampaPropio.Mensaje}»");
    }

    private async Task CambiarPerfilAsync(string perfil)
    {
        if (AuthenticationService.Instance.IsSignedIn)
            AuthenticationService.Instance.SignOut(false);

        AuthenticationService.Instance.SwitchProfile(perfil);
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    private async Task ComprarGuerreroAsync()
    {
        await CloudCodeService.Instance.CallModuleEndpointAsync<object>(
            "Tienda", "ComprarHeroe",
            new Dictionary<string, object>
            {
                { "idCompra", "COMPRA_GUERRERO" },
                { "idOperacion", Guid.NewGuid().ToString() }
            });
        Debug.Log("[L09] ComprarHeroe(COMPRA_GUERRERO) → HECHA");
    }
}