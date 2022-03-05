using MonitorLib.GOT.Editor;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

public class HookEditor
{
    private const string AssemblyPath = "./Library/ScriptAssemblies/Assembly-CSharp.dll";
    [MenuItem("Hook/ע�����")]
    public static void HookInject()
    {
        AssemblyPostProcessorRun();
    }

    [MenuItem("Hook/������")]
    public static void HookUtilsReport()
    {

    }

    [PostProcessScene] //�����ʱ����Զ����������ע�뷽��
    static void AssemblyPostProcessorRun()
    {
        try
        {
            if (Application.isPlaying || EditorApplication.isCompiling)
            {
                Debug.Log("You need stop play mode or wait compiling finished");
                return;
            }
            EditorApplication.LockReloadAssemblies();
            // ��·����ȡ����
            var readerParameters = new ReaderParameters { ReadSymbols = false };
            var assembly = AssemblyDefinition.ReadAssembly(AssemblyPath, readerParameters);
            if (assembly == null)
            {
                Debug.LogError(string.Format("InjectTool Inject Load assembly failed: {0}", AssemblyPath));
                return;
            }
            if (HookEditor.ProcessAssembly(assembly))
            {
                assembly.Write(AssemblyPath, new WriterParameters { WriteSymbols = true });
            }
            else
            {
                Debug.LogError(Path.GetFileName(AssemblyPath) + "�޷�����ȷ����");
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
        EditorApplication.UnlockReloadAssemblies();
        Debug.Log("ע��ɹ�");
    }

    private static bool ProcessAssembly(AssemblyDefinition assembly)
    {
        bool hasProcessed = false;
        foreach (var module in assembly.Modules)
        {
            foreach (var type in module.Types)
            {
                if (type.IsAbstract || type.IsInterface)//���˳�����ͽӿ�
                    continue;
                foreach (var method in type.Methods)
                {
                    //���˹��캯��
                    if (method.Name == ".ctor" || method.Name == ".cctor")
                        continue;
                    //���˳��󷽷����麯����get��set����
                    if (method.IsAbstract || method.IsVirtual || method.IsGetter || method.IsSetter)
                        continue;
                    //���ע�����ʧ�ܣ����Դ��������������������Ǹ������ϡ�
                    //Debug.Log(method.Name + "======= " + type.Name + "======= " + type.BaseType.GenericParameters +" ===== "+ module.Name);
                    var hookUtilBegin = module.ImportReference(typeof(HookUtil).GetMethod("Begin", new[] { typeof(string) }));
                    var hookUtilEnd = module.ImportReference(typeof(HookUtil).GetMethod("End", new[] { typeof(string) }));
                    ILProcessor ilProcessor = method.Body.GetILProcessor();

                    Instruction first = method.Body.Instructions[0];
                    ilProcessor.InsertBefore(first, Instruction.Create(OpCodes.Ldstr, type.FullName + "." + method.Name));
                    ilProcessor.InsertBefore(first, Instruction.Create(OpCodes.Call, hookUtilBegin));

                    //���������ֱ�� return ���޷�ͳ�Ƶ�bug 
                    //https://lostechies.com/gabrielschenker/2009/11/26/writing-a-profiler-for-silverlight-applications-part-1/

                    Instruction last = method.Body.Instructions[method.Body.Instructions.Count - 1];
                    Instruction lastInstruction = Instruction.Create(OpCodes.Ldstr, type.FullName + "." + method.Name);
                    ilProcessor.InsertBefore(last, lastInstruction);
                    ilProcessor.InsertBefore(last, Instruction.Create(OpCodes.Call, hookUtilEnd));

                    var jumpInstructions = method.Body.Instructions.Cast<Instruction>().Where(i => i.Operand == lastInstruction);
                    foreach (var jump in jumpInstructions)
                    {
                        jump.Operand = lastInstruction;
                    }
                    hasProcessed = true;
                }
            }
        }
        return hasProcessed;
    }
}
