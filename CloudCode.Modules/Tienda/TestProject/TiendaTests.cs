using Dvsc.CloudCode;
using NUnit.Framework;

namespace TestProject;

public class TiendaTests
{
    [Test]
    public void Metricas_IniciaEnCero_YAcumulaOperaciones()
    {
        var c = Metricas.Iniciar();
        Metricas.Leer(c, 4);
        Metricas.Escribir(c, 2);
        Assert.That(c.Lecturas, Is.EqualTo(4));
        Assert.That(c.Escrituras, Is.EqualTo(2));
    }

    [Test]
    public void RespuestaHora_ConservaKitEsperado()
    {
        var r = new RespuestaHora { entorno = "development", kit = "dvsc-20261105" };
        Assert.That(r.kit, Is.EqualTo("dvsc-20261105"));
    }
}


