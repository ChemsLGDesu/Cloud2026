using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using Unity.Services.CloudSave.Models.Data.Player;
using UnityEngine;

[Serializable]
public class HeroeInventarioDto
{
    public string id;
    public int nivel;
    public int experiencia;
}

[Serializable]
public class InventarioDataDto
{
    public List<HeroeInventarioDto> heroes = new List<HeroeInventarioDto>();
    public List<string> objetos = new List<string>();
}

[Serializable]
public class PerfilDataDto
{
    public int nivelCuenta = 1;
    public int experiencia = 0;
}

[Serializable]
public class ProgresoDataDto
{
    public int combatesGanados;
    public int combatesPerdidos;
    public int trofeos;
    public List<string> desbloqueos = new List<string>();
}

[Serializable]
public class ResultadoNodoDto
{
    public bool Exito;
    public string Mensaje;
    public string NodoId;
    public int SaldoResultante;
    public List<string> Desbloqueos;
}

public class ProgresionCliente : MonoBehaviour
{
    public PerfilDataDto Perfil { get; private set; } = new PerfilDataDto();
    public ProgresoDataDto Progreso { get; private set; } = new ProgresoDataDto();
    public InventarioDataDto Inventario { get; private set; } = new InventarioDataDto();
    public int Oro { get; private set; }
    public int Gemas { get; private set; }

    public async Task RefrescarAsync()
    {
        var keys = new HashSet<string> { "saldos", "inventario", "progreso", "perfil" };
        var data = await CloudSaveService.Instance.Data.Player.LoadAsync(
            keys, new LoadOptions(new ProtectedReadAccessClassOptions()));

        if (data.TryGetValue("perfil", out var pItem))
            Perfil = pItem.Value.GetAs<PerfilDataDto>() ?? new PerfilDataDto();

        if (data.TryGetValue("progreso", out var prItem))
            Progreso = prItem.Value.GetAs<ProgresoDataDto>() ?? new ProgresoDataDto();

        if (data.TryGetValue("inventario", out var invItem))
            Inventario = invItem.Value.GetAs<InventarioDataDto>() ?? new InventarioDataDto();

        Oro = 0;
        Gemas = 0;
        if (data.TryGetValue("saldos", out var sItem))
        {
            var dict = sItem.Value.GetAs<Dictionary<string, object>>();
            if (dict != null && dict.TryGetValue("monedas", out var mObj) && mObj != null)
            {
                var monedas = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, int>>(mObj.ToString());
                if (monedas != null)
                {
                    if (monedas.TryGetValue("ORO", out int o)) Oro = o;
                    if (monedas.TryGetValue("GEMAS", out int g)) Gemas = g;
                }
            }
        }
    }

    public async Task<ResultadoNodoDto> DesbloquearNodoAsync(string idNodo)
    {
        return await CloudCodeService.Instance.CallModuleEndpointAsync<ResultadoNodoDto>(
            "Tienda", "DesbloquearNodo",
            new Dictionary<string, object> { { "idNodo", idNodo } });
    }

    public string ResumenCorto()
    {
        string heroesStr = Inventario.heroes.Count == 0
            ? "ninguno"
            : string.Join(", ", Inventario.heroes.ConvertAll(h => $"{h.id} n{h.nivel} ({h.experiencia} xp)"));
        string objsStr = Inventario.objetos.Count == 0 ? "[]" : $"[{string.Join(", ", Inventario.objetos)}]";
        string nodosStr = Progreso.desbloqueos.Count == 0 ? "[]" : $"[{string.Join(", ", Progreso.desbloqueos)}]";
        return $"cuenta nivel {Perfil.nivelCuenta} ({Perfil.experiencia} xp) · héroes [{heroesStr}] · objetos {objsStr} · nodos {nodosStr} · ORO {Oro} GEMAS {Gemas}";
    }
}