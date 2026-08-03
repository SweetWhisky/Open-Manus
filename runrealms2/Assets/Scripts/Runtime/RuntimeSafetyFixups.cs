using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RunRealms2
{
    /// <summary>
    /// Applies runtime-only UI safety rules after the procedural Canvas is created.
    /// Decorative panels, labels and progress images must not consume lane/jump/slide gestures.
    /// Actual Selectables retain raycast handling.
    /// </summary>
    public sealed class RuntimeSafetyFixups : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var host = new GameObject("RUN_REALMS_2_RUNTIME_FIXUPS");
            DontDestroyOnLoad(host);
            host.AddComponent<RuntimeSafetyFixups>();
        }

        private IEnumerator Start()
        {
            // The bootstrap builds UI during Awake. Two end-of-frame passes cover both the
            // initial hub and any layout rebuild triggered by the first Canvas update.
            yield return new WaitForEndOfFrame();
            ApplyUiRules();
            yield return new WaitForEndOfFrame();
            ApplyUiRules();
            enabled = false;
        }

        private static void ApplyUiRules()
        {
            var graphics = Object.FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var graphic in graphics)
            {
                var selectable = graphic.GetComponent<Selectable>();
                graphic.raycastTarget = selectable != null && selectable.targetGraphic == graphic;
            }

            var inputs = Object.FindObjectsByType<InputField>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var input in inputs)
            {
                input.contentType = InputField.ContentType.Standard;
                input.lineType = InputField.LineType.SingleLine;
            }

            // Ensure a valid event system remains available for buttons and text fields.
            if (EventSystem.current == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }
        }
    }
}
