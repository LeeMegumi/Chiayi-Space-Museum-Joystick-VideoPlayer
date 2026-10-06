using System;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Threading;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// 透過序列埠接收 Arduino 搖桿訊號，控制 VideoNodeNavigator 切換段落。
/// 搭配 Arduino/JoystickSender/JoystickSender.ino 使用。
///
///   'R' → 下一個段落、'L' → 上一個段落、'H' → 心跳（用來自動找埠與偵測斷線）
///
/// 使用方式：
///   1. 把本腳本掛到有 VideoNodeNavigator 的同一個 GameObject 上。
///   2. Port Name 留空 = 自動尋找 Arduino；也可以直接填 "COM3" 之類的固定埠。
///   3. 鍵盤 ← / → 仍然可以使用（由 VideoNodeNavigator 處理）。
///
/// 注意：需要 Player Settings > Api Compatibility Level 設為 .NET Framework
///       才有 System.IO.Ports。
/// </summary>
[RequireComponent(typeof(VideoNodeNavigator))]
public class JoystickSerialInput : MonoBehaviour
{
    [Header("序列埠")]
    [Tooltip("留空 = 自動掃描所有 COM 埠，找到有送出心跳的 Arduino。也可填固定埠名，例如 COM3。")]
    public string portName = "";
    [Tooltip("必須與 Arduino 程式的 Serial.begin() 相同。")]
    public int baudRate = 9600;

    [Header("行為")]
    [Tooltip("搖桿安裝方向相反時勾選，左右對調。")]
    public bool swapLeftRight = false;
    [Tooltip("兩次跳轉之間的最短間隔（秒），避免觀眾狂撥時影片來不及 seek。")]
    public float minInterval = 0.3f;
    [Tooltip("在 Console 顯示連線狀態與收到的訊號。")]
    public bool logMessages = true;

    /// <summary>目前是否已連上 Arduino。</summary>
    public bool IsConnected => connected;

    private const int HeartbeatTimeoutMs = 3000; // 超過這個時間沒收到任何資料就視為斷線
    private const int DetectTimeoutMs = 4000;    // 自動掃描時，每個埠等待心跳的時間（Uno 開埠會重開機約 2 秒）
    private const int RetryDelayMs = 1000;

    private VideoNodeNavigator navigator;
    private VideoPlayer videoPlayer;
    private Thread thread;
    private volatile bool running;
    private volatile bool connected;
    private readonly ConcurrentQueue<char> commands = new ConcurrentQueue<char>();
    private readonly ConcurrentQueue<string> logs = new ConcurrentQueue<string>();
    private float lastJumpTime = -999f;

    void Awake()
    {
        navigator = GetComponent<VideoNodeNavigator>();
        videoPlayer = GetComponent<VideoPlayer>();
    }

    void OnEnable()
    {
        running = true;
        thread = new Thread(SerialLoop) { IsBackground = true, Name = "JoystickSerial" };
        thread.Start();
    }

    void OnDisable()
    {
        running = false;
        if (thread != null)
        {
            thread.Join(2000);
            thread = null;
        }
        connected = false;
    }

    void Update()
    {
        while (logs.TryDequeue(out string msg))
            if (logMessages) Debug.Log("[Joystick] " + msg);

        while (commands.TryDequeue(out char c))
        {
            if (videoPlayer != null && !videoPlayer.isPrepared) continue;
            if (Time.unscaledTime - lastJumpTime < minInterval) continue;

            bool next = (c == 'R') != swapLeftRight;
            lastJumpTime = Time.unscaledTime;
            if (logMessages) Debug.Log("[Joystick] " + (next ? "→ 下一個段落" : "← 上一個段落"));

            if (next) navigator.NextNode();
            else navigator.PreviousNode();
        }
    }

    // ────────────────────────────────────────────────
    //  以下在背景執行緒執行，不可呼叫 Unity API
    // ────────────────────────────────────────────────

    private void SerialLoop()
    {
        while (running)
        {
            SerialPort port = null;
            try
            {
                port = string.IsNullOrWhiteSpace(portName) ? FindArduino() : Open(portName.Trim());
                if (port == null)
                {
                    Sleep(RetryDelayMs);
                    continue;
                }

                connected = true;
                logs.Enqueue("已連線：" + port.PortName);
                ReadUntilLost(port, false);
                logs.Enqueue("連線中斷，重新尋找中…（" + port.PortName + "）");
            }
            catch (Exception e)
            {
                logs.Enqueue("序列埠錯誤：" + e.Message);
            }
            finally
            {
                connected = false;
                Close(port);
            }
            Sleep(RetryDelayMs);
        }
    }

    /// <summary>逐一開啟所有序列埠，回傳第一個有送出心跳 'H' 的埠。</summary>
    private SerialPort FindArduino()
    {
        foreach (string name in SerialPort.GetPortNames())
        {
            if (!running) return null;
            SerialPort port = null;
            try
            {
                port = Open(name);
                if (ReadUntilLost(port, true)) return port;
            }
            catch (Exception) { /* 這個埠打不開或被占用，換下一個 */ }
            Close(port);
        }
        return null;
    }

    private SerialPort Open(string name)
    {
        var port = new SerialPort(name, baudRate)
        {
            ReadTimeout = 200,
            DtrEnable = true,
        };
        port.Open();
        return port;
    }

    /// <summary>
    /// 持續讀取。detectOnly = true 時：收到第一個心跳就回傳 true，逾時回傳 false。
    /// detectOnly = false 時：一直讀到斷線（心跳逾時或例外）才回傳。
    /// </summary>
    private bool ReadUntilLost(SerialPort port, bool detectOnly)
    {
        int timeoutMs = detectOnly ? DetectTimeoutMs : HeartbeatTimeoutMs;
        DateTime lastData = DateTime.UtcNow;

        while (running)
        {
            int b;
            try
            {
                b = port.ReadByte();
            }
            catch (TimeoutException)
            {
                if ((DateTime.UtcNow - lastData).TotalMilliseconds > timeoutMs) return false;
                continue;
            }
            if (b < 0) return false;

            char c = (char)b;
            if (c == 'H')
            {
                lastData = DateTime.UtcNow;
                if (detectOnly) return true;
            }
            else if (c == 'L' || c == 'R')
            {
                lastData = DateTime.UtcNow;
                if (!detectOnly) commands.Enqueue(c);
            }
            else if (detectOnly && (DateTime.UtcNow - lastData).TotalMilliseconds > timeoutMs)
            {
                return false; // 一直送別的資料的裝置，不是我們的 Arduino
            }
        }
        return false;
    }

    private static void Close(SerialPort port)
    {
        if (port == null) return;
        try { if (port.IsOpen) port.Close(); } catch (Exception) { }
        try { port.Dispose(); } catch (Exception) { }
    }

    private void Sleep(int ms)
    {
        for (int waited = 0; running && waited < ms; waited += 100)
            Thread.Sleep(100);
    }
}
