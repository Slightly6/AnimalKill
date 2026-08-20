using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// 场景切换淡入淡出：屏幕慢慢变黑 → 全黑时切场景 → 再慢慢变亮。
/// 挂在一个空物体上（自动建全屏黑图），跨场景保留。
/// 用法：任何地方写 FadeManager.Go("场景名")，代替 SceneManager.LoadScene。
/// </summary>
public class FadeManager : MonoBehaviour
{
    public static FadeManager Instance { get; private set; }

    [Header("变黑/变亮各花多久（秒）")]
    public float fadeDuration = 0.5f;

    private Image fadeImage;

    void Awake()
    {
        // 跨场景只留一个
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildFadeImage();
    }

    // 静态入口：带淡入淡出切场景（场景里没挂 FadeManager 就直接切，不会报错）
    public static void Go(string sceneName)
    {
        if (Instance != null)
        {
            Instance.StartCoroutine(Instance.FadeToSceneRoutine(sceneName));
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    // 自动建一个盖在最上面的全屏黑图（初始全透明）
    void BuildFadeImage()
    {
        GameObject canvasGo = new GameObject("FadeCanvas");
        canvasGo.transform.SetParent(transform);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;   // 盖在最上面

        GameObject imgGo = new GameObject("FadeImage");
        imgGo.transform.SetParent(canvasGo.transform);

        fadeImage = imgGo.AddComponent<Image>();
        fadeImage.color = new Color(0, 0, 0, 0);   // 全透明
        fadeImage.raycastTarget = false;           // 不挡鼠标

        RectTransform rt = fadeImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // 变黑 → 切场景 → 变亮
    IEnumerator FadeToSceneRoutine(string sceneName)
    {
        yield return StartCoroutine(Fade(0f, 1f));   // 变黑
        SceneManager.LoadScene(sceneName);           // 全黑时切场景
        yield return StartCoroutine(Fade(1f, 0f));   // 变亮
    }

    // alpha 从 from 渐变到 to
    IEnumerator Fade(float from, float to)
    {
        float t = 0;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(from, to, t / fadeDuration);
            fadeImage.color = new Color(0, 0, 0, a);
            yield return null;
        }
        fadeImage.color = new Color(0, 0, 0, to);
    }
}
