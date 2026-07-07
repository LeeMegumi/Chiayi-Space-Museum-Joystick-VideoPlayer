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
        videoPlayer = GetComponent<VideoPlayer>();
        videoPlayer.isLooping = true;    // 整支影片持續 Loop
        videoPlayer.playOnAwake = false; // 由本腳本控制播放時機
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

        videoPlayer.time = nodes[index].time;   // seek 到節點時間
        if (!videoPlayer.isPlaying)
            videoPlayer.Play();

        onNodeChanged?.Invoke(index);
    }
}
