using UnityEngine;
using Unity.Services.CloudCode;
using UnityEngine.InputSystem;
using System.Threading.Tasks;

public class PruebaTienda : MonoBehaviour
{
    private InputAction accionEspacio;

    void Start()
    {
        // Creamos la acción vinculada a la barra espaciadora y nos suscribimos al evento
        accionEspacio = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/space");
        accionEspacio.performed += ctx => LlamarServidor();
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

    private async void LlamarServidor()
    {
        try
        {
            Debug.Log("Llamando al módulo Tienda en Unity Cloud...");

            string respuesta = await CloudCodeService.Instance.CallModuleEndpointAsync<string>("Tienda", "HoraDelServidor", null);

            Debug.Log("<color=green>¡El circuito está cerrado!</color> Respuesta: " + respuesta);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error al conectar con Cloud Code: " + e.Message);
        }
    }
}