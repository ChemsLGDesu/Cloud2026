using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using Unity.Services.CloudSave.Models.Data.Player;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Networking;
using SaveOptions = Unity.Services.CloudSave.Models.Data.Player.SaveOptions;

[Serializable]
public class DefensaPublicaDto
{
    public string nombre;
    public int nivelCuenta;
    public int trofeos;
    public List<string> equipo = new List<string>();
    public string actualizadaUtc;
}

[Serializable]
public class RivalEncontradoDto
{
    public string PlayerId;
    public string Nombre;
    public int Nivel;
    public int Trofeos;
    public List<string> Equipo;
}

[Serializable]
public class ResultadoBusquedaRivalDto
{
    public bool Hubo;
    public string Mensaje;
    public int NivelSolicitante;
    public int Franja;
    public int Candidatos;
    public int Consultas;
    public int LecturasConsumidas;
    public RivalEncontradoDto Rival;
}

public class RivalesCliente : MonoBehaviour
{
    public const string LlaveDefensa = "defensa";
    public const string LlaveNivel = "nivel";

    public async Task PublicarDefensaAsync(string nombre, List<string> equipo, int nivel, int trofeos = 0)
    {
        var defensa = new DefensaPublicaDto
        {
            nombre = nombre,
            nivelCuenta = nivel,
            trofeos = trofeos,
            equipo = equipo ?? new List<string>(),
            actualizadaUtc = DateTime.UtcNow.ToString("o")
        };

        var datos = new Dictionary<string, object>
        {
            { LlaveDefensa, defensa },
            { LlaveNivel, nivel }
        };

        await CloudSaveService.Instance.Data.Player.SaveAsync(
            datos, new SaveOptions(new PublicWriteAccessClassOptions()));
    }

    public async Task<DefensaPublicaDto> LeerDefensaDeAsync(string playerId)
    {
        var ajena = await CloudSaveService.Instance.Data.Player.LoadAsync(
            new HashSet<string> { LlaveDefensa },
            new LoadOptions(new PublicReadAccessClassOptions(playerId)));

        if (ajena.TryGetValue(LlaveDefensa, out var item))
            return item.Value.GetAs<DefensaPublicaDto>();

        return null;
    }

    public async Task<ResultadoBusquedaRivalDto> BuscarRivalAsync()
    {
        return await CloudCodeService.Instance.CallModuleEndpointAsync<ResultadoBusquedaRivalDto>(
            "Tienda", "BuscarRival", new Dictionary<string, object>());
    }

    public async Task<(long statusCode, string body)> IntentarEscribirDefensaAjenaAsync(string idObjetivo)
    {
        string projectId = Application.cloudProjectId;
        string url = $"https://cloud-save.services.api.unity.com/v1/data/projects/{projectId}/players/{idObjetivo}/public/items";
        string json = "{\"key\":\"defensa\",\"value\":{\"nombre\":\"Hackeado\"}}";

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", $"Bearer {AuthenticationService.Instance.AccessToken}");

        var op = req.SendWebRequest();
        while (!op.isDone)
            await Task.Yield();

        return (req.responseCode, req.downloadHandler.text);
    }
}