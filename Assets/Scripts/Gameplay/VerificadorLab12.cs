using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using UnityEngine;

[Serializable]
public class ResultadoBonusDto
{
    public bool exito;
    public string mensaje;
    public int cantidadEntregada;
    public int nuevoSaldo;
}

public class VerificadorLab12 : MonoBehaviour
{
    [SerializeField] private EventoLiveOps eventoLiveOps;
    [SerializeField] private ReproductorCombate reproductor;
    [SerializeField] private bool ejecutarPruebasCuentasAlActivar = true;

    private bool pruebasCuentasEjecutadas;

    private async void Start()
    {
        if (eventoLiveOps == null) eventoLiveOps = GetComponent<EventoLiveOps>();
        if (reproductor == null) reproductor = GetComponent<ReproductorCombate>();

        int intentos = 0;
        while ((UnityServices.State != ServicesInitializationState.Initialized ||
                !AuthenticationService.Instance.IsSignedIn) && intentos < 30)
        {
            await Task.Delay(250);
            intentos++;
        }

        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        eventoLiveOps.EventoCambio += OnEventoCambio;
        eventoLiveOps.Iniciar();
        Debug.Log("[L12] Monitor de evento iniciado (consulta cada 15 s)...");
    }

    private async void OnEventoCambio(CampanaEventoDto ev)
    {
        if (ev.activo)
        {
            Debug.Log($"[L12] >>> CAMBIO: el evento APARECIÓ («{ev.nombre}») a las {ev.horaServidorUtc} · quedan {eventoLiveOps.Contador()}");
            if (ejecutarPruebasCuentasAlActivar && !pruebasCuentasEjecutadas)
            {
                pruebasCuentasEjecutadas = true;
                await ProbarCuentasDuranteEventoAsync();
            }
        }
        else
        {
            Debug.Log($"[L12] >>> CAMBIO: el evento DESAPARECIÓ a las {ev.horaServidorUtc}");
        }
    }

    private async Task ProbarCuentasDuranteEventoAsync()
    {
        string sufijo = UnityEngine.Random.Range(10000, 99999).ToString();

        // 1. Cuenta nueva (Nuria): ve descuento Mago 840, daño x1.25, repetición idéntica y bonus 150 ORO
        await CambiarPerfilAsync($"L12_Nuria_{sufijo}");
        var vistaNuria = await eventoLiveOps.ActualizarAsync();
        long precioMago = eventoLiveOps.PrecioMostrado("HEROE_MAGO", 1200);
        Debug.Log($"[L12] Nuria (nueva): VistaEvento → «{vistaNuria.nombre}» activo: daño ×{vistaNuria.multiplicadorDano}, tienda -{vistaNuria.descuentoTienda * 100:0} % (Mago={precioMago} ORO), quedan {eventoLiveOps.Contador()}. Jugador nuevo={vistaNuria.esNuevo}: bienvenida de {vistaNuria.bonusCantidad} {vistaNuria.bonusMoneda}.");

        var bonus1 = await ReclamarBonusAsync();
        Debug.Log($"[L12] Nuria: bonus de bienvenida → {(bonus1.exito ? "HECHO" : "rechazado")} · {bonus1.mensaje} · ORO {bonus1.nuevoSaldo}");

        var bonus2 = await ReclamarBonusAsync();
        Debug.Log($"[L12] Nuria: segundo intento bonus → {(bonus2.exito ? "HECHO" : "rechazado")} · {bonus2.mensaje}");

        var combNuria = await reproductor.PelearAsync(new[] { "HEROE_GUERRERO" }, "bandidos", ReproductorCombate.NuevaOperacion());
        int atkEscalado = combNuria.EquipoPropio != null && combNuria.EquipoPropio.Count > 0 ? combNuria.EquipoPropio[0].ataque : 0;
        Debug.Log($"[L12] Nuria vs bandidos: victoria={combNuria.Victoria} en {combNuria.TurnosJugados} turnos · ataque propio {atkEscalado} · cuenta nivel {combNuria.NivelCuenta}");

        var repNuria = await CloudCodeService.Instance.CallModuleEndpointAsync<ResultadoCombateDto>(
            "Tienda", "RepetirCombate", new Dictionary<string, object>());
        int atkRep = repNuria.EquipoPropio != null && repNuria.EquipoPropio.Count > 0 ? repNuria.EquipoPropio[0].ataque : 0;
        Debug.Log($"[L12] Nuria repite el combate: IDÉNTICO (ataque {atkRep}, turnos {repNuria.TurnosJugados})");

        // 2. Cuenta nivel 2 (Pablo): ya peleó y subió de nivel, bonus rechazado
        await CambiarPerfilAsync($"L12_Pablo_{sufijo}");
        await reproductor.PelearAsync(new[] { "HEROE_GUERRERO" }, "bandidos", ReproductorCombate.NuevaOperacion());
        var vistaPablo = await eventoLiveOps.ActualizarAsync();
        var bonusPablo = await ReclamarBonusAsync();
        Debug.Log($"[L12] Pablo (nivel 2, nuevo={vistaPablo.esNuevo}): bonus → {(bonusPablo.exito ? "HECHO" : "rechazado")} · {bonusPablo.mensaje}");
    }

    private async Task CambiarPerfilAsync(string perfil)
    {
        if (AuthenticationService.Instance.IsSignedIn)
            AuthenticationService.Instance.SignOut(false);
        AuthenticationService.Instance.SwitchProfile(perfil);
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    private async Task<ResultadoBonusDto> ReclamarBonusAsync()
    {
        return await CloudCodeService.Instance.CallModuleEndpointAsync<ResultadoBonusDto>(
            "Tienda", "ReclamarBonusBienvenida", new Dictionary<string, object>());
    }
}