using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace Tienda
{
    // --- 1. CLASES DE MODELO (Construidas desde cero) ---
    public class ResultadoCompra
    {
        public bool Exito { get; set; }
        public bool Repetida { get; set; }
        public string Mensaje { get; set; }
        public string ItemEntregado { get; set; }
        public int NuevoSaldo { get; set; }
    }

    public class ItemCompra
    {
        public string Id { get; set; }
        public string CuestaMoneda { get; set; }
        public int CuestaCantidad { get; set; }
        public string EntregaItem { get; set; }
    }

    public class Catalogo
    {
        public List<ItemCompra> Compras { get; set; } = new List<ItemCompra>();
    }

    public class SaldosDict
    {
        private Dictionary<string, int> _saldos = new Dictionary<string, int>();

        public void Set(string moneda, int cantidad) { _saldos[moneda] = cantidad; }
        public int De(string moneda) { return _saldos.ContainsKey(moneda) ? _saldos[moneda] : 0; }
    }

    public class EstadoProtegido
    {
        public SaldosDict Saldos { get; set; } = new SaldosDict();
        public string LockOro { get; set; } // Nuevo: Candado del saldo

        public List<string> Inventario { get; set; } = new List<string>();
        public string LockInventario { get; set; } // Nuevo: Candado del inventario

        public List<string> Operaciones { get; set; } = new List<string>();
        public string LockOperaciones { get; set; } // Nuevo: Candado de operaciones
    }

    // --- 2. MÓDULO PRINCIPAL ---
    public class ModuloTienda
    {
        private readonly ILogger<ModuloTienda> _logger;

        public ModuloTienda(ILogger<ModuloTienda> logger)
        {
            _logger = logger;
        }

        [CloudCodeFunction("HoraDelServidor")]
        public string HoraDelServidor(IExecutionContext context)
        {
            return $"Hora: {DateTime.UtcNow}, Entorno: {context.EnvironmentName}, PlayerId: {context.PlayerId}";
        }

        // --- FUNCIONES AUXILIARES DE CLEAN CODE ---
        private ResultadoCompra Rechazo(string mensaje)
        {
            _logger.LogWarning($"Compra rechazada: {mensaje}");
            return new ResultadoCompra { Exito = false, Mensaje = mensaje };
        }

        private ResultadoCompra Exito(string item, int nuevoSaldo, string moneda)
        {
            return new ResultadoCompra
            {
                Exito = true,
                Mensaje = $"EXITO: Recibes {item}. Saldo: {nuevoSaldo} {moneda}",
                ItemEntregado = item,
                NuevoSaldo = nuevoSaldo
            };
        }

        // Modificamos para usar IGameApiClient
        private async Task<Catalogo> LeerCatalogoAsync(IExecutionContext context, IGameApiClient gameApiClient)
        {
            var response = await gameApiClient.RemoteConfigSettings.AssignSettingsGetAsync(
                context, context.AccessToken, context.ProjectId, context.EnvironmentId, "settings", new List<string> { "catalogo_economia" }, null);

            string catalogoJson = "{\"compras\":[{\"id\":\"COMPRA_GUERRERO\",\"cuestaMoneda\":\"ORO\",\"cuestaCantidad\":500,\"entregaItem\":\"HEROE_GUERRERO\"},{\"id\":\"COMPRA_MAGO\",\"cuestaMoneda\":\"ORO\",\"cuestaCantidad\":1200,\"entregaItem\":\"HEROE_MAGO\"}]}";

            if (response.Data?.Configs != null)
            {
                JsonNode configsNode = JsonNode.Parse(JsonSerializer.Serialize(response.Data.Configs));
                var str = configsNode?["settings"]?["catalogo_economia"]?.ToString();
                if (!string.IsNullOrEmpty(str)) catalogoJson = str;
            }

            return JsonSerializer.Deserialize<Catalogo>(catalogoJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        // Modificamos para incluir la migración desde la S06 (Ejercicio 5 y 6)
        private async Task<EstadoProtegido> LeerEstadoProtegidoAsync(IExecutionContext context, IGameApiClient gameApiClient, string playerId)
        {
            var keys = new List<string> { "ORO", "inventario", "operaciones" };
            var estado = new EstadoProtegido();
            bool necesitaMigracion = true;

            var responseProtected = await gameApiClient.CloudSaveData.GetProtectedItemsAsync(
                context, context.ServiceToken, context.ProjectId, playerId, keys, null);

            if (responseProtected.Data?.Results != null && responseProtected.Data.Results.Count > 0)
            {
                foreach (var item in responseProtected.Data.Results)
                {
                    if (item.Key == "ORO" && item.Value != null && int.TryParse(item.Value.ToString(), out int val))
                    {
                        estado.Saldos.Set("ORO", val);
                        estado.LockOro = item.WriteLock;
                    }
                    if (item.Key == "inventario" && item.Value != null)
                    {
                        estado.Inventario = JsonSerializer.Deserialize<List<string>>(item.Value.ToString()) ?? new List<string>();
                        estado.LockInventario = item.WriteLock;
                    }
                    if (item.Key == "operaciones" && item.Value != null)
                    {
                        estado.Operaciones = JsonSerializer.Deserialize<List<string>>(item.Value.ToString()) ?? new List<string>();
                        estado.LockOperaciones = item.WriteLock;
                    }
                }
                necesitaMigracion = false;
            }

            if (necesitaMigracion)
            {
                estado.Saldos.Set("ORO", 1000);
                await gameApiClient.CloudSaveData.SetProtectedItemAsync(context, context.ServiceToken, context.ProjectId, playerId, new SetItemBody("ORO", 1000));
            }
            return estado;
        }

        [CloudCodeFunction("ComprarHeroe")]
        public async Task<ResultadoCompra> ComprarHeroe(IExecutionContext context, IGameApiClient gameApiClient, string idCompra, string idOperacion)
        {
            var playerId = context.PlayerId;

            var catalogo = await LeerCatalogoAsync(context, gameApiClient);
            var compra = catalogo.Compras.FirstOrDefault(c => c.Id == idCompra);
            if (compra == null) return Rechazo($"El item {idCompra} no existe en el catálogo.");

            var estado = await LeerEstadoProtegidoAsync(context, gameApiClient, playerId);

            // PASO 9: Idempotencia - Si la operación ya existe, respondemos sin cobrar de nuevo
            if (estado.Operaciones.Contains(idOperacion))
            {
                return new ResultadoCompra
                {
                    Exito = true,
                    Repetida = true,
                    Mensaje = $"IDEMPOTENCIA: Compra ya registrada.",
                    ItemEntregado = compra.EntregaItem,
                    NuevoSaldo = estado.Saldos.De(compra.CuestaMoneda)
                };
            }

            if (estado.Saldos.De(compra.CuestaMoneda) < compra.CuestaCantidad)
                return Rechazo($"Te faltan {compra.CuestaCantidad} {compra.CuestaMoneda} para esta compra.");

            int nuevoSaldo = estado.Saldos.De(compra.CuestaMoneda) - compra.CuestaCantidad;
            estado.Inventario.Add(compra.EntregaItem);
            estado.Operaciones.Add(idOperacion);

            // PASO 6: Transacción Batch con Write Locks
            try
            {
                var cuerpo = new SetItemBatchBody(new List<SetItemBody>
                {
                    new SetItemBody(compra.CuestaMoneda, nuevoSaldo, estado.LockOro),
                    new SetItemBody("inventario", estado.Inventario, estado.LockInventario),
                    new SetItemBody("operaciones", estado.Operaciones, estado.LockOperaciones)
                });

                await gameApiClient.CloudSaveData.SetProtectedItemBatchAsync(
                    context, context.ServiceToken, context.ProjectId, playerId, cuerpo);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("409") || ex.Message.Contains("Conflict"))
                {
                    return Rechazo("vuelve a intentar");
                }
                throw;
            }

            return Exito(compra.EntregaItem, nuevoSaldo, compra.CuestaMoneda);
        }

    }
}