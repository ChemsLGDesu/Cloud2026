using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

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
        public bool Repetida { get; set; }
        public bool Victoria { get; set; }
        public int TurnosJugados { get; set; }
        public int Semilla { get; set; }
        public string Rival { get; set; }
        public List<UnidadCombate> EquipoPropio { get; set; } = new List<UnidadCombate>();
        public List<UnidadCombate> EquipoRival { get; set; } = new List<UnidadCombate>();
        public List<int> VidaFinalPropio { get; set; } = new List<int>();
        public List<EntradaBattleLog> BattleLog { get; set; } = new List<EntradaBattleLog>();
        public RecompensaCombate Recompensa { get; set; }
        public int TrofeosCambio { get; set; }
        public int PuestoEnRanking { get; set; }
        public int SaldoResultante { get; set; }
        public int XpGanada { get; set; }
        public int NivelCuenta { get; set; }
        public int ExperienciaCuenta { get; set; }
        public List<SubidaNivelHeroe> Subieron { get; set; } = new List<SubidaNivelHeroe>();
        public List<PremioBotin> Botin { get; set; } = new List<PremioBotin>();
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

    public class UltimoCombate
    {
        public string idOperacion { get; set; }
        public int semilla { get; set; }
        public string rival { get; set; }
        public bool victoria { get; set; }
        public string fechaUtc { get; set; }
        public List<UnidadCombate> equipoPropio { get; set; } = new List<UnidadCombate>();
        public List<UnidadCombate> equipoRival { get; set; } = new List<UnidadCombate>();
        public int xpGanada { get; set; }
        public int nivelCuenta { get; set; }
        public int experienciaCuenta { get; set; }
        public List<SubidaNivelHeroe> subieron { get; set; } = new List<SubidaNivelHeroe>();
        public List<PremioBotin> botin { get; set; } = new List<PremioBotin>();
        public RecompensaCombate recompensa { get; set; }
        public int trofeosCambio { get; set; }
        public int saldoResultante { get; set; }
    }

    public partial class ModuloCombate
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

        private ResultadoCombate RespuestaRepetida(EstadoCompleto estado, string idOperacion)
        {
            var ult = estado.UltimoCombate;
            if (ult != null && ult.idOperacion == idOperacion && ult.equipoPropio.Count > 0)
            {
                var sim = Simular(ult.equipoPropio, ult.equipoRival, ult.semilla);
                sim.Exito = true;
                sim.Repetida = true;
                sim.Rival = ult.rival;
                sim.XpGanada = ult.xpGanada;
                sim.NivelCuenta = ult.nivelCuenta;
                sim.ExperienciaCuenta = ult.experienciaCuenta;
                sim.Subieron = ult.subieron ?? new List<SubidaNivelHeroe>();
                sim.Botin = ult.botin ?? new List<PremioBotin>();
                sim.Recompensa = ult.recompensa ?? new RecompensaCombate { moneda = "ORO", cantidad = 0 };
                sim.TrofeosCambio = ult.trofeosCambio;
                sim.SaldoResultante = estado.Saldos.De("ORO");
                sim.Mensaje = $"Este combate ya se habÃƒÂ­a resuelto: mismo resultado, sin volver a premiar (op {idOperacion.Substring(0, Math.Min(8, idOperacion.Length))}...).";
                return sim;
            }

            return new ResultadoCombate
            {
                Exito = true,
                Repetida = true,
                SaldoResultante = estado.Saldos.De("ORO"),
                NivelCuenta = estado.Perfil.nivelCuenta,
                ExperienciaCuenta = estado.Perfil.experiencia,
                Mensaje = "Este combate ya se habÃƒÂ­a resuelto anteriormente: sin volver a premiar."
            };
        }

        [CloudCodeFunction("ResolverCombate")]
        public async Task<ResultadoCombate> ResolverCombate(
            IExecutionContext context,
            IGameApiClient gameApiClient,
            List<string> equipo,
            string idRival,
            string idOperacion = null)
        {
            var playerId = context.PlayerId;
            if (string.IsNullOrWhiteSpace(idOperacion))
                idOperacion = Guid.NewGuid().ToString();

            if (string.IsNullOrWhiteSpace(idRival))
                return Rechazo("Debes indicar un rival vÃƒÂ¡lido.");

            if (idRival == playerId)
                return Rechazo("No puedes pelear contra tu propia defensa.");

            if (equipo == null || equipo.Count == 0 || equipo.Count > MaxHeroesPorEquipo)
                return Rechazo($"El equipo lleva entre 1 y 3 hÃƒÂ©roes (mandaste {equipo?.Count ?? 0}).");

            var (tablaHeroes, rivales, cfgProg) = await LeerConfiguracionAsync(context, gameApiClient);
            var estado = await ModuloTienda.LeerEstadoCompletoAsync(context, gameApiClient, playerId);

            // Idempotencia: si idOperacion ya existe en operaciones, devolver mismo resultado sin escribir
            if (estado.Operaciones.ids.Contains(idOperacion))
                return RespuestaRepetida(estado, idOperacion);

            // Seleccionar instancias de hÃƒÂ©roes del inventario propio y aplicar bono por nivel
            var elegidos = SeleccionarHeroesPropio(equipo, estado.Inventario.heroes, out string faltante);
            if (elegidos == null)
                return Rechazo($"Ã‚Â«{faltante}Ã‚Â» no estÃƒÂ¡ en tu inventario.");

            var propio = new List<UnidadCombate>();
            foreach (var h in elegidos)
            {
                if (!tablaHeroes.TryGetValue(h.id, out var statsBase))
                    return Rechazo($"Ã‚Â«{h.id}Ã‚Â» no existe en la tabla de hÃƒÂ©roes.");

                propio.Add(new UnidadCombate
                {
                    Id = h.id,
                    Vida = ReglasProgresion.AplicarBonoNivel(cfgProg.Niveles, statsBase.Vida, h.nivel),
                    Ataque = ReglasProgresion.AplicarBonoNivel(cfgProg.Niveles, statsBase.Ataque, h.nivel),
                    Defensa = ReglasProgresion.AplicarBonoNivel(cfgProg.Niveles, statsBase.Defensa, h.nivel),
                    Velocidad = statsBase.Velocidad
                });
            }

            // Resolver equipo rival (NPC o PlayerId real)
            var npcConfig = rivales.Rivales.FirstOrDefault(r => r.Id == idRival && r.Id != "pvp");
            string nombreRival;
            string premioMoneda;
            int premioCantidad;
            int trofeosPremio;
            List<UnidadCombate> enemigo;

            if (npcConfig != null)
            {
                nombreRival = npcConfig.Nombre;
                premioMoneda = npcConfig.PremioMoneda;
                premioCantidad = npcConfig.PremioCantidad;
                trofeosPremio = npcConfig.Trofeos;
                enemigo = ArmarEquipoRival(npcConfig.Equipo, tablaHeroes);
                if (enemigo.Count == 0)
                    return Rechazo($"El equipo del rival Ã‚Â«{idRival}Ã‚Â» no tiene unidades vÃƒÂ¡lidas.");
            }
            else
            {
                var respDef = await gameApiClient.CloudSaveData.GetPublicItemsAsync(
                    context, context.ServiceToken, context.ProjectId, idRival, new List<string> { "defensa" }, null);

                var itemDef = respDef.Data?.Results?.FirstOrDefault(r => r.Key == "defensa");
                if (itemDef?.Value == null)
                    return Rechazo($"No hay ningÃƒÂºn rival Ã‚Â«{idRival}Ã‚Â».");

                var defensa = ModuloRivales.ParsearDefensaPublica(itemDef.Value.ToString());
                if (defensa == null)
                    return Rechazo($"No hay ningÃƒÂºn rival Ã‚Â«{idRival}Ã‚Â».");

                nombreRival = string.IsNullOrWhiteSpace(defensa.nombre) ? idRival : defensa.nombre;

                if (defensa.equipo == null || defensa.equipo.Count == 0)
                    return Rechazo($"Ã‚Â«{nombreRival}Ã‚Â» tiene la defensa vacÃƒÂ­a: no se puede atacar.");

                if (defensa.equipo.Count > MaxHeroesPorEquipo)
                    return Rechazo($"La defensa de Ã‚Â«{nombreRival}Ã‚Â» excede el lÃƒÂ­mite de {MaxHeroesPorEquipo} hÃƒÂ©roes.");

                var estadoRival = await ModuloTienda.LeerEstadoCompletoAsync(context, gameApiClient, idRival);
                var elegidosRival = SeleccionarHeroesPropio(defensa.equipo, estadoRival.Inventario.heroes, out string heroAjeno);
                if (elegidosRival == null)
                    return Rechazo($"La defensa de Ã‚Â«{nombreRival}Ã‚Â» declara Ã‚Â«{heroAjeno}Ã‚Â», que no estÃƒÂ¡ en su inventario.");

                enemigo = new List<UnidadCombate>();
                foreach (var hr in elegidosRival)
                {
                    if (!tablaHeroes.TryGetValue(hr.id, out var stRival))
                        return Rechazo($"Ã‚Â«{hr.id}Ã‚Â» no existe en la tabla de hÃƒÂ©roes.");

                    enemigo.Add(new UnidadCombate
                    {
                        Id = hr.id,
                        Vida = ReglasProgresion.AplicarBonoNivel(cfgProg.Niveles, stRival.Vida, hr.nivel),
                        Ataque = ReglasProgresion.AplicarBonoNivel(cfgProg.Niveles, stRival.Ataque, hr.nivel),
                        Defensa = ReglasProgresion.AplicarBonoNivel(cfgProg.Niveles, stRival.Defensa, hr.nivel),
                        Velocidad = stRival.Velocidad
                    });
                }

                var pvpConfig = rivales.Rivales.FirstOrDefault(r => r.Id == "pvp");
                premioMoneda = pvpConfig?.PremioMoneda ?? "ORO";
                premioCantidad = pvpConfig?.PremioCantidad ?? 200;
                trofeosPremio = pvpConfig?.Trofeos ?? 10;
            }

            var (campanaEvento, _) = await CampanasEvento.LeerAsync(gameApiClient, context, playerId, CampanasEvento.EsNuevo(estado.Perfil.nivelCuenta));
            foreach (var unidad in propio)
                unidad.Ataque = (int)Math.Round(unidad.Ataque * campanaEvento.FactorDano, MidpointRounding.AwayFromZero);

            int semilla = Random.Shared.Next(1, int.MaxValue);
            var resultado = Simular(propio, enemigo, semilla);
            resultado.Exito = true;
            resultado.Rival = nombreRival;

            // Calcular experiencia y aplicar subida de nivel en bucle
            int xpGanada = ReglasProgresion.ExperienciaDelCombate(cfgProg.Experiencia, resultado.Victoria, resultado.TurnosJugados);
            resultado.XpGanada = xpGanada;

            var subieron = new List<SubidaNivelHeroe>();
            for (int i = 0; i < elegidos.Count; i++)
            {
                if (!cfgProg.Experiencia.CaidosGanan && resultado.VidaFinalPropio[i] <= 0)
                    continue;

                int nivAntes = elegidos[i].nivel;
                var (nivNuevo, expNueva, subidos) = ReglasProgresion.Aplicar(
                    cfgProg.Niveles, elegidos[i].nivel, elegidos[i].experiencia, xpGanada);

                elegidos[i].nivel = nivNuevo;
                elegidos[i].experiencia = expNueva;

                if (subidos > 0)
                {
                    subieron.Add(new SubidaNivelHeroe
                    {
                        id = elegidos[i].id,
                        nivelAnterior = nivAntes,
                        nivelNuevo = nivNuevo,
                        experienciaActual = expNueva
                    });
                }
            }
            resultado.Subieron = subieron;

            // La cuenta siempre recibe XP
            var (nivCuentaNuevo, expCuentaNueva, _) = ReglasProgresion.Aplicar(
                cfgProg.Niveles, estado.Perfil.nivelCuenta, estado.Perfil.experiencia, xpGanada);
            estado.Perfil.nivelCuenta = nivCuentaNuevo;
            estado.Perfil.experiencia = expCuentaNueva;
            resultado.NivelCuenta = nivCuentaNuevo;
            resultado.ExperienciaCuenta = expCuentaNueva;

            // BotÃƒÂ­n sembrado solo al ganar
            var botinGanado = resultado.Victoria
                ? ReglasProgresion.TirarBotin(campanaEvento.BotinConEvento(cfgProg.Botin), semilla)
                : new List<PremioBotin>();
            resultado.Botin = botinGanado;

            foreach (var premio in botinGanado)
            {
                if (premio.Tipo == "objeto")
                {
                    for (int c = 0; c < premio.Cantidad; c++)
                        estado.Inventario.objetos.Add(premio.Id);
                }
                else if (premio.Tipo == "moneda")
                {
                    estado.Saldos.Poner(premio.Id, estado.Saldos.De(premio.Id) + premio.Cantidad);
                }
            }

            // Recompensa de monedas y trofeos
            int premioGanado = resultado.Victoria ? premioCantidad : 0;
            int cambioTrofeos = ReglasProgresion.CambioDeTrofeos(estado.Progreso.trofeos, resultado.Victoria, trofeosPremio);

            int saldoResultante = estado.Saldos.De(premioMoneda) + premioGanado;
            estado.Saldos.Poner(premioMoneda, saldoResultante);

            if (resultado.Victoria)
                estado.Progreso.combatesGanados++;
            else
                estado.Progreso.combatesPerdidos++;

            estado.Progreso.trofeos = Math.Max(0, estado.Progreso.trofeos + cambioTrofeos);

            resultado.Recompensa = new RecompensaCombate { moneda = premioMoneda, cantidad = premioGanado };
            resultado.TrofeosCambio = cambioTrofeos;
            resultado.SaldoResultante = estado.Saldos.De("ORO");
            resultado.Mensaje = resultado.Victoria
                ? $"Victoria contra Ã‚Â«{nombreRival}Ã‚Â» en {resultado.TurnosJugados} turnos: +{premioGanado} {premioMoneda} Ã‚Â· +{xpGanada} XP."
                : $"Derrota contra Ã‚Â«{nombreRival}Ã‚Â» en {resultado.TurnosJugados} turnos: +{xpGanada} XP.";

            estado.Operaciones.ids.Add(idOperacion);
            estado.UltimoCombate = new UltimoCombate
            {
                idOperacion = idOperacion,
                semilla = semilla,
                rival = nombreRival,
                victoria = resultado.Victoria,
                fechaUtc = DateTime.UtcNow.ToString("o"),
                equipoPropio = propio.Select(u => u.Clonar()).ToList(),
                equipoRival = enemigo.Select(u => u.Clonar()).ToList(),
                xpGanada = xpGanada,
                nivelCuenta = nivCuentaNuevo,
                experienciaCuenta = expCuentaNueva,
                subieron = subieron,
                botin = botinGanado,
                recompensa = resultado.Recompensa,
                trofeosCambio = cambioTrofeos,
                saldoResultante = resultado.SaldoResultante
            };

            try
            {
                await ModuloTienda.EscribirEstadoCompletoAsync(context, gameApiClient, playerId, estado);
                resultado.PuestoEnRanking = await PublicarTrofeosAsync(context, gameApiClient, playerId, estado.Progreso.trofeos);
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
            var estado = await ModuloTienda.LeerEstadoCompletoAsync(context, gameApiClient, context.PlayerId);
            var ultimo = estado.UltimoCombate;
            if (ultimo == null || ultimo.equipoPropio.Count == 0)
                return Rechazo("No tienes ningÃƒÂºn combate previo registrado para repetir.");

            var resultado = Simular(ultimo.equipoPropio, ultimo.equipoRival, ultimo.semilla);
            resultado.Exito = true;
            resultado.Repeticion = true;
            resultado.Rival = ultimo.rival;
            resultado.XpGanada = ultimo.xpGanada;
            resultado.Botin = ultimo.botin ?? new List<PremioBotin>();
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
                VidaFinalPropio = nuestros.Select(u => u.Vida).ToList(),
                BattleLog = log
            };
        }

        private List<HeroeInventario> SeleccionarHeroesPropio(
            List<string> idsSolicitados,
            List<HeroeInventario> inventarioHeroes,
            out string faltante)
        {
            faltante = null;
            var disponibles = new List<HeroeInventario>(inventarioHeroes);
            var elegidos = new List<HeroeInventario>();

            foreach (var id in idsSolicitados)
            {
                var encontrado = disponibles.FirstOrDefault(h => h.id == id);
                if (encontrado == null)
                {
                    faltante = id;
                    return null;
                }
                disponibles.Remove(encontrado);
                elegidos.Add(encontrado);
            }
            return elegidos;
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

        private async Task<(Dictionary<string, UnidadCombate> tabla, TablaRivales rivales, ConfigProgresion progresion)> LeerConfiguracionAsync(
            IExecutionContext context, IGameApiClient gameApiClient)
        {
            var response = await gameApiClient.RemoteConfigSettings.AssignSettingsGetAsync(
                context, context.AccessToken, context.ProjectId, context.EnvironmentId,
                "settings", new List<string> { "tabla_heroes", "rivales_npc", "progresion" }, null);

            var tabla = ObtenerTablaDefault();
            var rivales = ObtenerRivalesDefault();
            var progresion = ObtenerProgresionDefault();
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

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
                        _logger.LogWarning($"Usando rivales por defecto: {ex.Message}");
                    }
                }

                var tablaNode = settingsNode?["tabla_heroes"];
                if (tablaNode != null)
                {
                    try
                    {
                        string rawTabla = tablaNode is JsonValue ? tablaNode.ToString() : tablaNode.ToJsonString();
                        var parsed = JsonSerializer.Deserialize<Dictionary<string, UnidadCombate>>(rawTabla, opts);
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

                var progNode = settingsNode?["progresion"];
                if (progNode != null)
                {
                    try
                    {
                        string rawProg = progNode is JsonValue ? progNode.ToString() : progNode.ToJsonString();
                        var parsedProg = JsonSerializer.Deserialize<ConfigProgresion>(rawProg, opts);
                        if (parsedProg != null) progresion = parsedProg;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Usando progresion por defecto: {ex.Message}");
                    }
                }
            }

            return (tabla, rivales, progresion);
        }

        private Dictionary<string, UnidadCombate> ObtenerTablaDefault() => new Dictionary<string, UnidadCombate>
        {
            ["HEROE_GUERRERO"] = new UnidadCombate { Id = "HEROE_GUERRERO", Vida = 140, Ataque = 12, Defensa = 9, Velocidad = 8 },
            ["HEROE_ARQUERA"]  = new UnidadCombate { Id = "HEROE_ARQUERA",  Vida = 90,  Ataque = 17, Defensa = 4, Velocidad = 14 },
            ["HEROE_MAGO"]     = new UnidadCombate { Id = "HEROE_MAGO",     Vida = 80,  Ataque = 22, Defensa = 3, Velocidad = 11 }
        };

        private TablaRivales ObtenerRivalesDefault() => new TablaRivales
        {
            Rivales = new List<RivalNpc>
            {
                new RivalNpc { Id = "bandidos", Nombre = "Bandidos del camino", Equipo = new List<string> { "HEROE_ARQUERA" }, PremioMoneda = "ORO", PremioCantidad = 150, Trofeos = 5 },
                new RivalNpc { Id = "cazadores", Nombre = "Cazadores furtivos", Equipo = new List<string> { "HEROE_GUERRERO", "HEROE_ARQUERA" }, PremioMoneda = "ORO", PremioCantidad = 300, Trofeos = 8 },
                new RivalNpc { Id = "guardia_real", Nombre = "Guardia real", Equipo = new List<string> { "HEROE_GUERRERO", "HEROE_MAGO", "HEROE_ARQUERA" }, PremioMoneda = "ORO", PremioCantidad = 600, Trofeos = 10 },
                new RivalNpc { Id = "pvp", Nombre = "Duelo PvP AsÃƒÂ­ncrono", Equipo = new List<string>(), PremioMoneda = "ORO", PremioCantidad = 200, Trofeos = 10 }
            }
        };

        private ConfigProgresion ObtenerProgresionDefault() => new ConfigProgresion
        {
            Experiencia = new ConfigExperiencia { Victoria = 50, Derrota = 15, PorTurno = 1, TopeTurnos = 20, CaidosGanan = false },
            Niveles = new ConfigNiveles { Maximo = 10, CostoBase = 100, BonoPorNivel = 0.05 },
            Botin = new ConfigBotin
            {
                Tiradas = 1,
                Premios = new List<PremioBotin>
                {
                    new PremioBotin { Id = "NADA", Tipo = "nada", Cantidad = 0, Peso = 40 },
                    new PremioBotin { Id = "CAJA_BASICA", Tipo = "objeto", Cantidad = 1, Peso = 40 },
                    new PremioBotin { Id = "GEMAS", Tipo = "moneda", Cantidad = 20, Peso = 20 }
                }
            },
            Nodos = new List<NodoProgresion>
            {
                new NodoProgresion { Id = "TALENTO_ATAQUE_1", Nombre = "Ataque I", NivelCuentaMinimo = 1, CuestaMoneda = "ORO", CuestaCantidad = 100, Requiere = new List<string>() },
                new NodoProgresion { Id = "TALENTO_ATAQUE_2", Nombre = "Ataque II", NivelCuentaMinimo = 2, CuestaMoneda = "ORO", CuestaCantidad = 300, Requiere = new List<string> { "TALENTO_ATAQUE_1" } },
                new NodoProgresion { Id = "TALENTO_DEFENSA_1", Nombre = "Defensa I", NivelCuentaMinimo = 1, CuestaMoneda = "ORO", CuestaCantidad = 100, Requiere = new List<string>() },
                new NodoProgresion { Id = "TALENTO_MAESTRIA", Nombre = "MaestrÃƒÂ­a", NivelCuentaMinimo = 3, CuestaMoneda = "ORO", CuestaCantidad = 500, Requiere = new List<string> { "TALENTO_ATAQUE_2", "TALENTO_DEFENSA_1" } }
            }
        };
    }
}