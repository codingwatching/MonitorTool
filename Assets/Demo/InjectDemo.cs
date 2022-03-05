using MonitorLib.GOT;
using System;
using UnityEngine;
using UnityEngine.Profiling;

namespace TestModule
{
    public class Normal
    {
        public static int GetMax(int a, int b)
        {
            Debug.LogFormat("a = {0}, b = {1}", a, b);
            return a > b ? a : b;
        }
    }

    [TestInject]
    public class Inject
    {
        public static int GetMax(int a, int b)
        {
            return a;
        }
    }

    public class InjectDemo : MonoBehaviour
    {
        [ProfilerSample]
        void Start()
        {
            Debug.LogFormat("Normal Max: {0}", Normal.GetMax(6, 9));
            Debug.LogFormat("Inject Max: {0}", Inject.GetMax(6, 9));
            //for (int i = 0; i < 3; i++)
            Test();
            TestDefine();
        }

        [FunctionAnalysis]
        //[ProfilerSample]
        public void Test()
        {
            Profiler.BeginSample("****************");
            Debug.Log("��ʼѭ��100��");
            for (int i = 0; i < 100; i++)
            {
                Debug.Log(i);
            }
            Debug.Log("����ѭ��100��");
            Profiler.EndSample();
        }
        //[ProfilerSampleWithDefineName("-------�Զ���Sample����")]
        [FunctionAnalysis]
        public void TestDefine()
        {
            Debug.Log("���������Եķ���");
        }
    }
}