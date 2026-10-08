using UnityEngine;

/// <summary>
/// 啟用額外接上的螢幕（Display 2 之後）。
/// 只啟用「實際有接上」的螢幕，所以只接一台螢幕時也不會出錯。
/// </summary>
public class DisplayActitive : MonoBehaviour
{
    void Start()
    {
        // Display 1（索引 0）永遠是啟用的，不需要呼叫 Activate
        int count = Mathf.Min(Display.displays.Length, 4);
        for (int i = 1; i < count; i++)
            Display.displays[i].Activate();
    }
}
