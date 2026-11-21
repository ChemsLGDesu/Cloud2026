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
    public class DefensaPublica
    {
        public string nombre { get; set; } = "";
        public int nivelCuenta { get; set; }
        public int trofeos { get; set; }
        public List<string> equipo { get; set; } = new List<string>();
        public string actualizadaUtc { get; set; } = "";
    }

    public class RivalEncontrado
    {
        public string PlayerId { get; set; } = "";
        public string Nombre { get; set; } = "";
        public int Nivel { get; set; }
        public int Trofeos { get; set; }
        public List<string> Equipo { get; set; } = new List<string>();
    }

    public class ResultadoBusquedaRival
    {
        public bool Hubo { get; set; }
        public string Mensaje { get; set; } = "";
        public int NivelSolicitante { get; set; }
        public int Franja { get; set; }
        public int Candidatos { get; set; }
        public int Consultas { get; set; }
        public int LecturasConsumidas { get; set; }
        public RivalEncontrado Rival { get; set; }
    }

    public class ModuloRivales
    {
        private const int FranjaInicial = 2;
        private const int PasoFranja = 2;
        private const int FranjaMaxima = 10;
        private const int CandidatosSuficientes = 2;
        private readonly ILogger<ModuloRivales> _logger;

        public ModuloRivales(ILogger<ModuloRivales> logger)
        {
            _logger = logger;
        }

        [CloudCodeFunction("BuscarRival")]
        public async Task<ResultadoBusquedaRival> BuscarRival(IExecutionContext context, IGameApiClient gameApiClient)
        {
            var playerId = context.PlayerId;
            var resultado = new ResultadoBusquedaRival();

            // Leer nivel pÃºblico del propio solicitante
            var respPropio = await gameApiClient.CloudSaveData.GetPublicItemsAsync(
                context, context.ServiceToken, context.ProjectId, playerId, new List<string> { "nivel" }, null);

            var itemNivel = respPropio.Data?.Results?.FirstOrDefault(r => r.Key == "nivel");
            if (itemNivel?.Value == null || !int.TryParse(itemNivel.Value.ToString(), out int nivel))
            {
                resultado.Hubo = false;
                resultado.Mensaje = "AÃºn no has publicado tu defensa pÃºblica (falta la llave 'nivel').";
                return resultado;
            }

            resultado.NivelSolicitante = nivel;
            var candidatos = new List<RivalEncontrado>();

            for (int franja = FranjaInicial; franja <= FranjaMaxima; franja += PasoFranja)
            {
                candidatos = await ConsultarFranjaAsync(context, gameApiClient, nivel - franja, nivel + franja, resultado);
                candidatos.RemoveAll(c => c.PlayerId == playerId); // Excluir al propio jugador
                resultado.Franja = franja;
                resultado.Candidatos = candidatos.Count;

                if (candidatos.Count >= CandidatosSuficientes)
                    break;
            }

            if (candidatos.Count == 0)
            {
                int min = Math.Max(1, nivel - FranjaMaxima);
                int max = nivel + FranjaMaxima;
                resultado.Hubo = false;
                resultado.Mensaje = $"No hay rivales entre los niveles {min} y {max}.";
                return resultado;
            }

            var elegido = candidatos[Random.Shared.Next(candidatos.Count)];
            resultado.Hubo = true;
            resultado.Rival = elegido;
            resultado.Mensaje = $"Rival encontrado: Â«{elegido.Nombre}Â» (nivel {elegido.Nivel}) en franja Â±{resultado.Franja}.";
            return resultado;
        }

        private async Task<List<RivalEncontrado>> ConsultarFranjaAsync(
            IExecutionContext context,
            IGameApiClient gameApiClient,
            int nivelMinimo,
            int nivelMaximo,
            ResultadoBusquedaRival metricas)
        {
            var consulta = new QueryIndexBody(
                fields: new List<FieldFilter>
                {
                    new FieldFilter("nivel", nivelMinimo, FieldFilter.OpEnum.GE, true),
                    new FieldFilter("nivel", nivelMaximo, FieldFilter.OpEnum.LE, true)
                },
                returnKeys: new List<string> { "defensa", "nivel" },
                offset: 0,
                limit: 20
            );

            var resp = await gameApiClient.CloudSaveData.QueryPublicPlayerDataAsync(
                context, context.ServiceToken, context.ProjectId, consulta);

            metricas.Consultas++;
            var lista = new List<RivalEncontrado>();

            if (resp.Data?.Results == null) return lista;

            metricas.LecturasConsumidas += resp.Data.Results.Count;

            foreach (var entry in resp.Data.Results)
            {
                string idJugador = entry.Id?.ActualInstance?.ToString() ?? entry.Id?.ToString()?.Trim('"');
                if (string.IsNullOrEmpty(idJugador) || entry.Data == null) continue;

                var itemDef = entry.Data.FirstOrDefault(d => d.Key == "defensa");
                var itemNiv = entry.Data.FirstOrDefault(d => d.Key == "nivel");
                if (itemDef?.Value == null) continue;

                var def = ParsearDefensaPublica(itemDef.Value.ToString());
                if (def == null) continue;

                int niv = def.nivelCuenta;
                if (itemNiv?.Value != null && int.TryParse(itemNiv.Value.ToString(), out int parsedNiv))
                    niv = parsedNiv;

                lista.Add(new RivalEncontrado
                {
                    PlayerId = idJugador,
                    Nombre = string.IsNullOrWhiteSpace(def.nombre) ? idJugador : def.nombre,
                    Nivel = niv,
                    Trofeos = def.trofeos,
                    Equipo = def.equipo
                });
            }

            return lista;
        }

        public static DefensaPublica ParsearDefensaPublica(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson)) return null;
            try
            {
                var node = JsonNode.Parse(rawJson);
                if (node is not JsonObject obj) return null;

                var eq = new List<string>();
                var eqNode = obj["equipo"] ?? obj["Equipo"];
                if (eqNode is JsonArray arr)
                {
                    foreach (var item in arr)
                    {
                        if (item is JsonValue v) eq.Add(v.ToString());
                        else if (item is JsonObject hObj)
                        {
                            var idNode = hObj["id"] ?? hObj["Id"];
                            if (idNode != null) eq.Add(idNode.ToString());
                        }
                    }
                }

                return new DefensaPublica
                {
                    nombre = (obj["nombre"] ?? obj["Nombre"])?.ToString() ?? "",
                    nivelCuenta = (obj["nivelCuenta"] ?? obj["NivelCuenta"])?.GetValue<int>() ?? 0,
                    trofeos = (obj["trofeos"] ?? obj["Trofeos"])?.GetValue<int>() ?? 0,
                    equipo = eq,
                    actualizadaUtc = (obj["actualizadaUtc"] ?? obj["ActualizadaUtc"])?.ToString() ?? ""
                };
            }
            catch
            {
                return null;
            }
        }
    }
}