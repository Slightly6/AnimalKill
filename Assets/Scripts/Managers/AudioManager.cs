using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 全局音频：背景音乐（循环）+ 音效（一次性）。
/// 单例，切场景不销毁。只有战斗场景（battleSceneName）才放 BGM。
/// 音频放 Assets/Resources/Audio/ 下，按名字加载，不用手动拖。
/// 用法：AudioManager.Instance.PlayHit() 之类。
/// </summary>
public class AudioManager : Singleton<AudioManager>
{
    [Header("音量（0~1）")]
    public float bgmVolume = 0.5f;
    public float sfxVolume = 1f;

    [Header("只在哪个场景放 BGM")]
    public string battleSceneName = "SampleScene";   // 战斗场景名（和 Build Settings 一致）

    private AudioSource bgmSource;
    private AudioSource sfxSource;

    private AudioClip bgm;
    private AudioClip sfxDraw, sfxFlip, sfxHit, sfxFace, sfxDeath, sfxBell;

    protected override void Awake()
    {
        base.Awake();
        if (AudioManager.Instance != this) return;   // 是重复的副本，别初始化

        DontDestroyOnLoad(gameObject);   // 切场景不销毁，音乐不断

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;

        // 读上次存的音量（设置菜单改过就记住）
        bgmVolume = PlayerPrefs.GetFloat("BGMVolume", bgmVolume);
        sfxVolume = PlayerPrefs.GetFloat("SFXVolume", sfxVolume);

        bgm = Load("Audio/bgm");
        sfxDraw = Load("Audio/sfx_draw");
        sfxFlip = Load("Audio/sfx_flip");
        sfxHit = Load("Audio/sfx_hit");
        sfxFace = Load("Audio/sfx_face");
        sfxDeath = Load("Audio/sfx_death");
        sfxBell = Load("Audio/sfx_bell");

        // 只有战斗场景才放 BGM，切到别的场景就停
        SceneManager.sceneLoaded += OnSceneLoaded;
        UpdateBgmForScene(SceneManager.GetActiveScene().name);
    }

    AudioClip Load(string path)
    {
        AudioClip c = Resources.Load<AudioClip>(path);
        if (c == null) Debug.LogWarning("[音频] 找不到 " + path);
        return c;
    }

    public void PlayBGM()
    {
        if (bgmSource == null || bgm == null) return;
        bgmSource.clip = bgm;
        bgmSource.volume = bgmVolume;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    public void StopBGM()
    {
        if (bgmSource != null) bgmSource.Stop();
    }

    // 场景切换时自动判断：战斗场景放 BGM，别的场景停
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateBgmForScene(scene.name);
    }

    void UpdateBgmForScene(string sceneName)
    {
        if (sceneName == battleSceneName) PlayBGM();
        else StopBGM();
    }

    void PlaySfx(AudioClip clip)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    public void PlayDraw()  { PlaySfx(sfxDraw); }
    public void PlayFlip()  { PlaySfx(sfxFlip); }
    public void PlayHit()   { PlaySfx(sfxHit); }
    public void PlayFace()  { PlaySfx(sfxFace); }
    public void PlayDeath() { PlaySfx(sfxDeath); }
    public void PlayBell()  { PlaySfx(sfxBell); }

    // 设置菜单用（改完顺手存下来，下次进游戏还是这个音量）
    public void SetBgmVolume(float v)
    {
        bgmVolume = v;
        if (bgmSource != null) bgmSource.volume = v;
        PlayerPrefs.SetFloat("BGMVolume", v);
    }

    public void SetSfxVolume(float v)
    {
        sfxVolume = v;
        PlayerPrefs.SetFloat("SFXVolume", v);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;   // 别留挂着的回调
    }
}
