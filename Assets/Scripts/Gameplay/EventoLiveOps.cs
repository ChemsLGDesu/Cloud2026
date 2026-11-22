using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using UnityEngine;

[Serializable]
public class CampanaEventoDto
{
    public bool activo;
    public string id;
    public string nombre;
    public double multiplicadorDano = 1.0;
    public double descuentoTienda;
    public List<string> heroesEspeciales = new List<string>();
    public long segundosRestantes;
    public string tiempoRestanteTexto;
    public string horaServidorUtc;
    public bool esNuevo;
    public string bonusMoneda;
    public int bonusCantidad;
}

public class EventoLiveOps : MonoBehaviour
{
    [SerializeField] private float segundosEntreConsultas = 15f;

    public CampanaEventoDto Campana { get; private set; } = new CampanaEventoDto();
    public bool HayEvento => Campana != null && Campana.activo;
    public event Action<CampanaEventoDto> EventoCambio;

    private float instanteDeLaLectura;
    private CancellationTokenSource cts;
    private bool primeraLecturaLogueada;

    public void Iniciar()
    {
        Detener();
        primeraLecturaLogueada = false;
        cts = new CancellationTokenSource();
        _ = BucleSondeoAsync(cts.Token);
    }

    public void Detener()
    {
        if (cts == null) return;
        cts.Cancel();
        cts.Dispose();
        cts = null;
    }

    private async Task BucleSondeoAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await ActualizarAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[L12] Error al consultar VistaEvento: {ex.Message}");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(segundosEntreConsultas), token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    public async Task<CampanaEventoDto> ActualizarAsync()
    {
        var nueva = await CloudCodeService.Instance.CallModuleEndpointAsync<CampanaEventoDto>(
            "Tienda", "VistaEvento", new Dictionary<string, object>());

        bool habiaEvento = HayEvento;
        Campana = nueva ?? new CampanaEventoDto();
        instanteDeLaLectura = Time.realtimeSinceStartup;

        if (!primeraLecturaLogueada)
        {
            primeraLecturaLogueada = true;
            Debug.Log($"[L12] Estado inicial VistaEvento → activo={Campana.activo}, nombre=«{Campana.nombre}», horaUtc={Campana.horaServidorUtc}, quedan={Contador()}");
        }

        if (habiaEvento != HayEvento)
            EventoCambio?.Invoke(Campana);

        return Campana;
    }

    public long SegundosRestantesAhora()
    {
        if (!HayEvento) return 0;
        long transcurrido = (long)(Time.realtimeSinceStartup - instanteDeLaLectura);
        return Math.Max(0, Campana.segundosRestantes - transcurrido);
    }

    public string Contador()
    {
        if (!HayEvento) return "apagado";
        var ts = TimeSpan.FromSeconds(SegundosRestantesAhora());
        return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
    }

    public long PrecioMostrado(string item, long precioBase)
    {
        if (!HayEvento || Campana.descuentoTienda <= 0 ||
            Campana.heroesEspeciales == null || !Campana.heroesEspeciales.Contains(item))
            return precioBase;

        return (long)Math.Round(
            precioBase * (1.0 - Math.Min(1.0, Campana.descuentoTienda)),
            MidpointRounding.AwayFromZero);
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused && cts != null)
            _ = ActualizarAsync();
    }

    private void OnDestroy() => Detener();
}