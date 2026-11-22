using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace Tienda
{
    public class ConfigCampanaEvento
    {
        public bool Activo { get; set; }
        public string Id { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string InicioUtc { get; set; } = "";
        public string FinUtc { get; set; } = "";
        public double MultiplicadorDano { get; set; } = 1.0;
        public double DescuentoTienda { get; set; } = 0.0;
        public List<string> HeroesEspeciales { get; set; } = new List<string>();
        public List<PremioBotin> BotinExclusivo { get; set; } = new List<PremioBotin>();

        public double FactorDano =>
            Activo && MultiplicadorDano > 0 ? MultiplicadorDano : 1.0;

        public long PrecioConDescuento(string item, long precioBase)
        {
            if (!Activo || DescuentoTienda <= 0 || HeroesEspeciales == null || !HeroesEspeciales.Contains(item))
                return precioBase;

            return (long)Math.Round(
                precioBase * (1.0 - Math.Min(1.0, DescuentoTienda)),
                MidpointRounding.AwayFromZero);
        }

        public ConfigBotin BotinConEvento(ConfigBotin baseBotin)
        {
            var combinada = new ConfigBotin
            {
                Tiradas = baseBotin?.Tiradas ?? 1,
                Premios = new List<PremioBotin>(baseBotin?.Premios ?? new List<PremioBotin>())
            };

            if (Activo && BotinExclusivo != null && BotinExclusivo.Count > 0)
                combinada.Premios.AddRange(BotinExclusivo);

            return combinada;
        }
    }

    public class ConfigBonusBienvenida
    {
        public string Moneda { get; set; } = "ORO";
        public int Cantidad { get; set; } = 0;
    }

    public class ResultadoVistaEvento
    {
        public bool Activo { get; set; }
        public string Id { get; set; } = "";
        public string Nombre { get; set; } = "";
        public double MultiplicadorDano { get; set; } = 1.0;
        public double DescuentoTienda { get; set; } = 0.0;
        public List<string> HeroesEspeciales { get; set; } = new List<string>();
        public long SegundosRestantes { get; set; }
        public string TiempoRestanteTexto { get; set; } = "00:00:00";
        public string HoraServidorUtc { get; set; } = "";
        public bool EsNuevo { get; set; }
        public string BonusMoneda { get; set; } = "ORO";
        public int BonusCantidad { get; set; }
    }

    public class ResultadoBonusBienvenida
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; } = "";
        public int CantidadEntregada { get; set; }
        public int NuevoSaldo { get; set; }
    }

    public static class CampanasEvento
    {
        private static readonly HttpClient _http = new HttpClient();

        public static bool EsNuevo(int nivelCuenta) => nivelCuenta <= 1;

        private static JObject DesenvolverObjeto(JToken token)
        {
            for (int i = 0; i < 4 && token != null; i++)
            {
                if (token is JObject jo) return jo;
                if (token.Type == JTokenType.String)
                {
                    string s = token.Value<string>();
                    if (string.IsNullOrWhiteSpace(s)) return null;
                    try { token = JToken.Parse(s); continue; }
                    catch { return null; }
                }
                break;
            }
            return token as JObject;
        }

        private static JToken Prop(JObject obj, string name) =>
            obj?.GetValue(name, StringComparison.OrdinalIgnoreCase);

        public static async Task<(ConfigCampanaEvento campana, ConfigBonusBienvenida bonus)> LeerAsync(
            IGameApiClient api,
            IExecutionContext context,
            string playerId,
            bool esNuevo = false)
        {
            var campana = new ConfigCampanaEvento();
            var bonus = new ConfigBonusBienvenida();
            var keys = new List<string> { "campana_evento", "bonus_bienvenida" };
            JObject settingsObj = null;

            try
            {
                string url = $"https://config.unity3d.com/api/v1/projects/{context.ProjectId}/environments/{context.EnvironmentId}/settings";
                var payload = new
                {
                    userId = playerId,
                    attributes = new
                    {
                        unity = new { },
                        app = new { },
                        user = new Dictionary<string, string>
                        {
                            ["nuevo"] = esNuevo ? "true" : "false"
                        }
                    },
                    key = keys
                };

                using var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
                req.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                using var resp = await _http.SendAsync(req);
                string body = await resp.Content.ReadAsStringAsync();
                if (resp.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(body))
                {
                    var root = DesenvolverObjeto(JToken.Parse(body));
                    var cfgs = DesenvolverObjeto(Prop(root, "configs"));
                    settingsObj = DesenvolverObjeto(Prop(cfgs, "settings"));
                }
            }
            catch
            {
            }

            if (settingsObj == null)
            {
                try
                {
                    var getResp = await api.RemoteConfigSettings.AssignSettingsGetAsync(
                        context, context.AccessToken, context.ProjectId, context.EnvironmentId,
                        "settings", keys, null);

                    if (getResp.Data?.Configs != null)
                    {
                        string rawCfg = JsonConvert.SerializeObject(getResp.Data.Configs);
                        var cfgs = DesenvolverObjeto(JToken.Parse(rawCfg));
                        settingsObj = DesenvolverObjeto(Prop(cfgs, "settings"));
                    }
                }
                catch
                {
                }
            }

            if (settingsObj != null)
            {
                var evObj = DesenvolverObjeto(Prop(settingsObj, "campana_evento"));
                if (evObj != null)
                {
                    campana = evObj.ToObject<ConfigCampanaEvento>() ?? new ConfigCampanaEvento();
                    if (campana.Activo && DateTime.TryParse(campana.FinUtc, null,
                        DateTimeStyles.AdjustToUniversal, out DateTime fin) && DateTime.UtcNow >= fin)
                    {
                        campana.Activo = false;
                    }
                }

                var bonusObj = DesenvolverObjeto(Prop(settingsObj, "bonus_bienvenida"));
                if (bonusObj != null)
                {
                    bonus = bonusObj.ToObject<ConfigBonusBienvenida>() ?? new ConfigBonusBienvenida();
                }
            }

            return (campana, bonus);
        }
    }

    public partial class ModuloCombate
    {
        [CloudCodeFunction("VistaEvento")]
        public async Task<ResultadoVistaEvento> VistaEvento(
            IExecutionContext context,
            IGameApiClient gameApiClient)
        {
            var estado = await ModuloTienda.LeerEstadoCompletoAsync(context, gameApiClient, context.PlayerId);
            bool esNuevo = CampanasEvento.EsNuevo(estado.Perfil.nivelCuenta);
            var (campana, bonus) = await CampanasEvento.LeerAsync(gameApiClient, context, context.PlayerId, esNuevo);

            DateTime ahora = DateTime.UtcNow;
            long segundos = 0;
            if (campana.Activo && DateTime.TryParse(campana.FinUtc, null,
                DateTimeStyles.AdjustToUniversal, out DateTime fin))
            {
                segundos = Math.Max(0, (long)(fin - ahora).TotalSeconds);
            }

            var ts = TimeSpan.FromSeconds(segundos);
            return new ResultadoVistaEvento
            {
                Activo = campana.Activo,
                Id = campana.Id ?? "",
                Nombre = campana.Nombre ?? "",
                MultiplicadorDano = campana.FactorDano,
                DescuentoTienda = campana.Activo ? campana.DescuentoTienda : 0.0,
                HeroesEspeciales = campana.Activo ? (campana.HeroesEspeciales ?? new List<string>()) : new List<string>(),
                SegundosRestantes = segundos,
                TiempoRestanteTexto = $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}",
                HoraServidorUtc = ahora.ToString("HH:mm:ss'Z'"),
                EsNuevo = esNuevo,
                BonusMoneda = bonus.Moneda,
                BonusCantidad = bonus.Cantidad
            };
        }

        [CloudCodeFunction("ReclamarBonusBienvenida")]
        public async Task<ResultadoBonusBienvenida> ReclamarBonusBienvenida(
            IExecutionContext context,
            IGameApiClient gameApiClient)
        {
            string playerId = context.PlayerId;
            var estado = await ModuloTienda.LeerEstadoCompletoAsync(context, gameApiClient, playerId);
            int saldoActual = estado.Saldos.De("ORO");

            if (estado.Perfil.bonoBienvenida)
            {
                return new ResultadoBonusBienvenida
                {
                    Exito = false,
                    Mensaje = "El bonus de bienvenida ya se reclamó.",
                    NuevoSaldo = saldoActual
                };
            }

            bool esNuevo = CampanasEvento.EsNuevo(estado.Perfil.nivelCuenta);
            if (!esNuevo)
            {
                return new ResultadoBonusBienvenida
                {
                    Exito = false,
                    Mensaje = $"El bonus es para jugadores nuevos (tu cuenta es nivel {estado.Perfil.nivelCuenta}).",
                    NuevoSaldo = saldoActual
                };
            }

            var (_, bonus) = await CampanasEvento.LeerAsync(gameApiClient, context, playerId, true);
            if (bonus.Cantidad <= 0)
            {
                return new ResultadoBonusBienvenida
                {
                    Exito = false,
                    Mensaje = "No hay un bonus de bienvenida activo en este momento.",
                    NuevoSaldo = saldoActual
                };
            }

            string moneda = string.IsNullOrWhiteSpace(bonus.Moneda) ? "ORO" : bonus.Moneda;
            int nuevoSaldo = estado.Saldos.De(moneda) + bonus.Cantidad;
            estado.Saldos.Poner(moneda, nuevoSaldo);
            estado.Perfil.bonoBienvenida = true;

            await ModuloTienda.EscribirEstadoCompletoAsync(context, gameApiClient, playerId, estado);

            return new ResultadoBonusBienvenida
            {
                Exito = true,
                Mensaje = $"HECHO: Bonus de bienvenida: +{bonus.Cantidad} {moneda}.",
                CantidadEntregada = bonus.Cantidad,
                NuevoSaldo = nuevoSaldo
            };
        }
    }
}
