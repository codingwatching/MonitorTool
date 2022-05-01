using MonitorLib.GOT;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Profiling;
using UnityEngine.UI;

public class GOTProfiler : MonoBehaviour
{
    [Header("�Ƿ�����־")]
    public bool EnableLog = false;
    [Header("�Ƿ�ɼ�֡ͼ")]
    public bool EnableFrameTexture = false;
    [Header("�Ƿ�ɼ���������,ͳ��֮ǰ��Ҫ�ȵ���˵���Hook/���к������ܷ���")]
    public bool EnableFunctionAnalysis = false;
    [Header("�Ƿ�ɼ�CPU�¶�")]
    public bool EnableCPUInfo = true;
    public int CPUInfoFrame = 5;
    [Header("�Ƿ�ɼ���ع���")]
    public bool EnableBatteryInfo = true;
    public int BatteryInfoFrame = 5;
    [Header("����ǰ���֡��")]
    public int IgnoreFrameCount = 5;
    [Header("�Ƿ�ʹ�ö������ļ�(�����ʹ��txt)")]
    public bool UseBinary = false;

    public Text UploadTips;
    public Text ReportUrl;
    int m_FPS = 0;
    int m_TickTime = 0;
    string m_StartTime = "";
    float m_Accumulator = 0;
    int m_Frames = 0;
    float m_TimeLeft;
    float m_UpdateInterval = 0.5f;
    bool btnMonitor = false;
    string btnMsg = "��ʼ���";
    int m_frameIndex = 0;
    Action<bool> MonitorCallback;
    MonitorInfos monitorInfos = null;
    //�������ܷ���csv
    string funcAnalysisFilePath;
    //log��־·��
    string logFilePath;
    //�豸��Ϣ·��
    string deviceFilePath;
    //������Ϣ·��
    string testFilePath;
    //���ܼ��
    string monitorFilePath;
    //�ļ���׺����
    string fileExt;

    //string data = @"C:\Users\d00605132\AppData\LocalLow\Aladdin\MonitorToolRef\monitor_2022_4_29_23_51_49.data";

    void Awake()
    {
        Application.targetFrameRate = 60;
        //IBinarySerializable testInfo = new MonitorInfos();
        //var res = FileManager.ReadBinaryDataFromFile(data, ref testInfo);
        //if (res)
        //{
        //    Debug.LogError("�����ɹ�");
        //    Debug.Log(testInfo.ToString());
        //}
    }

    void Start()
    {
        GameObject.DontDestroyOnLoad(gameObject);
        MonitorCallback += (res) =>
        {
            if (res)
            {
                fileExt = UseBinary ? ConstString.BinaryExt : ConstString.TextExt;
                Debug.Log(ConstString.Monitoring);
                m_frameIndex = 0;
                ShareDatas.StartTime = DateTime.Now; //��ǰʱ��
                m_StartTime = ShareDatas.StartTime.ToString().Replace(" ", "_").Replace("/", "_").Replace(":", "_");
                ShareDatas.StartTimeStr = m_StartTime;
#if UNITY_EDITOR
                PlayerPrefs.SetString("TestTime", m_StartTime);
                PlayerPrefs.Save();
#endif
                if (EnableFrameTexture)
                {
                    FileManager.CreateDir($"{Application.persistentDataPath}/{m_StartTime}/");
                }
                if (EnableFunctionAnalysis)
                    funcAnalysisFilePath = $"{Application.persistentDataPath}/{ConstString.FuncAnalysisPrefix}{m_StartTime}.csv";
                if (EnableLog)
                    logFilePath = $"{Application.persistentDataPath}/{ConstString.LogPrefix}{m_StartTime}{fileExt}";
                deviceFilePath = $"{Application.persistentDataPath}/{ConstString.DevicePrefix}{m_StartTime}{fileExt}";
                testFilePath = $"{Application.persistentDataPath}/{ConstString.TestPrefix}{m_StartTime}{fileExt}";
                monitorFilePath = $"{Application.persistentDataPath}/{ConstString.MonitorPrefix}{m_StartTime}{fileExt}";
                if (EnableLog)
                {
                    LogManager.CreateLogFile(logFilePath, System.IO.FileMode.Append);
                    Application.logMessageReceived += LogManager.LogToFile;
                }

                m_TickTime = 0;
                InvokeRepeating("Tick", 1.0f, 1.0f);
                //д���豸��Ϣ
                GetSystemInfo();

                if (ReportUrl != null)
                {
                    ReportUrl.gameObject.SetActive(false);
                }

                //����Log��ɫ
                Debug.LogError("����Error Log");
                Debug.LogWarning("����Warning Log");

                StartMonitor();
            }
            else
            {
                Debug.Log(ConstString.MonitorStop);
                ShareDatas.EndTime = DateTime.Now;
                //�ϴ�����ʱ��
                UploadTestInfo();

                CancelInvoke("Tick");
                m_TickTime = 0;

                MonitorInfosReport();
                FuncAnalysisReport();

                if (EnableLog)
                {
                    Application.logMessageReceived -= LogManager.LogToFile;
                    LogManager.CloseLogFile();
                }

                //#if UNITY_EDITOR
                //                if (EnableFunctionAnalysis)
                //                {
                //                    HookUtil.PrintMethodDatas();
                //                }
                //#endif
                if (EnableLog)
                {
                    //FileManager.ReplaceContent(logFilePath, "[Log]", "<font color=\"#0000FF\">[Log]</font>");
                    //FileManager.ReplaceContent(logFilePath, "[Error]", "<font color=\"#FF0000\">[Error]</font>");
                    //FileManager.ReplaceContent(logFilePath, "[Warning]", "<font color=\"#FFD700\">[Warning]</font>");
                    UploadFile(logFilePath);
                }
                Debug.Log("�ļ��ϴ����");

                HttpGet(string.Format(Config.ReportRecordUpdateRequestUrl, Application.identifier, m_StartTime), (res) =>
                 {
                     if (res)
                     {
                         if (ReportUrl != null)
                         {
                             ReportUrl.gameObject.SetActive(true);
                             var url = string.Format(ShareDatas.ReportUrl, m_StartTime);
                            //ReportUrl.text = $"<a href={url}>[{url}]</a>"; //TODO:�޸ĳɶ�̬��ҳ������
                            ReportUrl.text = $"<a href={Config.ReportUrl}>[{Config.ReportUrl}]</a>";
                         }
                     }
                 });
            }
        };
    }

    public void HttpGet(string url, Action<bool> callback)
    {
        UnityWebRequest unityWebRequest = UnityWebRequest.Get(url);
        StartCoroutine(GetUrl(unityWebRequest, callback));
    }

    private IEnumerator GetUrl(UnityWebRequest unityWebRequest, Action<bool> callback)
    {
        yield return unityWebRequest.SendWebRequest();
        if (unityWebRequest.result == UnityWebRequest.Result.Success)
        {
            var res = unityWebRequest.downloadHandler.text;
            if (res.Equals("success"))
            {
                callback.Invoke(true);
                Debug.Log("http get����浵�ɹ�");
            }
            else
            {
                Debug.LogError("http get����浵ʧ��");
                callback.Invoke(false);
            }
        }
        else
        {
            Debug.LogError(unityWebRequest.error);
        }
    }

    void UploadTestInfo()
    {
        TestInfo testInfo = new TestInfo()
        {
            ProductName = Application.productName,
            PackageName = Application.identifier,
            Platform = Application.platform.ToString(),
            Version = Application.version,
            TestTime = ShareDatas.GetTestTime()
        };
        //FileManager.WriteToFile(testFilePath, $"Ӧ������{Application.productName}&nbsp&nbsp&nbsp������{Application.identifier}&nbsp&nbsp&nbsp����ϵͳ��{Application.platform}&nbsp&nbsp&nbsp�汾�ţ�{Application.version}&nbsp&nbsp&nbsp���β���ʱ��:{testTime}");
        bool writeRes = false;
        if (!UseBinary)
        {
            writeRes = FileManager.WriteToFile(testFilePath, JsonUtility.ToJson(testInfo));
        }
        else
        {
            writeRes = FileManager.WriteBinaryDataToFile(testFilePath, testInfo);
        }
        if (writeRes)
            UploadFile(testFilePath);
    }

    void StartMonitor()
    {
        monitorInfos = new MonitorInfos();
    }

    void FuncAnalysisReport()
    {
        if (EnableFunctionAnalysis)
        {
            HookUtil.MethodAnalysisReport(m_StartTime);
            if (File.Exists(funcAnalysisFilePath))
            {
                UploadFile(funcAnalysisFilePath);
            }
            else
            {
                Debug.LogError($"��ǰ�������ܷ�������  {funcAnalysisFilePath}������");
            }
        }
    }

    void MonitorInfosReport()
    {
        if (monitorInfos.MonitorInfoList.Count > 1)
        {
            monitorInfos.MonitorInfoList.RemoveAt(monitorInfos.MonitorInfoList.Count - 1);
        }
        bool writeRes = false;
        if (!UseBinary)
        {
            writeRes = FileManager.WriteToFile(monitorFilePath, JsonUtility.ToJson(monitorInfos));
        }
        else
        {
            writeRes = FileManager.WriteBinaryDataToFile(monitorFilePath, monitorInfos);
        }
        if (writeRes)
        {
            UploadFile(monitorFilePath);
        }
    }

    void Tick()
    {
        m_TickTime++;
    }

    void UploadFile(string filePath)
    {
        FileUploadManager.UploadFile(filePath, (sender, e) =>
        {
            Debug.Log("Uploading Progreess: " + e.ProgressPercentage);
            if (e.ProgressPercentage > 0 && e.ProgressPercentage < 100)
            {
                if (UploadTips != null && !UploadTips.gameObject.activeSelf)
                {
                    UploadTips.gameObject.SetActive(true);
                }
            }
            else if (e.ProgressPercentage >= 100)
            {
                if (UploadTips != null && UploadTips.gameObject.activeSelf)
                {
                    UploadTips.gameObject.SetActive(false);
                }
            }
            UploadTips.text = $"�����ϴ���,����{e.ProgressPercentage}%";
        }, (sender, e) =>
        {
            Debug.Log($"File Uploaded :{e.Result}");
        });
    }

    [HideAnalysis]
    void OnGUI()
    {
        if (GUI.Button(new Rect(150, 350, 200, 100), btnMsg))
        {
            btnMonitor = !btnMonitor;
            btnMsg = btnMonitor ? ConstString.Monitoring : ConstString.MonitorBegin;
            if (MonitorCallback != null)
                MonitorCallback.Invoke(btnMonitor);
        }
        if (btnMonitor)
            btnMsg = $"{ConstString.Monitoring}{m_TickTime}s";
        GUI.Label(new Rect(Screen.width / 2, 0, 100, 100), "FPS:" + m_FPS);
    }

    [HideAnalysis]
    void Update()
    {
        m_Frames++;
        m_Accumulator += Time.unscaledDeltaTime;
        m_TimeLeft -= Time.unscaledDeltaTime;
        if (m_TimeLeft <= 0f)
        {
            m_FPS = (int)(m_Accumulator > 0f ? m_Frames / m_Accumulator : 0f);
            m_Frames = 0;
            m_Accumulator = 0f;
            m_TimeLeft += m_UpdateInterval;
        }

        if (btnMonitor)
        {
            ++m_frameIndex;
            if (m_frameIndex > IgnoreFrameCount)
            {
                var monitorInfo = new MonitorInfo() { FrameIndex = m_frameIndex - IgnoreFrameCount, BatteryLevel = SystemInfo.batteryLevel, MemorySize = 0, Frame = m_FPS, MonoHeapSize = Profiler.GetMonoHeapSizeLong(), MonoUsedSize = Profiler.GetMonoUsedSizeLong(), TotalAllocatedMemory = Profiler.GetTotalAllocatedMemoryLong(), TotalUnusedReservedMemory = Profiler.GetTotalUnusedReservedMemoryLong(), UnityTotalReservedMemory = Profiler.GetTotalReservedMemoryLong(), AllocatedMemoryForGraphicsDriver = Profiler.GetAllocatedMemoryForGraphicsDriver() };
                monitorInfos.MonitorInfoList.Add(monitorInfo);
                if (EnableFrameTexture)
                {
                    ScreenCapture.CaptureScreenshot($"{Application.persistentDataPath}/{m_StartTime}/img_{m_StartTime}_{m_frameIndex - IgnoreFrameCount}.png");
                }
            }
        }
    }

    void GetSystemInfo()
    {
        DeviceInfo deviceInfo = new DeviceInfo()
        {
            UnityVersion = Application.unityVersion,
            DeviceModel = SystemInfo.deviceModel,
            BatteryLevel = SystemInfo.batteryLevel,
            DeviceName = SystemInfo.deviceName,
            DeviceUniqueIdentifier = SystemInfo.deviceUniqueIdentifier,
            GraphicsDeviceName = SystemInfo.graphicsDeviceName,
            GraphicsDeviceVendor = SystemInfo.graphicsDeviceVendor,
            GraphicsDeviceVersion = SystemInfo.graphicsDeviceVersion,
            GraphicsMemorySize = SystemInfo.graphicsMemorySize,
            OperatingSystem = SystemInfo.operatingSystem,
            ProcessorCount = SystemInfo.processorCount,
            ProcessorFrequency = SystemInfo.processorFrequency,
            ProcessorType = SystemInfo.processorType,
            SupportsShadows = SystemInfo.supportsShadows,
            SystemMemorySize = SystemInfo.systemMemorySize,
            ScreenHeight = Screen.height,
            ScreenWidth = Screen.width
        };
        bool writeRes = false;
        if (!UseBinary)
        {
            writeRes = FileManager.WriteToFile(deviceFilePath, JsonUtility.ToJson(deviceInfo));
        }
        else
        {
            writeRes = FileManager.WriteBinaryDataToFile(deviceFilePath, deviceInfo);
        }
        if (writeRes)
        {
            UploadFile(deviceFilePath);
        }
    }
}
