using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.Events;

/// <summary>
/// 影片節點跳轉控制器。
/// 整支影片持續 Loop，節點只是預先設定的「秒數跳點」。
/// 按 → 跳下一個節點、按 ← 回上一個節點，頭尾相接循環。
///
/// 使用方式：
///   1. 在場景放一個 GameObject，加上 Video Player 元件並指定 Video Clip / URL。
///   2. 把本腳本掛到「同一個」GameObject 上（會自動確保有 VideoPlayer）。
///   3. 在 Inspector 的 Nodes 清單逐一新增節點，填 Label 與 Time（秒）。
///   4. 執行後用 → / ← 切換節點。
/// </summary>
[RequireComponent(typeof(VideoPlayer))]
public class VideoNodeNavigator : MonoBehaviour
{
    [System.Serializable]
    public class VideoNode
    {
        [Tooltip("辨識用名稱，例如「發射」「進入軌道」")]
        public string label = "Node";

        [Tooltip("跳轉的時間點（秒）")]
        public double time = 0.0;
    }

    // 讓 UnityEvent<int> 能顯示在 Inspector（傳出目前節點索引）
    [System.Serializable]
    public class NodeChangedEvent : UnityEvent<int> { }

    [Header("節點設定（依秒數）")]
    [Tooltip("逐一新增節點，播放時會自動依 Time 由小到大排序，Inspector 順序不影響邏輯。")]
    public List<VideoNode> nodes = new List<VideoNode>();

    [Header("外部影片資料夾")]
    [Tooltip("勾選後，會優先播放執行檔旁邊資料夾內的影片（檔名排序第一個）；資料夾沒有影片時播放 Video Player 上原本指定的影片。")]
    public bool useExternalVideoFolder = true;
    [Tooltip("資料夾名稱。Build 後位於 .exe 旁邊；Editor 中位於專案根目錄（Assets 的上一層）。")]
    public string externalFolderName = ExternalVideoFolder.DefaultFolderName;

    [Header("畫面比例")]
    [Tooltip("影片比例與輸出畫面（RenderTexture）不同時的處理方式。\nFit Inside = 等比例完整顯示，多出來的地方留黑邊（建議）。\nFit Outside = 等比例填滿，超出的部分裁掉。\nStretch = 拉伸填滿（會變形）。")]
    public VideoAspectRatio aspectRatio = VideoAspectRatio.FitInside;

    [Header("輸入按鍵")]
    public KeyCode nextKey = KeyCode.RightArrow;
    public KeyCode previousKey = KeyCode.LeftArrow;

    [Header("行為")]
    [Tooltip("影片準備完成後，自動跳到第一個節點。")]
    public bool jumpToFirstNodeOnStart = true;

    [Header("事件（可選，可在 Inspector 綁定 UI）")]
    [Tooltip("每次切換節點時觸發，參數為目前節點索引。")]
    public NodeChangedEvent onNodeChanged;

    /// <summary>目前所在節點的索引；尚未跳轉時為 -1。</summary>
    public int CurrentIndex { get; private set; } = -1;

    private VideoPlayer videoPlayer;

    void Awake()
    {
        Application.runInBackground = true; // 視窗失去焦點時也繼續播放與接收搖桿訊號
        videoPlayer = GetComponent<VideoPlayer>();
        videoPlayer.isLooping = true;    // 整支影片持續 Loop
        videoPlayer.playOnAwake = false; // 由本腳本控制播放時機

        // 影片比例與輸出畫面不同時，等比例縮放置入，其餘留黑邊
        videoPlayer.aspectRatio = aspectRatio;
        ClearTargetTexture();

        if (useExternalVideoFolder)
        {
            string path = ExternalVideoFolder.FindVideo(externalFolderName);
            if (path != null)
            {
                videoPlayer.source = VideoSource.Url;
                videoPlayer.url = path;
                Debug.Log("[ExternalVideo] 播放外部影片：" + path);
            }
            else
            {
                Debug.Log("[ExternalVideo] 資料夾內沒有影片，播放內建影片：" + ExternalVideoFolder.GetFolderPath(externalFolderName));
            }
        }
    }

    void Start()
    {
        SortNodes();
        videoPlayer.prepareCompleted += OnPrepared;
        videoPlayer.Prepare();
    }

    void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.prepareCompleted -= OnPrepared;
    }

    // 把輸出用的 RenderTexture 清成黑色，確保影片沒蓋到的區域是黑邊而不是殘留畫面
    private void ClearTargetTexture()
    {
        RenderTexture rt = videoPlayer.targetTexture;
        if (rt == null) return;

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = previous;
    }

    private void OnPrepared(VideoPlayer vp)
    {
        vp.prepareCompleted -= OnPrepared;
        vp.Play();

        if (jumpToFirstNodeOnStart && nodes.Count > 0)
            GoToNode(0);
    }

    void Update()
    {
        if (!videoPlayer.isPrepared || nodes.Count == 0) return;

        if (Input.GetKeyDown(nextKey))
            NextNode();
        else if (Input.GetKeyDown(previousKey))
            PreviousNode();
    }

    // 依秒數由小到大排序，讓 Inspector 的填寫順序不影響切換邏輯
    private void SortNodes()
    {
        nodes.Sort((a, b) => a.time.CompareTo(b.time));
    }

    // ────────────────────────────────────────────────
    //  公開方法：也可從 UI Button、或其他腳本（如硬體按鈕）呼叫
    // ────────────────────────────────────────────────

    public void NextNode()
    {
        if (nodes.Count == 0) return;
        GoToNode((CurrentIndex + 1) % nodes.Count);                 // 頭尾相接
    }

    public void PreviousNode()
    {
        if (nodes.Count == 0) return;
        GoToNode((CurrentIndex - 1 + nodes.Count) % nodes.Count);   // 頭尾相接
    }

    /// <summary>跳到指定索引的節點。</summary>
    public void GoToNode(int index)
    {
        if (nodes.Count == 0) return;

        index = Mathf.Clamp(index, 0, nodes.Count - 1);
        CurrentIndex = index;

        // 外部影片可能比原本短：節點秒數超過影片長度時改跳回開頭，避免 seek 到影片外
        double t = nodes[index].time;
        if (videoPlayer.length > 0 && t >= videoPlayer.length) t = 0;
        videoPlayer.time = t;                   // seek 到節點時間
        if (!videoPlayer.isPlaying)
            videoPlayer.Play();

        onNodeChanged?.Invoke(index);
    }
}
