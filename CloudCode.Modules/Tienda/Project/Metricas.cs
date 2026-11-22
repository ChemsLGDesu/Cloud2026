using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Dvsc.CloudCode;

public sealed class ContadorMetricas
{
    public Stopwatch Cronometro { get; } = Stopwatch.StartNew();
    public int Lecturas { get; set; }
    public int Escrituras { get; set; }
}

public static class Metricas
{
    public static ContadorMetricas Iniciar() => new ContadorMetricas();

    public static void Leer(ContadorMetricas c, int n = 1) => c.Lecturas += n;

    public static void Escribir(ContadorMetricas c, int n = 1) => c.Escrituras += n;

    public static void Registrar(ILogger logger, string operacion, string? idOperacion, string resultado, ContadorMetricas c)
    {
        c.Cronometro.Stop();
        string idSeguro = string.IsNullOrWhiteSpace(idOperacion) ? "-" : idOperacion;
        logger.LogInformation(
            "operacion={Operacion} id={Id} resultado={Resultado} duracionMs={DuracionMs} lecturas={Lecturas} escrituras={Escrituras}",
            operacion, idSeguro, resultado, c.Cronometro.ElapsedMilliseconds, c.Lecturas, c.Escrituras);
    }
}
