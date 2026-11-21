using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Tienda;

namespace TestProject
{
    public class UnitTest
    {
        private UnidadCombate Guerrero() => new UnidadCombate { Id = "HEROE_GUERRERO", Vida = 140, Ataque = 12, Defensa = 9, Velocidad = 8 };
        private UnidadCombate Arquera()  => new UnidadCombate { Id = "HEROE_ARQUERA",  Vida = 90,  Ataque = 17, Defensa = 4, Velocidad = 14 };
        private UnidadCombate Mago()     => new UnidadCombate { Id = "HEROE_MAGO",     Vida = 80,  Ataque = 22, Defensa = 3, Velocidad = 11 };

        // --- 7 PRUEBAS DE COMBATE (S08) ---

        [Test]
        public void T01_Simulacion_MismaSemilla_EsDeterminista()
        {
            var r1 = ModuloCombate.Simular(new List<UnidadCombate> { Guerrero() }, new List<UnidadCombate> { Arquera() }, 12345);
            var r2 = ModuloCombate.Simular(new List<UnidadCombate> { Guerrero() }, new List<UnidadCombate> { Arquera() }, 12345);

            Assert.That(r2.Victoria, Is.EqualTo(r1.Victoria));
            Assert.That(r2.TurnosJugados, Is.EqualTo(r1.TurnosJugados));
            Assert.That(r2.BattleLog.Count, Is.EqualTo(r1.BattleLog.Count));
            for (int i = 0; i < r1.BattleLog.Count; i++)
            {
                Assert.That(r2.BattleLog[i].dano, Is.EqualTo(r1.BattleLog[i].dano));
                Assert.That(r2.BattleLog[i].vidaRestante, Is.EqualTo(r1.BattleLog[i].vidaRestante));
            }
        }

        [Test]
        public void T02_Simulacion_UnidadMasRapidaAtacaPrimero()
        {
            var r = ModuloCombate.Simular(new List<UnidadCombate> { Guerrero() }, new List<UnidadCombate> { Arquera() }, 777);
            Assert.That(r.BattleLog[0].atacante, Is.EqualTo("HEROE_ARQUERA"));
        }

        [Test]
        public void T03_Simulacion_DanoMinimoSiempreEsUno()
        {
            var tanque = new UnidadCombate { Id = "TANQUE", Vida = 50, Ataque = 5, Defensa = 999, Velocidad = 5 };
            var debil  = new UnidadCombate { Id = "DEBIL",  Vida = 10, Ataque = 1, Defensa = 0,   Velocidad = 10 };
            var r = ModuloCombate.Simular(new List<UnidadCombate> { debil }, new List<UnidadCombate> { tanque }, 99);
            Assert.That(r.BattleLog.All(e => e.dano >= 1), Is.True);
        }

        [Test]
        public void T04_Simulacion_NoMutuaLasUnidadesOriginales()
        {
            var g = Guerrero();
            var a = Arquera();
            ModuloCombate.Simular(new List<UnidadCombate> { g }, new List<UnidadCombate> { a }, 555);
            Assert.That(g.Vida, Is.EqualTo(140));
            Assert.That(a.Vida, Is.EqualTo(90));
        }

        [Test]
        public void T05_Simulacion_RespetaTopeDeTurnos()
        {
            var u1 = new UnidadCombate { Id = "U1", Vida = 99999, Ataque = 1, Defensa = 50, Velocidad = 10 };
            var u2 = new UnidadCombate { Id = "U2", Vida = 99999, Ataque = 1, Defensa = 50, Velocidad = 9 };
            var r = ModuloCombate.Simular(new List<UnidadCombate> { u1 }, new List<UnidadCombate> { u2 }, 1);
            Assert.That(r.TurnosJugados, Is.EqualTo(100));
            Assert.That(r.Victoria, Is.False);
        }

        [Test]
        public void T06_Simulacion_RegistraVidaFinalPropio()
        {
            var r = ModuloCombate.Simular(new List<UnidadCombate> { Guerrero() }, new List<UnidadCombate> { Arquera() }, 202059266);
            Assert.That(r.VidaFinalPropio.Count, Is.EqualTo(1));
            Assert.That(r.Victoria ? r.VidaFinalPropio[0] > 0 : r.VidaFinalPropio[0] == 0, Is.True);
        }

        [Test]
        public void T07_Simulacion_EquipoMultipleEliminaEnOrden()
        {
            var r = ModuloCombate.Simular(
                new List<UnidadCombate> { Guerrero(), Mago() },
                new List<UnidadCombate> { Arquera() },
                42);
            Assert.That(r.Victoria, Is.True);
            Assert.That(r.VidaFinalPropio.Count, Is.EqualTo(2));
        }

        // --- 13 PRUEBAS DE PROGRESIÃ“N (S10) ---

        [Test]
        public void T08_Experiencia_VictoriaConTurnosBajoTope()
        {
            var cfg = new ConfigExperiencia { Victoria = 50, Derrota = 15, PorTurno = 1, TopeTurnos = 20 };
            Assert.That(ReglasProgresion.ExperienciaDelCombate(cfg, true, 11), Is.EqualTo(61));
            Assert.That(ReglasProgresion.ExperienciaDelCombate(cfg, true, 12), Is.EqualTo(62));
        }

        [Test]
        public void T09_Experiencia_RespetaTopeDeTurnos()
        {
            var cfg = new ConfigExperiencia { Victoria = 50, Derrota = 15, PorTurno = 1, TopeTurnos = 20 };
            Assert.That(ReglasProgresion.ExperienciaDelCombate(cfg, true, 100), Is.EqualTo(70));
        }

        [Test]
        public void T10_Experiencia_DerrotaOtorgaBaseMasTurnos()
        {
            var cfg = new ConfigExperiencia { Victoria = 50, Derrota = 15, PorTurno = 1, TopeTurnos = 20 };
            Assert.That(ReglasProgresion.ExperienciaDelCombate(cfg, false, 11), Is.EqualTo(26));
        }

        [Test]
        public void T11_CostoParaSubir_EsCuadratico()
        {
            var niv = new ConfigNiveles { CostoBase = 100, Maximo = 10 };
            Assert.That(ReglasProgresion.CostoParaSubir(niv, 1), Is.EqualTo(100));
            Assert.That(ReglasProgresion.CostoParaSubir(niv, 2), Is.EqualTo(400));
            Assert.That(ReglasProgresion.CostoParaSubir(niv, 3), Is.EqualTo(900));
        }

        [Test]
        public void T12_AplicarNivel_SubeUnNivelYConservaSobrante()
        {
            var niv = new ConfigNiveles { CostoBase = 100, Maximo = 10 };
            var (nivel, exp, subidos) = ReglasProgresion.Aplicar(niv, 1, 62, 62);
            Assert.That(nivel, Is.EqualTo(2));
            Assert.That(exp, Is.EqualTo(24));
            Assert.That(subidos, Is.EqualTo(1));
        }

        [Test]
        public void T13_AplicarNivel_SubeVariosNivelesEnBucle()
        {
            var niv = new ConfigNiveles { CostoBase = 100, Maximo = 10 };
            var (nivel, exp, subidos) = ReglasProgresion.Aplicar(niv, 1, 90, 600);
            Assert.That(nivel, Is.EqualTo(3));
            Assert.That(exp, Is.EqualTo(190));
            Assert.That(subidos, Is.EqualTo(2));
        }

        [Test]
        public void T14_AplicarNivel_EnNivelMaximoDescartaExperiencia()
        {
            var niv = new ConfigNiveles { CostoBase = 100, Maximo = 10 };
            var (nivel, exp, subidos) = ReglasProgresion.Aplicar(niv, 10, 0, 500);
            Assert.That(nivel, Is.EqualTo(10));
            Assert.That(exp, Is.EqualTo(0));
            Assert.That(subidos, Is.EqualTo(0));
        }

        [Test]
        public void T15_BonoNivel_EscalaStatsCorrectamente()
        {
            var niv = new ConfigNiveles { BonoPorNivel = 0.05 };
            Assert.That(ReglasProgresion.AplicarBonoNivel(niv, 140, 1), Is.EqualTo(140));
            Assert.That(ReglasProgresion.AplicarBonoNivel(niv, 140, 3), Is.EqualTo(154));
            Assert.That(ReglasProgresion.AplicarBonoNivel(niv, 12, 3),  Is.EqualTo(13));
            Assert.That(ReglasProgresion.AplicarBonoNivel(niv, 9, 3),   Is.EqualTo(10));
        }

        [Test]
        public void T16_Botin_MismaSemillaProduceMismoBotin()
        {
            var botin = new ConfigBotin
            {
                Tiradas = 1,
                Premios = new List<PremioBotin>
                {
                    new PremioBotin { Id = "NADA", Tipo = "nada", Cantidad = 0, Peso = 40 },
                    new PremioBotin { Id = "CAJA_BASICA", Tipo = "objeto", Cantidad = 1, Peso = 40 },
                    new PremioBotin { Id = "GEMAS", Tipo = "moneda", Cantidad = 20, Peso = 20 }
                }
            };

            var b1 = ReglasProgresion.TirarBotin(botin, 8888);
            var b2 = ReglasProgresion.TirarBotin(botin, 8888);
            Assert.That(b2.Count, Is.EqualTo(b1.Count));
            if (b1.Count > 0) Assert.That(b2[0].Id, Is.EqualTo(b1[0].Id));
        }

        [Test]
        public void T17_Botin_DistribucionPonderada4000Tiradas()
        {
            var botin = new ConfigBotin
            {
                Tiradas = 1,
                Premios = new List<PremioBotin>
                {
                    new PremioBotin { Id = "NADA", Tipo = "nada", Cantidad = 0, Peso = 40 },
                    new PremioBotin { Id = "CAJA_BASICA", Tipo = "objeto", Cantidad = 1, Peso = 40 },
                    new PremioBotin { Id = "GEMAS", Tipo = "moneda", Cantidad = 20, Peso = 20 }
                }
            };

            int cajas = 0, gemas = 0, nada = 0;
            for (int s = 1; s <= 4000; s++)
            {
                var r = ReglasProgresion.TirarBotin(botin, s);
                if (r.Count == 0) nada++;
                else if (r[0].Id == "CAJA_BASICA") cajas++;
                else if (r[0].Id == "GEMAS") gemas++;
            }

            Assert.That(nada / 4000.0, Is.InRange(0.36, 0.44));
            Assert.That(cajas / 4000.0, Is.InRange(0.36, 0.44));
            Assert.That(gemas / 4000.0, Is.InRange(0.16, 0.24));
        }

        [Test]
        public void T18_Nodo_RechazaSiFaltanPrerrequisitos()
        {
            var nodo = new NodoProgresion { Id = "TALENTO_ATAQUE_2", Nombre = "Ataque II", NivelCuentaMinimo = 2, CuestaMoneda = "ORO", CuestaCantidad = 300, Requiere = new List<string> { "TALENTO_ATAQUE_1" } };
            string motivo = ReglasProgresion.PorQueNoSePuede(nodo, new List<string>(), 5, 1000);
            Assert.That(motivo, Does.Contain("TALENTO_ATAQUE_1"));
        }

        [Test]
        public void T19_Nodo_RechazaSiYaEstaDesbloqueadoONivelInsuficiente()
        {
            var nodo = new NodoProgresion { Id = "TALENTO_ATAQUE_1", Nombre = "Ataque I", NivelCuentaMinimo = 2, CuestaMoneda = "ORO", CuestaCantidad = 100, Requiere = new List<string>() };
            Assert.That(ReglasProgresion.PorQueNoSePuede(nodo, new List<string> { "TALENTO_ATAQUE_1" }, 2, 500), Does.Contain("ya estÃ¡ desbloqueado"));
            Assert.That(ReglasProgresion.PorQueNoSePuede(nodo, new List<string>(), 1, 500), Does.Contain("nivel de cuenta 2"));
        }

        [Test]
        public void T20_Nodo_PermiteDesbloquearCuandoCumpleTodo()
        {
            var nodo = new NodoProgresion { Id = "TALENTO_ATAQUE_2", Nombre = "Ataque II", NivelCuentaMinimo = 2, CuestaMoneda = "ORO", CuestaCantidad = 300, Requiere = new List<string> { "TALENTO_ATAQUE_1" } };
            string motivo = ReglasProgresion.PorQueNoSePuede(nodo, new List<string> { "TALENTO_ATAQUE_1" }, 2, 300);
            Assert.That(motivo, Is.Null);
        }

        // --- PRUEBAS DE TROFEOS Y SUELO EN CERO (S11) ---

        [Test]
        public void T21_Trofeos_VictoriaSumaYDerrotaConSueloEnCero()
        {
            Assert.That(ReglasProgresion.CambioDeTrofeos(0, true, 5), Is.EqualTo(5));
            Assert.That(ReglasProgresion.CambioDeTrofeos(5, false, 20), Is.EqualTo(-5));
            Assert.That(ReglasProgresion.CambioDeTrofeos(0, false, 20), Is.EqualTo(0));
        }

        [Test]
        public void T22_Trofeos_DiezMilCombatesAlAzarNuncaBajanDeCero()
        {
            var azar = new Random(202611);
            int trofeos = 0;
            for (int i = 0; i < 10000; i++)
            {
                bool victoria = azar.Next(2) == 0;
                int pagoRival = azar.Next(1, 30);
                int delta = ReglasProgresion.CambioDeTrofeos(trofeos, victoria, pagoRival);
                trofeos += delta;
                Assert.That(trofeos, Is.GreaterThanOrEqualTo(0));
            }
        }
    }
}