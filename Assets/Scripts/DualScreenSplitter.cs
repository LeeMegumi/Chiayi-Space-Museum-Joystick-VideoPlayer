using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 直式影片 RenderTexture → 依中線切成上下兩半，分別鋪到兩個橫式螢幕的 RawImage。
/// 自動維持不變形（讓每半剛好等於螢幕長寬比），上下溢出的部分自動裁切。
///
/// 使用：把上/下螢幕的 RawImage、來源 RenderTexture 拖進來即可。
/// 數值會在 Editor 即時更新（OnValidate），執行時也會套用（OnEnable）。
/// </summary>
[ExecuteAlways]
public class DualScreenSplitter : MonoBehaviour
{
    [Header("兩個橫式螢幕的 RawImage")]
    public RawImage topScreen;
    public RawImage bottomScreen;

    [Header("來源")]
    [Tooltip("直式影片的 RenderTexture（可留空，預設 1080x1920）")]
    public RenderTexture sourceTexture;

    [Header("螢幕規格")]
    [Tooltip("單一橫式螢幕長寬比，1920x1080 = 16/9")]
    public float screenAspect = 16f / 9f;

    [Tooltip("單一橫式螢幕的垂直解析度（用於邊框換算，通常 1080）")]
    public float screenPixelHeight = 1080f;

    [Tooltip("上下兩螢幕作用區之間的邊框間隙，換算成螢幕像素。無間隙或不需補償填 0。")]
    public float bezelPixels = 0f;

    void OnEnable()   { Apply(); }
    void OnValidate() { Apply(); }

    public void Apply()
    {
        if (topScreen == null || bottomScreen == null) return;

        float texW = sourceTexture ? sourceTexture.width  : 1080f;
        float texH = sourceTexture ? sourceTexture.height : 1920f;

        // 每半要取樣的 UV 高度 = 材質長寬比 ÷ 螢幕長寬比
        // 例：(1080/1920) / (16/9) = 0.31640625
        float h = (texW / texH) / screenAspect;

        // 邊框補償：把該落在黑邊後面的畫面「藏掉」，讓左右（上下）畫面接得起來
        float bezelUV = bezelPixels * h / screenPixelHeight;

        // UV 原點在左下：上螢幕取中線以上、下螢幕取中線以下
        topScreen.uvRect    = new Rect(0f, 0.5f + bezelUV * 0.5f,         1f, h);
        bottomScreen.uvRect = new Rect(0f, 0.5f - bezelUV * 0.5f - h,     1f, h);
    }
}
