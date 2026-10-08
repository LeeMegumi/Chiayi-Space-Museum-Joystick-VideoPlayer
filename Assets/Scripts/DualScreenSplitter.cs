using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 直式影片 RenderTexture 的輸出版面控制，兩種模式可在 Inspector 切換：
///
///   單螢幕直式（目前使用）：
///     整支直式影片完整鋪在「上螢幕」的 RawImage（Display 1）上，等比例縮放、置中。
///     若 Windows 的顯示方向仍是橫向（螢幕只是實體轉 90 度），會自動把畫面轉 90 度。
///
///   雙螢幕上下：
///     依中線切成上下兩半，分別鋪到兩個橫式螢幕的 RawImage。
///     自動維持不變形（讓每半剛好等於螢幕長寬比），上下溢出的部分自動裁切。
///
/// 使用：把上/下螢幕的 RawImage、來源 RenderTexture 拖進來即可。
/// </summary>
[ExecuteAlways]
public class DualScreenSplitter : MonoBehaviour
{
    public enum LayoutMode
    {
        [InspectorName("單螢幕直式")] SinglePortrait,
        [InspectorName("雙螢幕上下")] DualLandscape,
    }

    public enum RotationMode
    {
        [InspectorName("自動（橫向畫面才轉）")] Auto,
        [InspectorName("不旋轉")] None,
        [InspectorName("順時針 90 度")] Clockwise90,
        [InspectorName("逆時針 90 度")] CounterClockwise90,
    }

    [Header("輸出模式")]
    public LayoutMode mode = LayoutMode.SinglePortrait;

    [Tooltip("只在「單螢幕直式」使用。Windows 已設成直向顯示時不需要旋轉；" +
             "若 Windows 仍是橫向、螢幕只是實體轉 90 度，就需要由程式旋轉畫面。" +
             "自動 = 偵測到橫向畫面時順時針轉 90 度；方向相反請改選「逆時針 90 度」。")]
    public RotationMode rotation = RotationMode.Auto;

    [Header("螢幕的 RawImage（單螢幕模式只使用上螢幕）")]
    public RawImage topScreen;
    public RawImage bottomScreen;

    [Header("來源")]
    [Tooltip("直式影片的 RenderTexture（可留空，預設 1080x1920）")]
    public RenderTexture sourceTexture;

    [Header("雙螢幕模式的螢幕規格")]
    [Tooltip("單一橫式螢幕長寬比，1920x1080 = 16/9")]
    public float screenAspect = 16f / 9f;

    [Tooltip("單一橫式螢幕的垂直解析度（用於邊框換算，通常 1080）")]
    public float screenPixelHeight = 1080f;

    [Tooltip("上下兩螢幕作用區之間的邊框間隙，換算成螢幕像素。無間隙或不需補償填 0。")]
    public float bezelPixels = 0f;

    private bool dirty = true;
    private Vector2 lastCanvasSize;

    void OnEnable()   { dirty = true; }
    void OnValidate() { dirty = true; }   // 不能在 OnValidate 直接改 RectTransform，留到 Update 再套用

    void Update()
    {
        // 解析度 / 視窗大小改變時也要重新排版
        Vector2 size = CanvasSize();
        if (!dirty && size == lastCanvasSize) return;

        dirty = false;
        lastCanvasSize = size;
        Apply();
    }

    private Vector2 CanvasSize()
    {
        if (topScreen == null || topScreen.canvas == null) return Vector2.zero;
        return ((RectTransform)topScreen.canvas.transform).rect.size;
    }

    public void Apply()
    {
        if (topScreen == null) return;

        float texW = sourceTexture ? sourceTexture.width  : 1080f;
        float texH = sourceTexture ? sourceTexture.height : 1920f;

        if (mode == LayoutMode.SinglePortrait) ApplySingle(texW, texH);
        else ApplyDual(texW, texH);
    }

    // ── 單螢幕直式：整支影片等比例完整顯示 ──
    private void ApplySingle(float texW, float texH)
    {
        Vector2 canvas = CanvasSize();
        if (canvas.x <= 0f || canvas.y <= 0f) return;   // Canvas 還沒排版好，下一幀再試

        bool rotate = rotation == RotationMode.Auto ? canvas.x > canvas.y
                                                    : rotation != RotationMode.None;
        float angle = !rotate ? 0f : (rotation == RotationMode.CounterClockwise90 ? 90f : -90f);

        // 旋轉 90 度後，影片的「寬」對應螢幕的「高」
        float availW = rotate ? canvas.y : canvas.x;
        float availH = rotate ? canvas.x : canvas.y;
        float scale = Mathf.Min(availW / texW, availH / texH);

        RectTransform rt = topScreen.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(texW * scale, texH * scale);
        rt.localEulerAngles = new Vector3(0f, 0f, angle);

        topScreen.uvRect = new Rect(0f, 0f, 1f, 1f);
    }

    // ── 雙螢幕上下：依中線切兩半 ──
    private void ApplyDual(float texW, float texH)
    {
        if (bottomScreen == null) return;

        // 還原單螢幕模式改過的大小與旋轉
        RectTransform rt = topScreen.rectTransform;
        rt.sizeDelta = new Vector2(screenPixelHeight * screenAspect, screenPixelHeight);
        rt.localEulerAngles = Vector3.zero;

        // 每半要取樣的 UV 高度 = 材質長寬比 ÷ 螢幕長寬比
        // 例：(1080/1920) / (16/9) = 0.31640625
        float h = (texW / texH) / screenAspect;

        // 邊框補償：把該落在黑邊後面的畫面「藏掉」，讓上下畫面接得起來
        float bezelUV = bezelPixels * h / screenPixelHeight;

        // UV 原點在左下：上螢幕取中線以上、下螢幕取中線以下
        topScreen.uvRect    = new Rect(0f, 0.5f + bezelUV * 0.5f,         1f, h);
        bottomScreen.uvRect = new Rect(0f, 0.5f - bezelUV * 0.5f - h,     1f, h);
    }
}
