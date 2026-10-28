using System.Collections.Generic; // IMPORTANTE: Librería necesaria para el Dictionary
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using Unity.Services.CloudSave;
using UnityEngine;
using UnityEngine.InputSystem;

public class PruebaTienda : MonoBehaviour
{
    private InputAction accionEspacio;

    void Start()
    {
        // Actualizamos la barra espaciadora para que llame a la nueva función
        accionEspacio = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/space");
        accionEspacio.performed += ctx => ComprarGuerrero();
        accionEspacio.Enable();
    }

    private void OnDestroy()
    {
        // Limpiamos la suscripción al destruir el objeto
        if (accionEspacio != null)
        {
            accionEspacio.Disable();
            accionEspacio.Dispose();
        }
    }

    // Nueva función para comprar el guerrero desde el botón o el teclado
    // Nueva función para comprar el guerrero desde el botón o el teclado
    public async void ComprarGuerrero()
    {
        try
        {
            Debug.Log("Llamando a la Frontera de Confianza (ComprarHeroe)...");

            var argumentosCompra = new Dictionary<string, object>
            {
                { "idCompra", "COMPRA_GUERRERO" },
                { "idOperacion", System.Guid.NewGuid().ToString("N") }
            };

            // CORRECCIÓN 1: Pasamos 'argumentosCompra' correctamente
            var resultado = await CloudCodeService.Instance
                .CallModuleEndpointAsync<ResultadoCompraCloud>(
                    "Tienda", "ComprarHeroe", argumentosCompra);

            // CORRECCIÓN 2: Usamos 'resultado' para imprimir
            Debug.Log($"<color=green>¿Éxito?: {resultado.exito}</color> Mensaje: {resultado.mensaje}");


            // --- CÓDIGO DEL EJERCICIO 7 ---
            // Leemos el oro y el inventario desde Cloud Save (la capa protegida)
            var keys = new HashSet<string> { "ORO", "inventario" };
            var datos = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

            Debug.Log("Saldo e inventario actualizados desde la nube.");

        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error en la compra: {e.Message}");
        }
    }
}

[System.Serializable]
public class ResultadoCompraCloud
{
    public bool exito;
    public bool repetida;
    public string mensaje;
    public string itemEntregado;
    public int nuevoSaldo;
}
