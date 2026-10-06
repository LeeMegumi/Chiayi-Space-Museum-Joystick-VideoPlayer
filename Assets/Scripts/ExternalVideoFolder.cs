using System;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// 外部影片資料夾：讓 Build 後的執行檔可以直接換影片，不必重新 Build。
///
/// 資料夾位置：
///   Build 後  → 執行檔（.exe）旁邊的「Videos」資料夾
///   Editor 中 → 專案根目錄（Assets 的上一層）的「Videos」資料夾
///
/// 規則：播放資料夾內「檔名排序第一個」的影片；資料夾是空的就播放內建影片。
/// </summary>
public static class ExternalVideoFolder
{
    public const string DefaultFolderName = "Videos";

    private static readonly string[] Extensions = { ".mp4", ".m4v", ".mov", ".webm", ".wmv", ".avi" };

    /// <summary>取得資料夾完整路徑（不保證存在）。</summary>
    public static string GetFolderPath(string folderName = DefaultFolderName)
    {
        // Application.dataPath：Build 後為 xxx_Data，Editor 中為 Assets，上一層就是我們要的位置
        string root = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(root, string.IsNullOrWhiteSpace(folderName) ? DefaultFolderName : folderName.Trim());
    }

    /// <summary>確保資料夾存在，並回傳要播放的影片完整路徑；沒有影片時回傳 null。</summary>
    public static string FindVideo(string folderName = DefaultFolderName)
    {
        try
        {
            string folder = GetFolderPath(folderName);
            EnsureFolder(folder);

            return Directory.GetFiles(folder)
                .Where(f => Extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ExternalVideo] 讀取影片資料夾失敗，改用內建影片：" + e.Message);
            return null;
        }
    }

    /// <summary>建立資料夾與說明檔（已存在則不動）。</summary>
    public static void EnsureFolder(string folder)
    {
        Directory.CreateDirectory(folder);

        string readme = Path.Combine(folder, "說明.txt");
        if (File.Exists(readme)) return;

        File.WriteAllText(readme,
            "【影片資料夾使用說明】\r\n" +
            "\r\n" +
            "1. 把要播放的影片放進這個資料夾，重新開啟程式就會自動讀取。\r\n" +
            "2. 資料夾內有多支影片時，只會播放「檔名排序第一個」的影片。\r\n" +
            "   建議資料夾內只留一支，或在檔名前面加上 01_ 之類的編號。\r\n" +
            "3. 資料夾內沒有影片時，會播放程式內建的影片。\r\n" +
            "4. 建議格式：MP4（H.264 + AAC）。支援副檔名：" + string.Join(" ", Extensions) + "\r\n" +
            "5. 換影片的步驟：關閉程式 → 替換影片檔 → 重新開啟程式。\r\n" +
            "6. 注意：段落節點（搖桿跳轉的秒數）是在程式內設定的，\r\n" +
            "   若新影片的段落時間點不同，需要回 Unity 調整節點秒數。\r\n");
    }
}
