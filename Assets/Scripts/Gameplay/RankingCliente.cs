using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Exceptions;
using Unity.Services.Leaderboards.Models;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class EntradaRankingDto
{
    public string playerId;
    public string playerName;
    public int rank;
    public double score;
    public string tier;
}

[Serializable]
public class PaginaRankingDto
{
    public List<EntradaRankingDto> results = new List<EntradaRankingDto>();
}

[Serializable]
public class ResultadoTemporadaDto
{
    public bool Exito;
    public string NombreTemporada;
    public int Puesto;
    public double Trofeos;
    public string Tier;
    public string Premio;
    public string CierreUtc;
    public string HoraServidorUtc;
    public long SegundosRestantes;
    public string TiempoRestanteTexto;
}

public class RankingCliente : MonoBehaviour
{
    public const string IdTabla = "trofeos_dvsc";
    private string BaseUrl => $"https://leaderboards.services.api.unity.com/v1/projects/{Application.cloudProjectId}/leaderboards/{IdTabla}";

    private static EntradaRankingDto Mapear(LeaderboardEntry e) => new EntradaRankingDto
    {
        playerId = e.PlayerId,
        playerName = e.PlayerName,
        rank = e.Rank,
        score = e.Score,
        tier = e.Tier
    };

    public async Task<List<EntradaRankingDto>> RefrescarTopAsync(int limite = 10)
    {
        try
        {
            var page = await LeaderboardsService.Instance.GetScoresAsync(
                IdTabla, new GetScoresOptions { Offset = 0, Limit = limite });
            return page?.Results?.ConvertAll(Mapear) ?? new List<EntradaRankingDto>();
        }
        catch (ServicesInitializationException)
        {
            string json = await PeticionRestAsync("GET", $"{BaseUrl}/scores?offset=0&limit={limite}");
            var pagina = JsonUtility.FromJson<PaginaRankingDto>(json);
            return pagina?.results ?? new List<EntradaRankingDto>();
        }
    }

    public async Task<EntradaRankingDto> ObtenerPropioAsync()
    {
        try
        {
            var entry = await LeaderboardsService.Instance.GetPlayerScoreAsync(IdTabla);
            return entry != null ? Mapear(entry) : null;
        }
        catch (LeaderboardsException ex) when (ex.Reason == LeaderboardsExceptionReason.EntryNotFound)
        {
            return null;
        }
        catch (ServicesInitializationException)
        {
            try
            {
                string pid = AuthenticationService.Instance.PlayerId;
                string json = await PeticionRestAsync("GET", $"{BaseUrl}/scores/players/{pid}");
                return JsonUtility.FromJson<EntradaRankingDto>(json);
            }
            catch (Exception ex) when (ex.Message.Contains("404"))
            {
                return null;
            }
        }
        catch (Exception ex) when (ex.Message.Contains("404") || ex.Message.Contains("Not Found"))
        {
            return null;
        }
    }

    public async Task<List<EntradaRankingDto>> ObtenerVecinosAsync(int rangoLimite = 5)
    {
        try
        {
            var page = await LeaderboardsService.Instance.GetPlayerRangeAsync(
                IdTabla, new GetPlayerRangeOptions { RangeLimit = rangoLimite });
            return page?.Results?.ConvertAll(Mapear) ?? new List<EntradaRankingDto>();
        }
        catch (ServicesInitializationException)
        {
            try
            {
                string pid = AuthenticationService.Instance.PlayerId;
                string json = await PeticionRestAsync("GET", $"{BaseUrl}/scores/players/{pid}/range?rangeLimit={rangoLimite}");
                var pagina = JsonUtility.FromJson<PaginaRankingDto>(json);
                return pagina?.results ?? new List<EntradaRankingDto>();
            }
            catch (Exception ex) when (ex.Message.Contains("404"))
            {
                return new List<EntradaRankingDto>();
            }
        }
        catch (Exception ex) when (ex.Message.Contains("404") || ex.Message.Contains("Not Found"))
        {
            return new List<EntradaRankingDto>();
        }
    }

    public async Task<EntradaRankingDto> IntentarEnviarPuntajeDesdeClienteAsync(double puntaje)
    {
        try
        {
            var entry = await LeaderboardsService.Instance.AddPlayerScoreAsync(IdTabla, puntaje);
            return Mapear(entry);
        }
        catch (ServicesInitializationException)
        {
            string pid = AuthenticationService.Instance.PlayerId;
            string json = await PeticionRestAsync("POST", $"{BaseUrl}/scores/players/{pid}", $"{{\"score\":{puntaje}}}");
            return JsonUtility.FromJson<EntradaRankingDto>(json);
        }
    }

    public async Task<ResultadoTemporadaDto> VistaTemporadaAsync()
    {
        return await CloudCodeService.Instance.CallModuleEndpointAsync<ResultadoTemporadaDto>(
            "Tienda", "VistaTemporada", new Dictionary<string, object>());
    }

    private async Task<string> PeticionRestAsync(string metodo, string url, string bodyJson = null)
    {
        using var req = new UnityWebRequest(url, metodo);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Authorization", $"Bearer {AuthenticationService.Instance.AccessToken}");
        if (!string.IsNullOrEmpty(bodyJson))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(bodyJson));
            req.SetRequestHeader("Content-Type", "application/json");
        }

        var op = req.SendWebRequest();
        while (!op.isDone)
            await Task.Yield();

        if (req.result != UnityWebRequest.Result.Success)
            throw new Exception($"({req.responseCode}) {req.downloadHandler.text}");

        return req.downloadHandler.text;
    }
}