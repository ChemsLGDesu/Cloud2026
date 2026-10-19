using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace Tienda
{
    public class ModuloTienda
    {
        private readonly ILogger<ModuloTienda> _logger;

        public ModuloTienda(ILogger<ModuloTienda> logger)
        {
            _logger = logger;
        }

        [CloudCodeFunction("HoraDelServidor")]
        public string HoraDelServidor(IExecutionContext context)
        {
            return $"Hora: {DateTime.UtcNow}, Entorno: {context.EnvironmentName}, PlayerId: {context.PlayerId}";
        }

        // --- EJERCICIO 3: LA FRONTERA DE CONFIANZA ---
        [CloudCodeFunction("ComprarHeroe")]
        public async Task<string> ComprarHeroe(IExecutionContext context, IGameApiClient gameApiClient, string idCompra, string idOperacion)
        {
            _logger.LogInformation($"Iniciando ComprarHeroe. PlayerId: {context.PlayerId}, idCompra: {idCompra}");

            try
            {
                // PASO 1: Leer Remote Config (Usamos el AccessToken normal del contexto)
                var remoteConfigResponse = await gameApiClient.RemoteConfigSettings.AssignSettingsGetAsync(
                    context,
                    context.AccessToken,
                    context.ProjectId,
                    context.EnvironmentId,
                    "settings",
                    new List<string> { "catalogo_economia" },
                    null);

                string? catalogoJsonStr = null;
                if (remoteConfigResponse.Data?.Configs != null)
                {
                    string configsJson = JsonSerializer.Serialize(remoteConfigResponse.Data.Configs);
                    JsonNode? configsNode = JsonNode.Parse(configsJson);
                    catalogoJsonStr = configsNode?["settings"]?["catalogo_economia"]?.ToString();
                }

                if (string.IsNullOrEmpty(catalogoJsonStr))
                {
                    catalogoJsonStr = "{\"compras\":[{\"id\":\"COMPRA_GUERRERO\",\"cuestaMoneda\":\"ORO\",\"cuestaCantidad\":500,\"entregaItem\":\"HEROE_GUERRERO\"},{\"id\":\"COMPRA_MAGO\",\"cuestaMoneda\":\"ORO\",\"cuestaCantidad\":1200,\"entregaItem\":\"HEROE_MAGO\"}]}";
                }

                JsonNode? catalogo = JsonNode.Parse(catalogoJsonStr);
                JsonNode? compraInfo = null;
                JsonArray? comprasArray = catalogo?["compras"]?.AsArray();

                if (comprasArray != null)
                {
                    foreach (var compra in comprasArray)
                    {
                        if (compra?["id"]?.ToString() == idCompra)
                        {
                            compraInfo = compra;
                            break;
                        }
                    }
                }

                if (compraInfo == null)
                {
                    return $"ERROR: Compra {idCompra} no existe en el catálogo.";
                }

                string monedaCobro = compraInfo["cuestaMoneda"]?.ToString() ?? "ORO";
                int precioHeroe = int.Parse(compraInfo["cuestaCantidad"]?.ToString() ?? "500");
                string itemAEntregar = compraInfo["entregaItem"]?.ToString() ?? "HEROE_GUERRERO";

                // PASO 2: Leer saldos en la clase PROTECTED
                var cloudSaveKeys = new List<string> { monedaCobro, "operaciones" };

                var saldosResponse = await gameApiClient.CloudSaveData.GetProtectedItemsAsync(
                    context,
                    context.AccessToken,
                    context.ProjectId,
                    context.PlayerId,
                    cloudSaveKeys,
                    null);

                int saldoActual = 0;
                if (saldosResponse.Data?.Results != null)
                {
                    foreach (var item in saldosResponse.Data.Results)
                    {
                        if (item.Key == monedaCobro && item.Value != null)
                        {
                            if (int.TryParse(item.Value.ToString(), out int parsedSaldo))
                            {
                                saldoActual = parsedSaldo;
                            }
                        }
                    }
                }

                if (saldoActual == 0 && monedaCobro == "ORO") saldoActual = 1000;

                // PASO 3: Validar fondos
                if (saldoActual < precioHeroe)
                {
                    return "ERROR: Fondos insuficientes";
                }

                int nuevoSaldo = saldoActual - precioHeroe;

                // PASO 4: Cobrar y guardar (AQUÍ ESTÁ LA MAGIA: Usamos ServiceToken)
                await gameApiClient.CloudSaveData.SetProtectedItemAsync(
                    context,
                    context.ServiceToken, // <- ESTO EVITA EL UNAUTHORIZED
                    context.ProjectId,
                    context.PlayerId,
                    new SetItemBody(monedaCobro, nuevoSaldo));

                await gameApiClient.CloudSaveData.SetProtectedItemAsync(
                    context,
                    context.ServiceToken, // <- ESTO EVITA EL UNAUTHORIZED
                    context.ProjectId,
                    context.PlayerId,
                    new SetItemBody("ultima_operacion", idOperacion));

                return $"EXITO: {idCompra} completada. Item: {itemAEntregar}. Saldo restante: {nuevoSaldo} {monedaCobro}";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error en ComprarHeroe: {ex.Message}");
                return $"ERROR_INTERNO: {ex.Message}";
            }
        }
    }
}