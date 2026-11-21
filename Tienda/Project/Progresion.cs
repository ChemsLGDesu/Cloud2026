using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace Tienda
{
    public class ConfigExperiencia
    {
        public int Victoria { get; set; } = 50;
        public int Derrota { get; set; } = 15;
        public int PorTurno { get; set; } = 1;
        public int TopeTurnos { get; set; } = 20;
        public bool CaidosGanan { get; set; } = false;
    }

    public class ConfigNiveles
    {
        public int Maximo { get; set; } = 10;
        public int CostoBase { get; set; } = 100;
        public double BonoPorNivel { get; set; } = 0.05;
    }

    public class PremioBotin
    {
        public string Id { get; set; } = "";
        public string Tipo { get; set; } = "nada";
        public int Cantidad { get; set; }
        public int Peso { get; set; }
    }

    public class ConfigBotin
    {
        public int Tiradas { get; set; } = 1;
        public List<PremioBotin> Premios { get; set; } = new List<PremioBotin>();
    }

    public class NodoProgresion
    {
        public string Id { get; set; } = "";
        public string Nombre { get; set; } = "";
        public int NivelCuentaMinimo { get; set; } = 1;
        public string CuestaMoneda { get; set; } = "ORO";
        public int CuestaCantidad { get; set; }
        public List<string> Requiere { get; set; } = new List<string>();
    }

    public class ConfigProgresion
    {
        public ConfigExperiencia Experiencia { get; set; } = new ConfigExperiencia();
        public ConfigNiveles Niveles { get; set; } = new ConfigNiveles();
        public ConfigBotin Botin { get; set; } = new ConfigBotin();
        public List<NodoProgresion> Nodos { get; set; } = new List<NodoProgresion>();
    }

    public class SubidaNivelHeroe
    {
        public string id { get; set; } = "";
        public int nivelAnterior { get; set; }
        public int nivelNuevo { get; set; }
        public int experienciaActual { get; set; }
    }

    public class ResultadoNodo
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; } = "";
        public string NodoId { get; set; } = "";
        public int SaldoResultante { get; set; }
        public List<string> Desbloqueos { get; set; } = new List<string>();
    }

    public static class ReglasProgresion
    {
        public static int ExperienciaDelCombate(ConfigExperiencia cfg, bool victoria, int turnos)
        {
            int baseXp = victoria ? cfg.Victoria : cfg.Derrota;
            int turnosValidos = Math.Max(0, Math.Min(turnos, cfg.TopeTurnos));
            return baseXp + (cfg.PorTurno * turnosValidos);
        }

        public static int CostoParaSubir(ConfigNiveles niveles, int nivelActual)
        {
            int n = Math.Max(1, nivelActual);
            return niveles.CostoBase * n * n;
        }

        public static (int nivel, int experiencia, int subidos) Aplicar(
            ConfigNiveles niveles, int nivel, int experiencia, int ganada)
        {
            int subidos = 0;
            if (nivel >= niveles.Maximo)
                return (niveles.Maximo, 0, 0);

            int total = experiencia + Math.Max(0, ganada);
            while (nivel < niveles.Maximo && total >= CostoParaSubir(niveles, nivel))
            {
                total -= CostoParaSubir(niveles, nivel);
                nivel++;
                subidos++;
            }

            return nivel >= niveles.Maximo
                ? (niveles.Maximo, 0, subidos)
                : (nivel, total, subidos);
        }

        public static int AplicarBonoNivel(ConfigNiveles niveles, int statBase, int nivel)
        {
            double mult = 1.0 + (niveles.BonoPorNivel * Math.Max(0, nivel - 1));
            return (int)Math.Round(statBase * mult, MidpointRounding.AwayFromZero);
        }

        public static List<PremioBotin> TirarBotin(ConfigBotin botin, int semilla)
        {
            var obtenidos = new List<PremioBotin>();
            if (botin == null || botin.Premios == null || botin.Premios.Count == 0 || botin.Tiradas <= 0)
                return obtenidos;

            int pesoTotal = botin.Premios.Sum(p => Math.Max(0, p.Peso));
            if (pesoTotal <= 0) return obtenidos;

            var azar = new Random(semilla ^ 0x5EED);
            for (int tirada = 0; tirada < botin.Tiradas; tirada++)
            {
                int punto = azar.Next(pesoTotal);
                foreach (var premio in botin.Premios)
                {
                    int peso = Math.Max(0, premio.Peso);
                    if (peso == 0) continue;
                    punto -= peso;
                    if (punto < 0)
                    {
                        if (premio.Cantidad > 0 && premio.Tipo != "nada")
                            obtenidos.Add(premio);
                        break;
                    }
                }
            }
            return obtenidos;
        }

        public static string PorQueNoSePuede(
            NodoProgresion nodo,
            IReadOnlyCollection<string> desbloqueados,
            int nivelCuenta,
            int saldoDisponible)
        {
            if (nodo == null)
                return "El nodo solicitado no existe.";

            if (desbloqueados.Contains(nodo.Id))
                return $"«{nodo.Nombre}» ya está desbloqueado.";

            var faltantes = nodo.Requiere.Where(r => !desbloqueados.Contains(r)).ToList();
            if (faltantes.Count > 0)
                return $"Falta desbloquear antes: {string.Join(", ", faltantes)}.";

            if (nivelCuenta < nodo.NivelCuentaMinimo)
                return $"Requiere nivel de cuenta {nodo.NivelCuentaMinimo} (tienes {nivelCuenta}).";

            if (saldoDisponible < nodo.CuestaCantidad)
                return $"Te faltan {nodo.CuestaCantidad} {nodo.CuestaMoneda} (tienes {saldoDisponible}).";

            return null;
        }
    }

    public partial class ModuloCombate
    {
        [CloudCodeFunction("DesbloquearNodo")]
        public async Task<ResultadoNodo> DesbloquearNodo(
            IExecutionContext context,
            IGameApiClient gameApiClient,
            string idNodo)
        {
            if (string.IsNullOrWhiteSpace(idNodo))
                return new ResultadoNodo { Exito = false, Mensaje = "Debes indicar el id del nodo." };

            var (_, _, cfgProg) = await LeerConfiguracionAsync(context, gameApiClient);
            var nodo = cfgProg.Nodos.FirstOrDefault(n => n.Id == idNodo);
            if (nodo == null)
                return new ResultadoNodo { Exito = false, NodoId = idNodo, Mensaje = $"«{idNodo}» no es un nodo de progresión." };

            var estado = await ModuloTienda.LeerEstadoCompletoAsync(context, gameApiClient, context.PlayerId);
            int saldoMoneda = estado.Saldos.De(nodo.CuestaMoneda);

            string motivo = ReglasProgresion.PorQueNoSePuede(
                nodo, estado.Progreso.desbloqueos, estado.Perfil.nivelCuenta, saldoMoneda);

            if (motivo != null)
            {
                return new ResultadoNodo
                {
                    Exito = false,
                    NodoId = nodo.Id,
                    Mensaje = motivo,
                    SaldoResultante = saldoMoneda,
                    Desbloqueos = estado.Progreso.desbloqueos
                };
            }

            int nuevoSaldo = saldoMoneda - nodo.CuestaCantidad;
            estado.Saldos.Poner(nodo.CuestaMoneda, nuevoSaldo);
            estado.Progreso.desbloqueos.Add(nodo.Id);

            try
            {
                await ModuloTienda.EscribirEstadoCompletoAsync(context, gameApiClient, context.PlayerId, estado);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("409") || ex.Message.Contains("Conflict"))
                    return new ResultadoNodo { Exito = false, NodoId = nodo.Id, Mensaje = "Conflicto de concurrencia, vuelve a intentar." };
                throw;
            }

            return new ResultadoNodo
            {
                Exito = true,
                NodoId = nodo.Id,
                Mensaje = $"HECHO: Desbloqueaste «{nodo.Nombre}» por {nodo.CuestaCantidad} {nodo.CuestaMoneda}.",
                SaldoResultante = nuevoSaldo,
                Desbloqueos = estado.Progreso.desbloqueos
            };
        }
    }
}