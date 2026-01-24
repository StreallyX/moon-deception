using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

public class StressUISetup : MonoBehaviour
{
    [MenuItem("Moon Deception/Setup Stress UI")]
    public static void SetupStressUI()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        GameObject stressBarBG = new GameObject("StressBarBackground");
        stressBarBG.transform.SetParent(canvas.transform, false);
        RectTransform bgRect = stressBarBG.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.5f, 0f);
        bgRect.anchorMax = new Vector2(0.5f, 0f);
        bgRect.pivot = new Vector2(0.5f, 0f);
        bgRect.anchoredPosition = new Vector2(0, 30);
        bgRect.sizeDelta = new Vector2(400, 30);
        
        Image bgImage = stressBarBG.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        GameObject stressBarObj = new GameObject("StressBar");
        stressBarObj.transform.SetParent(stressBarBG.transform, false);
        RectTransform sliderRect = stressBarObj.AddComponent<RectTransform>();
        sliderRect.anchorMin = Vector2.zero;
        sliderRect.anchorMax = Vector2.one;
        sliderRect.offsetMin = new Vector2(5, 5);
        sliderRect.offsetMax = new Vector2(-5, -5);

        Slider slider = stressBarObj.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;

        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(stressBarObj.transform, false);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = Vector2.zero;

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = Color.green;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;

        slider.fillRect = fillRect;
        slider.targetGraphic = fillImage;

        GameObject label = new GameObject("StressLabel");
        label.transform.SetParent(stressBarBG.transform, false);
        RectTransform labelRect = label.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.anchoredPosition = new Vector2(0, 5);
        labelRect.sizeDelta = new Vector2(0, 20);

        Text labelText = label.AddComponent<Text>();
        labelText.text = "STRESS";
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontSize = 14;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = Color.white;

        StressSystem stressSystem = FindFirstObjectByType<StressSystem>();
        if (stressSystem != null)
        {
            var so = new SerializedObject(stressSystem);
            so.FindProperty("stressSlider").objectReferenceValue = slider;
            so.FindProperty("stressBarFill").objectReferenceValue = fillImage;
            so.ApplyModifiedProperties();
        }

        Debug.Log("[StressUISetup] Stress UI created successfully!");
        Selection.activeGameObject = stressBarBG;
    }
}
