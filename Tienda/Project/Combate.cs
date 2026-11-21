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
    public class UnidadCombate
    {
        public string Id { get; set; }
        public int Vida { get; set; }
        public int Ataque { get; set; }
        public int Defensa { get; set; }
        public int Velocidad { get; set; }
        public bool Viva => Vida > 0;

        public UnidadCombate Clonar() => new UnidadCombate
        {
            Id = Id,
            Vida = Vida,
            Ataque = Ataque,
            Defensa = Defensa,
            Velocidad = Velocidad
        };
    }

    public class EntradaBattleLog
    {
        public int turno { get; set; }
        public string atacante { get; set; }
        public string objetivo { get; set; }
        public int dano { get; set; }
        public int vidaRestante { get; set; }
    }

    public class RecompensaCombate
    {
        public string moneda { get; set; }
        public int cantidad { get; set; }
    }

    public class ResultadoCombate
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; }
        public bool Repeticion { get; set; }
        public bool Victoria { get; set; }
        public int TurnosJugados { get; set; }
        public int Semilla { get; set; }
        public string Rival { get; set; }
        public List<UnidadCombate> EquipoPropio { get; set; } = new List<UnidadCombate>();
        public List<UnidadCombate> EquipoRival { get; set; } = new List<UnidadCombate>();
        public List<EntradaBattleLog> BattleLog { get; set; } = new List<EntradaBattleLog>();
        public RecompensaCombate Recompensa { get; set; }
        public int TrofeosCambio { get; set; }
        public int SaldoResultante { get; set; }
    }

    public class RivalNpc
    {
        public string Id { get; set; }
        public string Nombre { get; set; }
        public List<string> Equipo { get; set; } = new List<string>();
        public string PremioMoneda { get; set; } = "ORO";
        public int PremioCantidad { get; set; }
        public int Trofeos { get; set; }
    }

    public class TablaRivales
    {
        public List<RivalNpc> Rivales { get; set; } = new List<RivalNpc>();
    }

    public class ProgresoJugador
    {
        public int combatesGanados { get; set; }
        public int combatesPerdidos { get; set; }
        public int trofeos { get; set; }
    }

    public class UltimoCombate
    {
        public int semilla { get; set; }
        public string rival { get; set; }
        public bool victoria { get; set; }
        public string fechaUtc { get; set; }
        public List<UnidadCombate> equipoPropio { get; set; } = new List<UnidadCombate>();
        public List<UnidadCombate> equipoRival { get; set; } = new List<UnidadCombate>();
    }

    public class ModuloCombate
    {
        private const int MaxHeroesPorEquipo = 3;
        private const int MaxTurnos = 100;
        private readonly ILogger<ModuloCombate> _logger;

        public ModuloCombate(ILogger<ModuloCombate> logger)
        {
            _logger = logger;
        }

        private ResultadoCombate Rechazo(string mensaje)
        {
            _logger.LogWarning($"Combate rechazado: {mensaje}");
            return new ResultadoCombate { Exito = false, Mensaje = mensaje };
        }

        [CloudCodeFunction("ResolverCombate")]
        public async Task<ResultadoCombate> ResolverCombate(
            IExecutionContext context,
            IGameApiClient gameApiClient,
            List<string> equipo,
            string idRival)
        {
            var playerId = context.PlayerId;

            if (equipo == null || equipo.Count == 0 || equipo.Count > MaxHeroesPorEquipo)
                return Rechazo($"El equipo lleva entre 1 y 3 hÃƒÂ©roes (mandaste {equipo?.Count ?? 0}).");

            var (tablaHeroes, rivales) = await LeerConfiguracionAsync(context, gameApiClient);

            var rivalConfig = rivales.Rivales.FirstOrDefault(r => r.Id == idRival);
            if (rivalConfig == null)
                return Rechazo($"No hay ningÃƒÂºn rival Ã‚Â«{idRival}Ã‚Â».");

            var keys = new List<string> { "ORO", "inventario", "progreso", "ultimo_combate" };
            var respProtected = await gameApiClient.CloudSaveData.GetProtectedItemsAsync(
                context, context.ServiceToken, context.ProjectId, playerId, keys, null);

            int saldoActual = 0;
            string lockOro = null;
            var inventario = new List<string>();
            var progreso = new ProgresoJugador();
            string lockProgreso = null;
            string lockUltimo = null;

            if (respProtected.Data?.Results != null)
            {
                foreach (var item in respProtected.Data.Results)
                {
                    if (item.Key == "ORO" && item.Value != null && int.TryParse(item.Value.ToString(), out int val))
                    {
                        saldoActual = val;
                        lockOro = item.WriteLock;
                    }
                    else if (item.Key == "inventario" && item.Value != null)
                    {
                        inventario = JsonSerializer.Deserialize<List<string>>(item.Value.ToString()) ?? new List<string>();
                    }
                    else if (item.Key == "progreso" && item.Value != null)
                    {
                        progreso = JsonSerializer.Deserialize<ProgresoJugador>(item.Value.ToString(),
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new ProgresoJugador();
                        lockProgreso = item.WriteLock;
                    }
                    else if (item.Key == "ultimo_combate" && item.Value != null)
                    {
                        lockUltimo = item.WriteLock;
                    }
                }
            }

            var propio = ArmarEquipoPropio(equipo, inventario, tablaHeroes, out string faltante);
            if (propio == null)
                return Rechazo($"Ã‚Â«{faltante}Ã‚Â» no estÃƒÂ¡ en tu inventario o no existe en la tabla de hÃƒÂ©roes.");

            var enemigo = ArmarEquipoRival(rivalConfig.Equipo, tablaHeroes);
            if (enemigo.Count == 0)
                return Rechazo($"El equipo del rival Ã‚Â«{idRival}Ã‚Â» no tiene unidades vÃƒÂ¡lidas.");

            int semilla = Random.Shared.Next(1, int.MaxValue);
            var resultado = Simular(propio, enemigo, semilla);
            resultado.Exito = true;
            resultado.Rival = rivalConfig.Nombre;
            resultado.Mensaje = resultado.Victoria
                ? $"Victoria contra Ã‚Â«{rivalConfig.Nombre}Ã‚Â» en {resultado.TurnosJugados} turnos."
                : $"Derrota contra Ã‚Â«{rivalConfig.Nombre}Ã‚Â» en {resultado.TurnosJugados} turnos.";

            int premioGanado = resultado.Victoria ? rivalConfig.PremioCantidad : 0;
            int cambioTrofeos = resultado.Victoria ? rivalConfig.Trofeos : -(rivalConfig.Trofeos / 2);

            saldoActual += premioGanado;
            if (resultado.Victoria)
                progreso.combatesGanados++;
            else
                progreso.combatesPerdidos++;

            progreso.trofeos = Math.Max(0, progreso.trofeos + cambioTrofeos);

            resultado.Recompensa = new RecompensaCombate { moneda = rivalConfig.PremioMoneda, cantidad = premioGanado };
            resultado.TrofeosCambio = cambioTrofeos;
            resultado.SaldoResultante = saldoActual;

            var ultimo = new UltimoCombate
            {
                semilla = semilla,
                rival = rivalConfig.Nombre,
                victoria = resultado.Victoria,
                fechaUtc = DateTime.UtcNow.ToString("o"),
                equipoPropio = propio.Select(u => u.Clonar()).ToList(),
                equipoRival = enemigo.Select(u => u.Clonar()).ToList()
            };

            try
            {
                var cuerpo = new SetItemBatchBody(new List<SetItemBody>
                {
                    new SetItemBody(rivalConfig.PremioMoneda, saldoActual, lockOro),
                    new SetItemBody("progreso", progreso, lockProgreso),
                    new SetItemBody("ultimo_combate", ultimo, lockUltimo)
                });

                await gameApiClient.CloudSaveData.SetProtectedItemBatchAsync(
                    context, context.ServiceToken, context.ProjectId, playerId, cuerpo);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("409") || ex.Message.Contains("Conflict"))
                    return Rechazo("Conflicto de concurrencia, vuelve a intentar.");
                throw;
            }

            return resultado;
        }

        [CloudCodeFunction("RepetirCombate")]
        public async Task<ResultadoCombate> RepetirCombate(IExecutionContext context, IGameApiClient gameApiClient)
        {
            var resp = await gameApiClient.CloudSaveData.GetProtectedItemsAsync(
                context, context.ServiceToken, context.ProjectId, context.PlayerId,
                new List<string> { "ultimo_combate" }, null);

            var item = resp.Data?.Results?.FirstOrDefault(r => r.Key == "ultimo_combate");
            if (item?.Value == null)
                return Rechazo("No tienes ningÃƒÂºn combate previo registrado para repetir.");

            var ultimo = JsonSerializer.Deserialize<UltimoCombate>(item.Value.ToString(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var resultado = Simular(ultimo.equipoPropio, ultimo.equipoRival, ultimo.semilla);
            resultado.Exito = true;
            resultado.Repeticion = true;
            resultado.Rival = ultimo.rival;
            resultado.Mensaje = "RepeticiÃƒÂ³n determinista del ÃƒÂºltimo combate (sin premio ni escritura).";
            return resultado;
        }

        public static ResultadoCombate Simular(List<UnidadCombate> propio, List<UnidadCombate> rival, int semilla)
        {
            var nuestros = propio.Select(u => u.Clonar()).ToList();
            var suyos = rival.Select(u => u.Clonar()).ToList();

            var todos = new List<(UnidadCombate unidad, bool esPropio)>();
            foreach (var u in nuestros) todos.Add((u, true));
            foreach (var u in suyos) todos.Add((u, false));

            var azar = new Random(semilla);
            var orden = todos.OrderByDescending(p => p.unidad.Velocidad).ToList();
            var log = new List<EntradaBattleLog>();
            int turno = 0;

            while (turno < MaxTurnos && nuestros.Any(u => u.Viva) && suyos.Any(u => u.Viva))
            {
                turno++;
                foreach (var (unidad, esPropio) in orden)
                {
                    if (!unidad.Viva) continue;
                    var enemigos = esPropio ? suyos : nuestros;
                    var objetivo = enemigos.FirstOrDefault(u => u.Viva);
                    if (objetivo == null) break;

                    int dano = Math.Max(1, unidad.Ataque + azar.Next(-2, 3) - objetivo.Defensa);
                    objetivo.Vida = Math.Max(0, objetivo.Vida - dano);

                    log.Add(new EntradaBattleLog
                    {
                        turno = turno,
                        atacante = unidad.Id,
                        objetivo = objetivo.Id,
                        dano = dano,
                        vidaRestante = objetivo.Vida
                    });
                }
            }

            bool victoria = nuestros.Any(u => u.Viva) && !suyos.Any(u => u.Viva);

            return new ResultadoCombate
            {
                Victoria = victoria,
                TurnosJugados = turno,
                Semilla = semilla,
                EquipoPropio = propio.Select(u => u.Clonar()).ToList(),
                EquipoRival = rival.Select(u => u.Clonar()).ToList(),
                BattleLog = log
            };
        }

        private List<UnidadCombate> ArmarEquipoPropio(
            List<string> idsSolicitados,
            List<string> inventarioJugador,
            Dictionary<string, UnidadCombate> tabla,
            out string faltante)
        {
            faltante = null;
            var disponibles = new List<string>(inventarioJugador);
            var resultado = new List<UnidadCombate>();

            foreach (var id in idsSolicitados)
            {
                if (!disponibles.Remove(id) || !tabla.TryGetValue(id, out var stats))
                {
                    faltante = id;
                    return null;
                }
                resultado.Add(stats.Clonar());
            }
            return resultado;
        }

        private List<UnidadCombate> ArmarEquipoRival(List<string> idsRival, Dictionary<string, UnidadCombate> tabla)
        {
            var lista = new List<UnidadCombate>();
            foreach (var id in idsRival)
            {
                if (tabla.TryGetValue(id, out var stats))
                    lista.Add(stats.Clonar());
            }
            return lista;
        }

        private async Task<(Dictionary<string, UnidadCombate> tabla, TablaRivales rivales)> LeerConfiguracionAsync(
            IExecutionContext context, IGameApiClient gameApiClient)
        {
            var response = await gameApiClient.RemoteConfigSettings.AssignSettingsGetAsync(
                context, context.AccessToken, context.ProjectId, context.EnvironmentId,
                "settings", new List<string> { "tabla_heroes", "rivales_npc" }, null);

            var tabla = ObtenerTablaDefault();
            var rivales = ObtenerRivalesDefault();

            if (response.Data?.Configs != null)
            {
                JsonNode configsNode = JsonNode.Parse(JsonSerializer.Serialize(response.Data.Configs));
                var settingsNode = configsNode?["settings"] ?? configsNode?["Settings"];

                var rivalesNode = settingsNode?["rivales_npc"];
                if (rivalesNode != null)
                {
                    try
                    {
                        string rawRivales = rivalesNode is JsonValue ? rivalesNode.ToString() : rivalesNode.ToJsonString();
                        var root = JsonNode.Parse(rawRivales);
                        var arr = root?["rivales"]?.AsArray();
                        if (arr != null && arr.Count > 0)
                        {
                            var lista = new List<RivalNpc>();
                            foreach (var item in arr)
                            {
                                if (item is not JsonObject obj) continue;
                                var eq = new List<string>();
                                if (obj["equipo"] is JsonArray eqArr)
                                {
                                    foreach (var h in eqArr)
                                        if (h != null) eq.Add(h.ToString());
                                }
                                lista.Add(new RivalNpc
                                {
                                    Id = obj["id"]?.ToString() ?? "",
                                    Nombre = obj["nombre"]?.ToString() ?? "",
                                    Equipo = eq,
                                    PremioMoneda = obj["premioMoneda"]?.ToString() ?? "ORO",
                                    PremioCantidad = obj["premioCantidad"]?.GetValue<int>() ?? 0,
                                    Trofeos = obj["trofeos"]?.GetValue<int>() ?? 0
                                });
                            }
                            if (lista.Count > 0) rivales.Rivales = lista;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Usando rivales por defecto por formato inesperado en Remote Config: {ex.Message}");
                    }
                }

                var tablaNode = settingsNode?["tabla_heroes"];
                if (tablaNode != null)
                {
                    try
                    {
                        string rawTabla = tablaNode is JsonValue ? tablaNode.ToString() : tablaNode.ToJsonString();
                        var parsed = JsonSerializer.Deserialize<Dictionary<string, UnidadCombate>>(rawTabla,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (parsed != null && parsed.Count > 0)
                        {
                            foreach (var kv in parsed) kv.Value.Id = kv.Key;
                            tabla = parsed;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Usando tabla_heroes por defecto: {ex.Message}");
                    }
                }
            }

            return (tabla, rivales);
        }

        private TablaRivales ObtenerRivalesDefault() => new TablaRivales
        {
            Rivales = new List<RivalNpc>
            {
                new RivalNpc { Id = "bandidos", Nombre = "Bandidos del camino", Equipo = new List<string> { "HEROE_ARQUERA" }, PremioMoneda = "ORO", PremioCantidad = 150, Trofeos = 5 },
                new RivalNpc { Id = "cazadores", Nombre = "Cazadores furtivos", Equipo = new List<string> { "HEROE_GUERRERO", "HEROE_ARQUERA" }, PremioMoneda = "ORO", PremioCantidad = 300, Trofeos = 8 },
                new RivalNpc { Id = "guardia_real", Nombre = "Guardia real", Equipo = new List<string> { "HEROE_GUERRERO", "HEROE_MAGO", "HEROE_ARQUERA" }, PremioMoneda = "ORO", PremioCantidad = 600, Trofeos = 10 }
            }
        };
        private Dictionary<string, UnidadCombate> ObtenerTablaDefault() => new Dictionary<string, UnidadCombate>
        {
            ["HEROE_GUERRERO"] = new UnidadCombate { Id = "HEROE_GUERRERO", Vida = 140, Ataque = 12, Defensa = 9, Velocidad = 8 },
            ["HEROE_ARQUERA"]  = new UnidadCombate { Id = "HEROE_ARQUERA",  Vida = 90,  Ataque = 17, Defensa = 4, Velocidad = 14 },
            ["HEROE_MAGO"]     = new UnidadCombate { Id = "HEROE_MAGO",     Vida = 80,  Ataque = 22, Defensa = 3, Velocidad = 11 }
        };
    }
}