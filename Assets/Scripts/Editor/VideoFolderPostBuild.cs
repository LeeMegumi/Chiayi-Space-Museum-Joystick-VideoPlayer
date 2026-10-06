using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;

/// <summary>
/// Build 完成後，自動在執行檔旁邊建立「Videos」資料夾與說明檔，
/// 之後把影片丟進去即可替換播放內容。
/// </summary>
public static class VideoFolderPostBuild
{
    [PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        string buildDir = Path.GetDirectoryName(pathToBuiltProject);
        if (string.IsNullOrEmpty(buildDir)) return;

        string folder = Path.Combine(buildDir, ExternalVideoFolder.DefaultFolderName);
        ExternalVideoFolder.EnsureFolder(folder);
        UnityEngine.Debug.Log("[ExternalVideo] 已建立影片資料夾：" + folder);
    }
}
