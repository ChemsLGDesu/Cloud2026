using System;
using Unity.Services.CloudCode.Core;

namespace Tienda
{
    public class ModuloTienda
    {
        [CloudCodeFunction("HoraDelServidor")]
        public string HoraDelServidor(IExecutionContext context)
        {
            // Devuelve la información exacta que exige el laboratorio 07
            return $"Hora: {DateTime.UtcNow}, Entorno: {context.EnvironmentName}, PlayerId: {context.PlayerId}";
        }
    }
}
