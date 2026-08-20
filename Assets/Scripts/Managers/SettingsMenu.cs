using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 设置面板：两个滑条（背景音乐音量 / 音效音量），挂设置面板上。
/// 把两个 Slider 拖进来，运行后自动绑好（也可以自己在 Inspector 里绑 OnValueChanged）。
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    public Slider bgmSlider;   // 背景音乐音量滑条（0~1）
    public Slider sfxSlider;   // 音效音量滑条（0~1）

    void Start()
    {
        // 滑条初始值 = 当前音量
        if (bgmSlider != null) bgmSlider.value = AudioManager.Instance.bgmVolume;
        if (sfxSlider != null) sfxSlider.value = AudioManager.Instance.sfxVolume;

        // 拖滑条时改音量
        if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(OnBgmChanged);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSfxChanged);
    }

    public void OnBgmChanged(float v) { AudioManager.Instance.SetBgmVolume(v); }
    public void OnSfxChanged(float v) { AudioManager.Instance.SetSfxVolume(v); }
}
