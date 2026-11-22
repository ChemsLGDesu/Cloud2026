#pragma warning disable CS0618
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Analytics;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cloud2026.Services
{
    public class Telemetria : MonoBehaviour
    {
        public static Telemetria Instance { get; private set; }

        [Header("Consentimiento")]
        [SerializeField] private bool autoAceptarEnPruebas = false;

        public bool RecoleccionActiva { get; private set; }
        public bool ConsentimientoOtorgado { get; private set; }

        private TaskCompletionSource<bool> consentimientoTcs;
        private GameObject panelConsentimiento;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public async Task<bool> SolicitarConsentimientoEIniciarAsync(string externalUserId = null)
        {
            if (ConsentimientoOtorgado)
            {
                IniciarRecoleccion(externalUserId);
                return true;
            }

            if (autoAceptarEnPruebas)
            {
                ConsentimientoOtorgado = true;
                IniciarRecoleccion(externalUserId);
                return true;
            }

            consentimientoTcs = new TaskCompletionSource<bool>();
            MostrarPantallaConsentimiento();
            bool aceptado = await consentimientoTcs.Task;

            if (aceptado)
            {
                ConsentimientoOtorgado = true;
                IniciarRecoleccion(externalUserId);
            }

            return aceptado;
        }

        public void IniciarRecoleccion(string externalUserId = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(externalUserId))
                    UnityServices.ExternalUserId = externalUserId;

                AnalyticsService.Instance.StartDataCollection();
                RecoleccionActiva = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DVSC] No se pudo iniciar Analytics: {ex.Message}");
            }
        }

        public void CambiarJugador(string externalUserId)
        {
            if (string.IsNullOrEmpty(externalUserId)) return;
            Enviar();
            UnityServices.ExternalUserId = externalUserId;
            if (ConsentimientoOtorgado)
            {
                try { AnalyticsService.Instance.StartDataCollection(); }
                catch { }
            }
        }

        public void Registrar(string nombre, Dictionary<string, object> parametros = null)
        {
            if (!RecoleccionActiva) return;

            try
            {
                var evento = new CustomEvent(nombre);
                string detalle = string.Empty;

                if (parametros != null && parametros.Count > 0)
                {
                    foreach (var par in parametros)
                        evento[par.Key] = par.Value;

                    detalle = " " + string.Join(", ", parametros.Select(p => $"{p.Key}={p.Value}"));
                }

                AnalyticsService.Instance.RecordEvent(evento);
                Debug.Log($"[DVSC] evento registrado: {nombre}{detalle}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DVSC] Error registrando '{nombre}': {ex.Message}");
            }
        }

        public void RegistrarCombate(bool victoria, string heroe, int nivelRival, int turnos)
        {
            string nombreEvento = victoria ? Eventos.CombateGanado : Eventos.CombatePerdido;
            Registrar(nombreEvento, new Dictionary<string, object>
            {
                { "heroe", NormalizarHeroe(heroe) },
                { "nivel_rival", nivelRival },
                { "turnos", turnos }
            });
        }

        public void Enviar()
        {
            if (!RecoleccionActiva) return;
            try
            {
                AnalyticsService.Instance.Flush();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DVSC] Error al enviar lote Analytics: {ex.Message}");
            }
        }

        public static string NormalizarHeroe(string idHeroe)
        {
            if (string.IsNullOrWhiteSpace(idHeroe)) return "desconocido";
            string limpio = idHeroe.Trim();
            if (limpio.StartsWith("HEROE_", StringComparison.OrdinalIgnoreCase))
                limpio = limpio.Substring(6);
            return limpio.ToLowerInvariant();
        }

        private void MostrarPantallaConsentimiento()
        {
            if (panelConsentimiento != null)
            {
                panelConsentimiento.SetActive(true);
                return;
            }

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem_Telemetria");
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<StandaloneInputModule>();
            }

            string privacyUrl = "https://unity.com/legal/privacy-policy";
            try
            {
                if (!string.IsNullOrWhiteSpace(AnalyticsService.Instance.PrivacyUrl))
                    privacyUrl = AnalyticsService.Instance.PrivacyUrl;
            }
            catch { }

            var canvasGo = new GameObject("CanvasConsentimientoAnalytics");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();
            panelConsentimiento = canvasGo;

            var fondoGo = new GameObject("Fondo");
            fondoGo.transform.SetParent(canvasGo.transform, false);
            var fondoImg = fondoGo.AddComponent<Image>();
            fondoImg.color = new Color(0.05f, 0.07f, 0.11f, 0.88f);
            var fondoRt = fondoGo.GetComponent<RectTransform>();
            fondoRt.anchorMin = Vector2.zero;
            fondoRt.anchorMax = Vector2.one;
            fondoRt.offsetMin = Vector2.zero;
            fondoRt.offsetMax = Vector2.zero;

            var tarjetaGo = new GameObject("Tarjeta");
            tarjetaGo.transform.SetParent(fondoGo.transform, false);
            var tarjetaImg = tarjetaGo.AddComponent<Image>();
            tarjetaImg.color = new Color(0.13f, 0.17f, 0.24f, 1f);
            var tarjetaRt = tarjetaGo.GetComponent<RectTransform>();
            tarjetaRt.sizeDelta = new Vector2(540f, 300f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            CrearTexto(tarjetaGo.transform, font, "Privacidad y Telemetría del Juego", 22, FontStyle.Bold, new Vector2(0f, 100f), new Vector2(490f, 40f));
            CrearTexto(tarjetaGo.transform, font,
                "Usamos Unity Analytics para medir el recorrido inicial, compras y balance de combates sin recopilar datos personales.\nPolítica de privacidad:\n" + privacyUrl,
                14, FontStyle.Normal, new Vector2(0f, 15f), new Vector2(490f, 110f));

            CrearBoton(tarjetaGo.transform, font, "Ver Política", new Color(0.24f, 0.31f, 0.42f, 1f), new Vector2(-120f, -95f), new Vector2(200f, 44f), () =>
            {
                Application.OpenURL(privacyUrl);
            });

            CrearBoton(tarjetaGo.transform, font, "Aceptar y Continuar", new Color(0.16f, 0.58f, 0.34f, 1f), new Vector2(120f, -95f), new Vector2(220f, 44f), () =>
            {
                panelConsentimiento.SetActive(false);
                consentimientoTcs?.TrySetResult(true);
            });
        }

        private static void CrearTexto(Transform parent, Font font, string contenido, int size, FontStyle style, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject("Texto");
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<Text>();
            txt.font = font;
            txt.text = contenido;
            txt.fontSize = size;
            txt.fontStyle = style;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
        }

        private static void CrearBoton(Transform parent, Font font, string etiqueta, Color colorFondo, Vector2 pos, Vector2 sizeDelta, Action onClick)
        {
            var go = new GameObject("Boton_" + etiqueta);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = colorFondo;
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;

            CrearTexto(go.transform, font, etiqueta, 15, FontStyle.Bold, Vector2.zero, sizeDelta);
        }

        private void OnApplicationQuit() => Enviar();
    }
}
