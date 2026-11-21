using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using UnityEngine;

[Serializable]
public class UnidadCombateDto
{
    public string id;
    public int vida;
    public int ataque;
    public int defensa;
    public int velocidad;
    public bool viva;
}

[Serializable]
public class EntradaBattleLogDto
{
    public int turno;
    public string atacante;
    public string objetivo;
    public int dano;
    public int vidaRestante;
}

[Serializable]
public class RecompensaCombateDto
{
    public string moneda;
    public int cantidad;
}

[Serializable]
public class SubidaNivelHeroeDto
{
    public string id;
    public int nivelAnterior;
    public int nivelNuevo;
    public int experienciaActual;
}

[Serializable]
public class PremioBotinDto
{
    public string id;
    public string tipo;
    public int cantidad;
    public int peso;
}

[Serializable]
public class ResultadoCombateDto
{
    public bool Exito;
    public string Mensaje;
    public bool Repeticion;
    public bool Repetida;
    public bool Victoria;
    public int TurnosJugados;
    public int Semilla;
    public string Rival;
    public List<UnidadCombateDto> EquipoPropio;
    public List<UnidadCombateDto> EquipoRival;
    public List<int> VidaFinalPropio;
    public List<EntradaBattleLogDto> BattleLog;
    public RecompensaCombateDto Recompensa;
    public int TrofeosCambio;
    public int SaldoResultante;
    public int XpGanada;
    public int NivelCuenta;
    public int ExperienciaCuenta;
    public List<SubidaNivelHeroeDto> Subieron;
    public List<PremioBotinDto> Botin;
}

public class ReproductorCombate : MonoBehaviour
{
    public event Action<EntradaBattleLogDto> AccionReproducida;
    public event Action<ResultadoCombateDto> CombateTerminado;

    public ResultadoCombateDto UltimoResultado { get; private set; }

    public static string NuevaOperacion() => Guid.NewGuid().ToString();

    public async Task<ResultadoCombateDto> PelearAsync(IList<string> equipo, string idRival, string idOperacion = null)
    {
        if (string.IsNullOrWhiteSpace(idOperacion))
            idOperacion = NuevaOperacion();

        var args = new Dictionary<string, object>
        {
            { "equipo", equipo },
            { "idRival", idRival },
            { "idOperacion", idOperacion }
        };

        var res = await CloudCodeService.Instance.CallModuleEndpointAsync<ResultadoCombateDto>(
            "Tienda", "ResolverCombate", args);

        if (res != null && res.Exito && !res.Repetida)
            UltimoResultado = res;

        return res;
    }

    public async Task<ResultadoCombateDto> RepetirAsync()
    {
        var res = await CloudCodeService.Instance.CallModuleEndpointAsync<ResultadoCombateDto>(
            "Tienda", "RepetirCombate", new Dictionary<string, object>());
        return res;
    }

    public IEnumerator Reproducir(ResultadoCombateDto resultado, float intervaloSegundos = 0.5f)
    {
        if (resultado == null || resultado.BattleLog == null)
            yield break;

        foreach (var entrada in resultado.BattleLog)
        {
            AccionReproducida?.Invoke(entrada);
            yield return new WaitForSeconds(intervaloSegundos);
        }

        CombateTerminado?.Invoke(resultado);
    }

    public static bool MismoBattleLog(ResultadoCombateDto a, ResultadoCombateDto b)
    {
        if (a == null || b == null || a.BattleLog == null || b.BattleLog == null)
            return false;
        if (a.BattleLog.Count != b.BattleLog.Count)
            return false;

        for (int i = 0; i < a.BattleLog.Count; i++)
        {
            var x = a.BattleLog[i];
            var y = b.BattleLog[i];
            if (x.turno != y.turno || x.atacante != y.atacante || x.objetivo != y.objetivo ||
                x.dano != y.dano || x.vidaRestante != y.vidaRestante)
                return false;
        }
        return true;
    }
}