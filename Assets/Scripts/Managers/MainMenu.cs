using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 主菜单按钮。挂在主菜单场景的一个空物体上，把按钮的 OnClick 拖到对应方法。
/// 四个按钮：Play（新游戏）/ Continue（继续）/ Settings（设置）/ Restart（重新开始）。
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("场景名（要和 Build Settings 里一致）")]
    public string mapSceneName = "Map";        // 地图场景

    // 新游戏：清掉旧存档和进度，从第 1 关开始
    public void OnPlay()
    {
        SaveManager.Instance.Clear();
        GameProgress.Reset();
        SceneManager.LoadScene(mapSceneName);
    }

    // 继续：读档接着打；没存档就当新游戏
    public void OnContinue()
    {
        if (!SaveManager.Instance.Load())
        {
            Debug.Log("[主菜单] 没有存档，开新游戏");
            OnPlay();
            return;
        }
        SceneManager.LoadScene(mapSceneName);
    }

    // 设置：打开/关闭设置面板。面板显隐自己搭，音量用 SettingsMenu 管。
    public void OnSettings()
    {
        // 这里留空：把「设置」按钮 OnClick 直接拖到你的设置面板的 SetActive(true)，
        // 或者拖到你自己写的「显示设置面板」方法上。
    }

    // 重新开始：和 Play 一样（清存档 + 从第 1 关开始）
    public void OnRestart()
    {
        OnPlay();
    }

    // 退出游戏
    public void OnQuit()
    {
        Application.Quit();
    }

    // 有没有存档（给「继续」按钮用：没存档就置灰）
    public bool HasSave()
    {
        return SaveManager.Instance.HasSave();
    }
}
