using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 暂停菜单：按 ESC 暂停/继续，用 OnGUI 直接画按钮（不用搭 UI，挂哪都行）。
/// 设了 DontDestroyOnLoad，只在一个场景挂一次就全局生效。
/// 按钮：继续 / 重新开始 / 回主菜单。
/// </summary>
public class PauseMenu : Singleton<PauseMenu>
{
    [Header("场景名（要和 Build Settings 里一致）")]
    public string mapSceneName = "Map";        // 地图场景
    public string menuSceneName = "MainMenu";  // 主菜单场景

    bool paused = false;

    protected override void Awake()
    {
        base.Awake();
        if (PauseMenu.Instance != this) return;   // 重复副本，别留
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        // 主菜单场景里不响应 ESC（那有自己的按钮）
        if (SceneManager.GetActiveScene().name == menuSceneName) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            paused = !paused;
            Time.timeScale = paused ? 0f : 1f;   // 暂停 / 继续
        }
    }

    void OnGUI()
    {
        if (!paused) return;

        // 半透明黑底盖住全屏
        GUI.color = new Color(0, 0, 0, 0.7f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float w = 200f;
        float h = 50f;
        float x = (Screen.width - w) / 2f;
        float y = Screen.height / 2f - 100f;

        if (GUI.Button(new Rect(x, y, w, h), "继续"))
        {
            paused = false;
            Time.timeScale = 1f;
        }

        if (GUI.Button(new Rect(x, y + h + 15, w, h), "重新开始"))
        {
            OnRestart();
        }

        if (GUI.Button(new Rect(x, y + 2 * (h + 15), w, h), "回主菜单"))
        {
            OnMainMenu();
        }
    }

    // 重新开始：清存档 + 清进度 + 从第 1 关开始
    void OnRestart()
    {
        Time.timeScale = 1f;   // 先恢复，不然切场景后还是暂停
        paused = false;
        SaveManager.Instance.Clear();
        GameProgress.Reset();
        SceneManager.LoadScene(mapSceneName);
    }

    // 回主菜单
    void OnMainMenu()
    {
        Time.timeScale = 1f;
        paused = false;
        SceneManager.LoadScene(menuSceneName);
    }
}
