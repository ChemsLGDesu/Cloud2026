using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.Leaderboards.Model;

namespace Tienda
{
    public class ConfigTemporada
    {
        public string Nombre { get; set; } = "Temporada 1";
        public string Tabla { get; set; } = "trofeos_dvsc";
        public Dictionary<string, string> Premios { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Oro"] = "Corona de oro (+20 GEMAS)",
            ["Plata"] = "Medalla de plata (+10 GEMAS)",
            ["Bronce"] = "Insignia de bronce (+5 GEMAS)"
        };
    }

    public class ResultadoTemporada
    {
        public bool Exito { get; set; }
        public string NombreTemporada { get; set; } = "";
        public int Puesto { get; set; }
        public double Trofeos { get; set; }
        public string Tier { get; set; } = "";
        public string Premio { get; set; } = "";
        public string CierreUtc { get; set; } = "";
        public string HoraServidorUtc { get; set; } = "";
        public long SegundosRestantes { get; set; }
        public string TiempoRestanteTexto { get; set; } = "";
    }

    public partial class ModuloCombate
    {
        public const string TablaTrofeos = "trofeos_dvsc";
        private static readonly HttpClient _httpRanking = new HttpClient();

        public async Task<int> PublicarTrofeosAsync(
            IExecutionContext context,
            IGameApiClient gameApiClient,
            string playerId,
            int trofeos)
        {
            try
            {
                var r = await gameApiClient.Leaderboards.AddLeaderboardPlayerScoreAsync(
                    context,
                    context.ServiceToken,
                    Guid.Parse(context.ProjectId),
                    TablaTrofeos,
                    playerId,
                    new AddLeaderboardScore(trofeos, null, null));

                return (r?.Data?.Rank ?? -1) + 1;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"No se pudo publicar en leaderboard {TablaTrofeos}: {ex.Message}");
                return 0;
            }
        }

        [CloudCodeFunction("VistaTemporada")]
        public async Task<ResultadoTemporada> VistaTemporada(
            IExecutionContext context,
            IGameApiClient gameApiClient)
        {
            var cfg = await LeerConfigTemporadaAsync(context, gameApiClient);
            var proyecto = Guid.Parse(context.ProjectId);

            DateTime cierre = await LeerCierreAsync(context, context.ProjectId, cfg.Tabla);
            DateTime ahora = DateTime.UtcNow;
            long segundosRestantes = Math.Max(0, (long)(cierre - ahora).TotalSeconds);

            int puesto = 0;
            double trofeos = 0;
            string tier = "Bronce";

            try
            {
                var entrada = (await gameApiClient.Leaderboards.GetLeaderboardPlayerScoreAsync(
                    context, context.AccessToken, proyecto, cfg.Tabla, context.PlayerId, null))?.Data;

                if (entrada != null)
                {
                    puesto = entrada.Rank + 1;
                    trofeos = entrada.Score;
                    if (!string.IsNullOrWhiteSpace(entrada.Tier))
                        tier = entrada.Tier;
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"Jugador aún sin puntaje en {cfg.Tabla}: {ex.Message}");
            }

            cfg.Premios.TryGetValue(tier, out string premio);
            var ts = TimeSpan.FromSeconds(segundosRestantes);

            return new ResultadoTemporada
            {
                Exito = true,
                NombreTemporada = cfg.Nombre,
                Puesto = puesto,
                Trofeos = trofeos,
                Tier = tier,
                Premio = premio ?? "Sin premio asignado",
                CierreUtc = cierre.ToString("yyyy-MM-dd HH:mm:ss'Z'"),
                HoraServidorUtc = ahora.ToString("yyyy-MM-dd HH:mm:ss'Z'"),
                SegundosRestantes = segundosRestantes,
                TiempoRestanteTexto = $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m"
            };
        }

        private async Task<DateTime> LeerCierreAsync(IExecutionContext context, string projectId, string leaderboardId)
        {
            try
            {
                string url = $"https://leaderboards.services.api.unity.com/v1/projects/{projectId}/leaderboards/{leaderboardId}/versions";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);

                using var resp = await _httpRanking.SendAsync(req);
                string json = await resp.Content.ReadAsStringAsync();
                if (resp.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(json))
                {
                    var node = JsonNode.Parse(json);
                    string nextResetStr = node?["nextReset"]?.ToString();
                    if (DateTime.TryParse(nextResetStr, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out DateTime parsed))
                        return parsed;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"No se pudo leer nextReset por REST: {ex.Message}");
            }

            return new DateTime(2026, 12, 1, 5, 0, 0, DateTimeKind.Utc);
        }

        private async Task<ConfigTemporada> LeerConfigTemporadaAsync(IExecutionContext context, IGameApiClient gameApiClient)
        {
            var cfg = new ConfigTemporada();
            try
            {
                var response = await gameApiClient.RemoteConfigSettings.AssignSettingsGetAsync(
                    context, context.AccessToken, context.ProjectId, context.EnvironmentId,
                    "settings", new List<string> { "temporada" }, null);

                if (response.Data?.Configs != null)
                {
                    JsonNode configsNode = JsonNode.Parse(JsonSerializer.Serialize(response.Data.Configs));
                    var settingsNode = configsNode?["settings"] ?? configsNode?["Settings"];
                    var tempNode = settingsNode?["temporada"];
                    if (tempNode != null)
                    {
                        string raw = tempNode is JsonValue ? tempNode.ToString() : tempNode.ToJsonString();
                        var parsed = JsonSerializer.Deserialize<ConfigTemporada>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (parsed != null)
                        {
                            if (!string.IsNullOrWhiteSpace(parsed.Nombre)) cfg.Nombre = parsed.Nombre;
                            if (!string.IsNullOrWhiteSpace(parsed.Tabla)) cfg.Tabla = parsed.Tabla;
                            if (parsed.Premios != null && parsed.Premios.Count > 0)
                                cfg.Premios = new Dictionary<string, string>(parsed.Premios, StringComparer.OrdinalIgnoreCase);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Usando config temporada por defecto: {ex.Message}");
            }
            return cfg;
        }
    }
}