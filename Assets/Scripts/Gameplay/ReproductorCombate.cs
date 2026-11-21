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
public class ResultadoCombateDto
{
    public bool Exito;
    public string Mensaje;
    public bool Repeticion;
    public bool Victoria;
    public int TurnosJugados;
    public int Semilla;
    public string Rival;
    public List<UnidadCombateDto> EquipoPropio;
    public List<UnidadCombateDto> EquipoRival;
    public List<EntradaBattleLogDto> BattleLog;
    public RecompensaCombateDto Recompensa;
    public int TrofeosCambio;
    public int SaldoResultante;
}

public class ReproductorCombate : MonoBehaviour
{
    public event Action<EntradaBattleLogDto> AccionReproducida;
    public event Action<ResultadoCombateDto> CombateTerminado;

    public async Task<ResultadoCombateDto> PelearAsync(IList<string> equipo, string idRival)
    {
        var args = new Dictionary<string, object>
        {
            { "equipo", equipo },
            { "idRival", idRival }
        };

        return await CloudCodeService.Instance.CallModuleEndpointAsync<ResultadoCombateDto>(
            "Tienda", "ResolverCombate", args);
    }

    public async Task<ResultadoCombateDto> RepetirAsync()
    {
        return await CloudCodeService.Instance.CallModuleEndpointAsync<ResultadoCombateDto>(
            "Tienda", "RepetirCombate", new Dictionary<string, object>());
    }

    public IEnumerator Reproducir(ResultadoCombateDto resultado, float intervaloSegundos = 0.6f)
    {
        if (resultado == null || !resultado.Exito || resultado.BattleLog == null)
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
        if (a?.BattleLog == null || b?.BattleLog == null) return false;
        if (a.BattleLog.Count != b.BattleLog.Count) return false;

        for (int i = 0; i < a.BattleLog.Count; i++)
        {
            var x = a.BattleLog[i];
            var y = b.BattleLog[i];
            if (x.turno != y.turno ||
                x.atacante != y.atacante ||
                x.objetivo != y.objetivo ||
                x.dano != y.dano ||
                x.vidaRestante != y.vidaRestante)
            {
                return false;
            }
        }
        return true;
    }
}