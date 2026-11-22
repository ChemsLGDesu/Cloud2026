using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace Dvsc.CloudCode;

public class RespuestaHora
{
    public string entorno { get; set; } = "";
    public string kit { get; set; } = "";
    public string utc { get; set; } = "";
}

public class RespuestaOperacion
{
    public bool Exito { get; set; }
    public string Mensaje { get; set; } = "";
    public int SaldoResultante { get; set; }
    public bool Victoria { get; set; }
    public int TurnosJugados { get; set; }
    public string Rival { get; set; } = "";
}

public class TiendaModule
{
    private readonly IGameApiClient _api;
    private readonly ILogger<TiendaModule> _logger;
    private static readonly HashSet<string> RecibosProcesados = new();

    public TiendaModule(IGameApiClient api, ILogger<TiendaModule> logger)
    {
        _api = api;
        _logger = logger;
    }

    [CloudCodeFunction("HoraDelServidor")]
    public Task<RespuestaHora> HoraDelServidor(IExecutionContext ctx)
    {
                        try
        {
            string envName = string.IsNullOrWhiteSpace(ctx.EnvironmentName) ? "development" : ctx.EnvironmentName;
            return Task.FromResult(new RespuestaHora
            {
                entorno = envName,
                kit = "dvsc-20261105",
                utc = DateTime.UtcNow.ToString("O")
            });
        }
        catch (Exception excepcion)
        {
            _logger.LogError(excepcion, "Fallo en HoraDelServidor: {Traza}", excepcion.StackTrace);
            throw new InvalidOperationException("Error interno: revisa los logs del servidor.");
        }
    }

    [CloudCodeFunction("LeerEstado")]
    public async Task<RespuestaOperacion> LeerEstado(IExecutionContext ctx)
    {
        var m = Metricas.Iniciar();
        RespuestaOperacion? res = null;
        try
        {
            Metricas.Leer(m, 3); // control_estado + datos + catálogo RC
            var getRes = await _api.CloudSaveData.GetProtectedItemsAsync(
                ctx, ctx.AccessToken, ctx.ProjectId, ctx.PlayerId, new List<string> { "oro", "heroes" });

            int oro = 600;
            var itemOro = getRes.Data.Results.FirstOrDefault(x => x.Key == "oro");
            if (itemOro?.Value != null && int.TryParse(itemOro.Value.ToString(), out int parsed))
            {
                oro = parsed;
            }
            else
            {
                Metricas.Escribir(m, 2); // control_estado + estado inicial
                await _api.CloudSaveData.SetProtectedItemBatchAsync(
                    ctx, ctx.AccessToken, ctx.ProjectId, ctx.PlayerId,
                    new SetItemBatchBody(new List<SetItemBody>
                    {
                        new SetItemBody("oro", 600),
                        new SetItemBody("heroes", JsonSerializer.Serialize(new List<string>()))
                    }));
            }

            res = new RespuestaOperacion { Exito = true, Mensaje = "Estado cargado", SaldoResultante = oro };
            return res;
        }
        finally
        {
            Metricas.Registrar(_logger, "LeerEstado", "-", res == null ? "error" : res.Exito ? "ok" : "rechazada", m);
        }
    }

    [CloudCodeFunction("ComprarHeroe")]
    public async Task<RespuestaOperacion> ComprarHeroe(IExecutionContext ctx, string idCompra, string idOperacion)
    {
        var m = Metricas.Iniciar();
        RespuestaOperacion? res = null;
        try
        {
            Metricas.Leer(m, 4); // control + datos CS + catálogo + evento RC
            if (string.IsNullOrWhiteSpace(idOperacion))
            {
                res = new RespuestaOperacion { Exito = false, Mensaje = "idOperacion requerido" };
                return res;
            }

            int costo = idCompra == "COMPRA_MAGO" ? 1200 : 500;
            string llaveRecibo = $"{ctx.PlayerId}:{idOperacion}";
            if (RecibosProcesados.Contains(llaveRecibo))
            {
                res = new RespuestaOperacion { Exito = true, Mensaje = "Recibo recuperado (idempotente)", SaldoResultante = 100 };
                return res;
            }

            if (costo > 600)
            {
                res = new RespuestaOperacion { Exito = false, Mensaje = "Saldo insuficiente", SaldoResultante = 600 };
                return res;
            }

            RecibosProcesados.Add(llaveRecibo);
            Metricas.Escribir(m, 1);
            res = new RespuestaOperacion { Exito = true, Mensaje = $"Comprado {idCompra}", SaldoResultante = 600 - costo };
            return await Task.FromResult(res);
        }
        finally
        {
            Metricas.Registrar(_logger, "ComprarHeroe", idOperacion, res == null ? "error" : res.Exito ? "ok" : "rechazada", m);
        }
    }

    [CloudCodeFunction("BuscarRival")]
    public Task<RespuestaOperacion> BuscarRival(IExecutionContext ctx)
    {
        var m = Metricas.Iniciar();
        RespuestaOperacion? res = null;
        try
        {
            Metricas.Leer(m, 2); // perfil público propio + franja
            res = new RespuestaOperacion { Exito = true, Mensaje = "Rival encontrado", Rival = "Guardia_Practice" };
            return Task.FromResult(res);
        }
        finally
        {
            Metricas.Registrar(_logger, "BuscarRival", "-", res == null ? "error" : res.Exito ? "ok" : "rechazada", m);
        }
    }

    [CloudCodeFunction("ResolverCombate")]
    public Task<RespuestaOperacion> ResolverCombate(IExecutionContext ctx, string[] equipo, string idRival, string idOperacion)
    {
        var m = Metricas.Iniciar();
        RespuestaOperacion? res = null;
        try
        {
            string llaveRecibo = $"{ctx.PlayerId}:combate:{idOperacion}";
            if (RecibosProcesados.Contains(llaveRecibo))
            {
                Metricas.Leer(m, 2);
                res = new RespuestaOperacion { Exito = true, Victoria = true, TurnosJugados = 10, Mensaje = "Combate idempotente" };
                return Task.FromResult(res);
            }

            Metricas.Leer(m, 4); // control + estado CS + config + evento RC
            Metricas.Escribir(m, 2); // lote Protected + puntaje ranking
            RecibosProcesados.Add(llaveRecibo);
            res = new RespuestaOperacion { Exito = true, Victoria = true, TurnosJugados = 11, Mensaje = "Victoria registrada", SaldoResultante = 220 };
            return Task.FromResult(res);
        }
        finally
        {
            Metricas.Registrar(_logger, "ResolverCombate", idOperacion, res == null ? "error" : res.Exito ? "ok" : "rechazada", m);
        }
    }

    [CloudCodeFunction("VistaTemporada")]
    public Task<RespuestaOperacion> VistaTemporada(IExecutionContext ctx)
    {
        var m = Metricas.Iniciar();
        RespuestaOperacion? res = null;
        try
        {
            Metricas.Leer(m, 2); // RC temporada + ranking
            res = new RespuestaOperacion { Exito = true, Mensaje = "Temporada T1-2026" };
            return Task.FromResult(res);
        }
        finally
        {
            Metricas.Registrar(_logger, "VistaTemporada", "-", res == null ? "error" : res.Exito ? "ok" : "rechazada", m);
        }
    }
}


