using UnityEngine;
using Unity.Services.CloudCode;
using UnityEngine.InputSystem;
using System.Threading.Tasks;
using System.Collections.Generic; // IMPORTANTE: Librería necesaria para el Dictionary

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
    public async void ComprarGuerrero()
    {
        try
        {
            Debug.Log("Llamando a la Frontera de Confianza (ComprarHeroe)...");

            // CORRECCIÓN: Usamos Dictionary<string, object> tal como exige Unity
            var argumentosCompra = new Dictionary<string, object>
            {
                { "idCompra", "COMPRA_GUERRERO" },
                { "idOperacion", System.Guid.NewGuid().ToString() } // Genera un ID único para la transacción
            };

            // Llamamos al nuevo módulo que acabamos de subir a la nube
            string respuesta = await CloudCodeService.Instance.CallModuleEndpointAsync<string>(
                "Tienda", "ComprarHeroe", argumentosCompra);

            Debug.Log($"<color=green>¡Compra procesada!</color> Respuesta: {respuesta}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error en la compra: {e.Message}");
        }
    }
}
