using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace Tienda
{
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

    public class HeroeInventario
    {
        public string id { get; set; }
        public int nivel { get; set; } = 1;
        public int experiencia { get; set; } = 0;
    }

    public class InventarioData
    {
        public List<HeroeInventario> heroes { get; set; } = new List<HeroeInventario>();
        public List<string> objetos { get; set; } = new List<string>();
    }

    public class SaldosData
    {
        public Dictionary<string, int> monedas { get; set; } = new Dictionary<string, int>
        {
            { "ORO", 1000 },
            { "GEMAS", 0 }
        };

        public int De(string moneda) => monedas.TryGetValue(moneda, out int v) ? v : 0;
        public void Poner(string moneda, int cantidad) => monedas[moneda] = Math.Max(0, cantidad);
    }

    public class PerfilData
    {
        public int nivelCuenta { get; set; } = 1;
        public int experiencia { get; set; } = 0;
    }

    public class ProgresoData
    {
        public int combatesGanados { get; set; }
        public int combatesPerdidos { get; set; }
        public int trofeos { get; set; }
        public List<string> desbloqueos { get; set; } = new List<string>();
    }

    public class OperacionesData
    {
        public List<string> ids { get; set; } = new List<string>();
    }

    public class EstadoCompleto
    {
        public SaldosData Saldos { get; set; } = new SaldosData();
        public string LockSaldos { get; set; }

        public InventarioData Inventario { get; set; } = new InventarioData();
        public string LockInventario { get; set; }

        public ProgresoData Progreso { get; set; } = new ProgresoData();
        public string LockProgreso { get; set; }

        public PerfilData Perfil { get; set; } = new PerfilData();
        public string LockPerfil { get; set; }

        public OperacionesData Operaciones { get; set; } = new OperacionesData();
        public string LockOperaciones { get; set; }

        public UltimoCombate UltimoCombate { get; set; }
        public string LockUltimoCombate { get; set; }
    }

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

        [CloudCodeFunction("LeerEstado")]
        public async Task<EstadoCompleto> LeerEstado(IExecutionContext context, IGameApiClient gameApiClient)
        {
            return await LeerEstadoCompletoAsync(context, gameApiClient, context.PlayerId);
        }

        private ResultadoCompra Rechazo(string mensaje)
        {
            _logger.LogWarning($"Compra rechazada: {mensaje}");
            return new ResultadoCompra { Exito = false, Mensaje = mensaje };
        }

        private async Task<Catalogo> LeerCatalogoAsync(IExecutionContext context, IGameApiClient gameApiClient)
        {
            var defaultCatalogo = new Catalogo
            {
                Compras = new List<ItemCompra>
                {
                    new ItemCompra { Id = "COMPRA_GUERRERO", CuestaMoneda = "ORO", CuestaCantidad = 500, EntregaItem = "HEROE_GUERRERO" },
                    new ItemCompra { Id = "COMPRA_ARQUERA", CuestaMoneda = "ORO", CuestaCantidad = 800, EntregaItem = "HEROE_ARQUERA" },
                    new ItemCompra { Id = "COMPRA_MAGO", CuestaMoneda = "ORO", CuestaCantidad = 1200, EntregaItem = "HEROE_MAGO" }
                }
            };

            try
            {
                var response = await gameApiClient.RemoteConfigSettings.AssignSettingsGetAsync(
                    context, context.AccessToken, context.ProjectId, context.EnvironmentId,
                    "settings", new List<string> { "catalogo_economia" }, null);

                if (response.Data?.Configs != null)
                {
                    JsonNode configsNode = JsonNode.Parse(JsonSerializer.Serialize(response.Data.Configs));
                    var settingsNode = configsNode?["settings"] ?? configsNode?["Settings"];
                    var catNode = settingsNode?["catalogo_economia"];
                    if (catNode != null)
                    {
                        string str = catNode is JsonValue ? catNode.ToString() : catNode.ToJsonString();
                        var root = JsonNode.Parse(str);
                        var arr = root?["compras"]?.AsArray();
                        if (arr != null && arr.Count > 0)
                        {
                            var lista = new List<ItemCompra>();
                            foreach (var el in arr)
                            {
                                if (el is JsonObject obj)
                                {
                                    string id = obj["id"]?.ToString() ?? "";
                                    string moneda = obj["cuestaMoneda"]?.ToString() ?? obj["moneda"]?.ToString() ?? "ORO";
                                    int cant = obj["cuestaCantidad"]?.GetValue<int>() ?? obj["precio"]?.GetValue<int>() ?? 0;
                                    string entrega = obj["entregaItem"]?.ToString() ?? obj["item"]?.ToString() ?? id.Replace("COMPRA_", "HEROE_");
                                    if (!string.IsNullOrEmpty(id))
                                        lista.Add(new ItemCompra { Id = id, CuestaMoneda = moneda, CuestaCantidad = cant, EntregaItem = entrega });
                                }
                            }
                            if (lista.Count > 0) return new Catalogo { Compras = lista };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Usando catálogo por defecto: {ex.Message}");
            }

            return defaultCatalogo;
        }

        public static async Task<EstadoCompleto> LeerEstadoCompletoAsync(IExecutionContext context, IGameApiClient gameApiClient, string playerId)
        {
            var keys = new List<string> { "saldos", "ORO", "inventario", "progreso", "perfil", "operaciones", "ultimo_combate" };
            var estado = new EstadoCompleto();
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var resp = await gameApiClient.CloudSaveData.GetProtectedItemsAsync(
                context, context.ServiceToken, context.ProjectId, playerId, keys, null);

            bool tieneSaldos = false;
            int oroLegacy = 1000;

            if (resp.Data?.Results != null)
            {
                foreach (var item in resp.Data.Results)
                {
                    if (item.Value == null) continue;
                    string raw = item.Value.ToString();

                    if (item.Key == "saldos")
                    {
                        var parsed = JsonSerializer.Deserialize<SaldosData>(raw, opts);
                        if (parsed != null)
                        {
                            estado.Saldos = parsed;
                            if (!estado.Saldos.monedas.ContainsKey("ORO")) estado.Saldos.monedas["ORO"] = 0;
                            if (!estado.Saldos.monedas.ContainsKey("GEMAS")) estado.Saldos.monedas["GEMAS"] = 0;
                            estado.LockSaldos = item.WriteLock;
                            tieneSaldos = true;
                        }
                    }
                    else if (item.Key == "ORO" && int.TryParse(raw, out int valOro))
                    {
                        oroLegacy = valOro;
                    }
                    else if (item.Key == "inventario")
                    {
                        var node = JsonNode.Parse(raw);
                        if (node is JsonArray arr)
                        {
                            foreach (var h in arr)
                                if (h != null) estado.Inventario.heroes.Add(new HeroeInventario { id = h.ToString(), nivel = 1, experiencia = 0 });
                        }
                        else
                        {
                            estado.Inventario = JsonSerializer.Deserialize<InventarioData>(raw, opts) ?? new InventarioData();
                        }
                        estado.LockInventario = item.WriteLock;
                    }
                    else if (item.Key == "progreso")
                    {
                        estado.Progreso = JsonSerializer.Deserialize<ProgresoData>(raw, opts) ?? new ProgresoData();
                        estado.LockProgreso = item.WriteLock;
                    }
                    else if (item.Key == "perfil")
                    {
                        estado.Perfil = JsonSerializer.Deserialize<PerfilData>(raw, opts) ?? new PerfilData();
                        if (estado.Perfil.nivelCuenta < 1) estado.Perfil.nivelCuenta = 1;
                        estado.LockPerfil = item.WriteLock;
                    }
                    else if (item.Key == "operaciones")
                    {
                        var node = JsonNode.Parse(raw);
                        if (node is JsonArray arr)
                        {
                            foreach (var op in arr)
                                if (op != null) estado.Operaciones.ids.Add(op.ToString());
                        }
                        else
                        {
                            estado.Operaciones = JsonSerializer.Deserialize<OperacionesData>(raw, opts) ?? new OperacionesData();
                        }
                        estado.LockOperaciones = item.WriteLock;
                    }
                    else if (item.Key == "ultimo_combate")
                    {
                        estado.UltimoCombate = JsonSerializer.Deserialize<UltimoCombate>(raw, opts);
                        estado.LockUltimoCombate = item.WriteLock;
                    }
                }
            }

            if (!tieneSaldos)
                estado.Saldos.Poner("ORO", oroLegacy);

            return estado;
        }

        public static async Task EscribirEstadoCompletoAsync(IExecutionContext context, IGameApiClient gameApiClient, string playerId, EstadoCompleto estado)
        {
            if (estado.Operaciones.ids.Count > 20)
                estado.Operaciones.ids = estado.Operaciones.ids.Skip(estado.Operaciones.ids.Count - 20).ToList();

            var batch = new List<SetItemBody>
            {
                new SetItemBody("saldos", estado.Saldos, estado.LockSaldos),
                new SetItemBody("inventario", estado.Inventario, estado.LockInventario),
                new SetItemBody("progreso", estado.Progreso, estado.LockProgreso),
                new SetItemBody("perfil", estado.Perfil, estado.LockPerfil),
                new SetItemBody("operaciones", estado.Operaciones, estado.LockOperaciones)
            };

            if (estado.UltimoCombate != null)
                batch.Add(new SetItemBody("ultimo_combate", estado.UltimoCombate, estado.LockUltimoCombate));

            await gameApiClient.CloudSaveData.SetProtectedItemBatchAsync(
                context, context.ServiceToken, context.ProjectId, playerId, new SetItemBatchBody(batch));
        }

        [CloudCodeFunction("ComprarHeroe")]
        public async Task<ResultadoCompra> ComprarHeroe(IExecutionContext context, IGameApiClient gameApiClient, string idCompra, string idOperacion)
        {
            var playerId = context.PlayerId;
            var catalogo = await LeerCatalogoAsync(context, gameApiClient);
            var compra = catalogo.Compras.FirstOrDefault(c => c.Id == idCompra);
            if (compra == null) return Rechazo($"El item {idCompra} no existe en el catÃ¡logo.");

            var estado = await LeerEstadoCompletoAsync(context, gameApiClient, playerId);

            if (!string.IsNullOrWhiteSpace(idOperacion) && estado.Operaciones.ids.Contains(idOperacion))
            {
                return new ResultadoCompra
                {
                    Exito = true,
                    Repetida = true,
                    Mensaje = "IDEMPOTENCIA: Compra ya registrada.",
                    ItemEntregado = compra.EntregaItem,
                    NuevoSaldo = estado.Saldos.De(compra.CuestaMoneda)
                };
            }

            if (estado.Saldos.De(compra.CuestaMoneda) < compra.CuestaCantidad)
                return Rechazo($"Te faltan {compra.CuestaCantidad} {compra.CuestaMoneda} para esta compra.");

            int nuevoSaldo = estado.Saldos.De(compra.CuestaMoneda) - compra.CuestaCantidad;
            estado.Saldos.Poner(compra.CuestaMoneda, nuevoSaldo);
            estado.Inventario.heroes.Add(new HeroeInventario { id = compra.EntregaItem, nivel = 1, experiencia = 0 });
            if (!string.IsNullOrWhiteSpace(idOperacion))
                estado.Operaciones.ids.Add(idOperacion);

            try
            {
                await EscribirEstadoCompletoAsync(context, gameApiClient, playerId, estado);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("409") || ex.Message.Contains("Conflict"))
                    return Rechazo("vuelve a intentar");
                throw;
            }

            return new ResultadoCompra
            {
                Exito = true,
                Mensaje = $"EXITO: Recibes {compra.EntregaItem}. Saldo: {nuevoSaldo} {compra.CuestaMoneda}",
                ItemEntregado = compra.EntregaItem,
                NuevoSaldo = nuevoSaldo
            };
        }
    }
}