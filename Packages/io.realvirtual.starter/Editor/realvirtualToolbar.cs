// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace realvirtual
{
    [InitializeOnLoad]
    public class realvirtualToolbar : EditorWindow
    {
        private bool groupEnabled;
        
        //! Helper method to show Unity 2022 compatibility warnings for Unity 6-only interfaces
        private static bool CheckUnity6Compatibility(string interfaceName)
        {
#if !UNITY_6000_0_OR_NEWER
            EditorUtility.DisplayDialog("Unity 2022 Compatibility Notice",
                $"The {interfaceName} interface requires Unity 6 or newer due to advanced API dependencies " +
                "(Awaitable API, WebSocketSharp, or RenderGraphModule).\n\n" +
                "In Unity 2022, this interface is not available. Please upgrade to Unity 6 to use this interface, " +
                "or use alternative interfaces compatible with Unity 2022 such as S7, OPCUA, Modbus, TwinCAT ADS, or EthernetIP.",
                "OK");
            return false;
#else
            return true;
#endif
        }

        [MenuItem("Tools/realvirtual/Create new realvirtual Scene", false, 1)]
        static void CreateNewScene()
        {
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            AddComponent("Packages/io.realvirtual.starter/Assets/Prefabs/realvirtual.prefab");
        }


        [MenuItem("Tools/realvirtual/Export/Full project as package", false, 51)]
        static void ExportWholeProjet()
        {
            string[] s = Application.dataPath.Split('/');
            string projectName = s[s.Length - 2];

            var path = EditorUtility.SaveFilePanel(
                "Export full project as package",
                "",
                projectName,
                "unitypackage");

            if (path.Length != 0)
            {
                AssetDatabase.ExportPackage("Assets", path,
                    ExportPackageOptions.Interactive | ExportPackageOptions.Recurse |
                    ExportPackageOptions.IncludeLibraryAssets | ExportPackageOptions.IncludeDependencies);
            }
        }

        [MenuItem("Tools/realvirtual/Export/Current scene as package", false, 52)]
        static void ExportScene()
        {
            string projectName = SceneManager.GetActiveScene().name;
            string assetpath = SceneManager.GetActiveScene().path;
            var path = EditorUtility.SaveFilePanel(
                "Export current scene including dependencies as package",
                "",
                projectName,
                "unitypackage");

            if (path.Length != 0)
            {
                AssetDatabase.ExportPackage(assetpath, path,
                    ExportPackageOptions.Interactive | ExportPackageOptions.Recurse |
                    ExportPackageOptions.IncludeDependencies);
            }
        }

        [MenuItem("Tools/realvirtual/Export/Selected as package", false, 53)]
        static void ExportSelected()
        {
            var selected = Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets);
            if (selected.Length == 0)
            {
               Debug.LogError("Please select an object within the Project to export. The current selection is not valid in the given context.");
               return;
            }
            var obj1 = selected[0];
            var projectName = obj1.name;
            string assetpath = AssetDatabase.GetAssetPath(obj1);
            var path = EditorUtility.SaveFilePanel(
                "Export selected folder as package",
                "",
                projectName,
                "unitypackage");

            if (path.Length != 0)
            {
                AssetDatabase.ExportPackage(assetpath, path,
                    ExportPackageOptions.Interactive | ExportPackageOptions.Recurse);
            }
        }
        
#if REALVIRTUAL_ZIP
        [MenuItem("Tools/realvirtual/Export/Full project as ZIP", false, 53)]
        static void ExportProjectAsZip()
        {
            string[] s = Application.dataPath.Split('/');
            string projectName = s[s.Length - 2];

            string filename = projectName + "-" + Global.Version;
            filename = filename.Replace(" ", "");
            filename = filename.Replace("(", "-");
            filename = filename.Replace(")", "");
            var exportfile = EditorUtility.SaveFilePanel("Save full Project path", "", filename, "zip");
            if (exportfile.Length != 0)
            {
                string p = Application.dataPath;
                p = p.Replace("/Assets", "");

                // Use ZipHelper to avoid loading Ionic.Zip.dll at compile time
                // This allows the DLL to be included in UPM package exports
                ZipHelper.CreateProjectZip(p, exportfile);
            }
        }
#endif



        [MenuItem("Tools/realvirtual/Add CAD Link (Pro)", false, 150)]
        static void AddCADLink()
        {
            var find = AssetDatabase.FindAssets(
                "CADLink t:prefab");
            if (find.Length > 0)
                AddComponent(AssetDatabase.GUIDToAssetPath(find[0]));
            else
            {
                EditorUtility.DisplayDialog("Warning",
                    "CADLink is only included in realvirtual Professional", "OK");
            }
        }

        [MenuItem("Tools/realvirtual/Add Component/Source", false, 160)]
        static void AddSource()
        {
            AddScript(typeof(Source));
        }

        [MenuItem("Tools/realvirtual/Add Component/MU", false, 160)]
        static void AddMU()
        {
            AddScript(typeof(MU));
        }

        [MenuItem("Tools/realvirtual/Add Component/Drive", false, 160)]
        static void AddDrive()
        {
            AddScript(typeof(Drive));
        }

        [MenuItem("Tools/realvirtual/Add Component/Transport Surface", false, 160)]
        static void AddTransportSurface()
        {
            AddScript(typeof(TransportSurface));
        }

        [MenuItem("Tools/realvirtual/Add Component/Drive Behaviour/Simple Drive", false, 160)]
        static void AddDriveBehaviourSimple()
        {
            AddScript(typeof(Drive_Simple));
        }

        [MenuItem("Tools/realvirtual/Add Component/Drive Behaviour/Destination Drive", false, 160)]
        static void AddDriveBehaviourDestination()
        {
            AddScript(typeof(Drive_DestinationMotor));
        }

        [MenuItem("Tools/realvirtual/Add Component/Drive Behaviour/Cylinder", false, 160)]
        static void AddDriveBehaviourCylinder()
        {
            AddScript(typeof(Drive_Cylinder));
        }

        [MenuItem("Tools/realvirtual/Add Component/Drive Behaviour/Speed", false, 160)]
        static void AddDriveBehaviourSpeed()
        {
            AddScript(typeof(Drive_Speed));
        }

        [MenuItem("Tools/realvirtual/Add Component/Drive Behaviour/ContinousDestination", false, 160)]
        static void AddDriveBehaviourContinousDestination()
        {
            AddScript(typeof(Drive_ContinousDestination));
        }


        [MenuItem("Tools/realvirtual/Add Component/Drive Behaviour/Gear", false, 160)]
        static void AddDriveBehaviourGear()
        {
            AddScript(typeof(Drive_Gear));
        }

        [MenuItem("Tools/realvirtual/Add Component/Drive Behaviour/CAM", false, 160)]
        static void AddDriveBehaviourCAM()
        {
            AddScript(typeof(CAM));
        }

        [MenuItem("Tools/realvirtual/Add Component/Drive Behaviour/CAMTime", false, 160)]
        static void AddDriveBehaviourCAMTime()
        {
            AddScript(typeof(CAMTime));
        }

        #region LogicStep Components

        [MenuItem("Tools/realvirtual/Add Component/LogicStep/Serial Container", false, 160)]
        static void AddLogicSerialContainer()
        {
            AddScript(typeof(LogicStep_SerialContainer));
        }

        [MenuItem("Tools/realvirtual/Add Component/LogicStep/Parallel Container", false, 160)]
        static void AddLogicParallelContainer()
        {
            AddScript(typeof(LogicStep_ParallelContainer));
        }

        [MenuItem("Tools/realvirtual/Add Component/LogicStep/Delay", false, 160)]
        static void AddLogicDelay()
        {
            AddScript(typeof(LogicStep_Delay));
        }

        [MenuItem("Tools/realvirtual/Add Component/LogicStep/Drive to", false, 160)]
        static void AddLogicDriveTo()
        {
            AddScript(typeof(LogicStep_DriveTo));
        }

        [MenuItem("Tools/realvirtual/Add Component/LogicStep/Start Drive", false, 160)]
        static void AddLogicStartDrive()
        {
            AddScript(typeof(LogicStep_StartDriveTo));
        }

        [MenuItem("Tools/realvirtual/Add Component/LogicStep/Jump", false, 160)]
        static void AddLogicJump()
        {
            AddScript(typeof(LogicStep_JumpOnSignal));
        }

        [MenuItem("Tools/realvirtual/Add Component/LogicStep/Set Signal Bool", false, 160)]
        static void AddLogicSetSignal()
        {
            AddScript(typeof(LogicStep_SetSignalBool));
        }

        [MenuItem("Tools/realvirtual/Add Component/LogicStep/Wait for Drives", false, 160)]
        static void AddLogicStepWaitForDrives()
        {
            AddScript(typeof(LogicStep_WaitForDrivesAtTarget));
        }

        [MenuItem("Tools/realvirtual/Add Component/LogicStep/Wait for Sensor", false, 160)]
        static void AddLogicWaitForSensor()
        {
            AddScript(typeof(LogicStep_WaitForSensor));
        }

        [MenuItem("Tools/realvirtual/Add Component/LogicStep/Wait for Signal Bool", false, 160)]
        static void AddLogicWaitForSignal()
        {
            AddScript(typeof(LogicStep_WaitForSignalBool));
        }

        #endregion

        [MenuItem("Tools/realvirtual/Add Component/Sensor", false, 160)]
        static void AddSensor()
        {
            AddScript(typeof(Sensor));
        }

        [MenuItem("Tools/realvirtual/Add Component/Measure", false, 160)]
        static void AddMeasureComponent()
        {
            AddScript(typeof(Measure));
        }

        [MenuItem("Tools/realvirtual/Add Component/MeasureRaycast", false, 160)]
        static void AddMeasureRaycastComponent()
        {
            AddScript(typeof(MeasureRaycast));
        }


        [MenuItem("Tools/realvirtual/Add Component/Grip", false, 160)]
        static void AddGrip()
        {
            AddScript(typeof(Grip));
        }

        [MenuItem("Tools/realvirtual/Add Component/Sink", false, 160)]
        static void AddSink()
        {
            AddScript(typeof(Sink));
        }


        [MenuItem("Tools/realvirtual/Add Component/Group", false, 160)]
        static void AddGroup()
        {
            AddScript(typeof(Group));
        }

        [MenuItem("Tools/realvirtual/Add Component/Kinematic", false, 160)]
        static void AddKinematicScript()
        {
            AddScript(typeof(Kinematic));
        }

        [MenuItem("Tools/realvirtual/Add Component/Chain", false, 160)]
        static void AddChainScript()
        {
            AddScript(typeof(Chain));
        }

        [MenuItem("Tools/realvirtual/Add Component/Chain element", false, 160)]
        static void AddChainElementScript()
        {
            AddScript(typeof(ChainElement));
        }
        
        [MenuItem("Tools/realvirtual/Add Component/Guide Line", false, 160)]
        static void AddGuideLineComp()
        {
            AddScript(typeof(GuideLine));
        }
        
        [MenuItem("Tools/realvirtual/Add Component/Guide Circle", false, 160)]
        static void AddGuideCircleComp()
        {
            AddScript(typeof(GuideCircle));
        }

#if !REALVIRTUAL_PROFESSIONAL
        [MenuItem("Tools/realvirtual/Add Component/PerformanceOptimizer (Pro)", false, 160)]
        static void AddPerformanceOptimizer()
        {
            EditorUtility.DisplayDialog("Info",
                "The PerformanceOptimizer is only included in Game4Automation Professional.",
                "OK");
        }

        [MenuItem("Tools/realvirtual/Add Component/SignalManager (Pro)", false, 160)]
        static void AddSignalManager()
        {
            EditorUtility.DisplayDialog("Info",
                "SignalManager is only included in Game4Automation Professional.",
                "OK");
        }

        [MenuItem("Tools/realvirtual/Add Component/RobotIK (Pro)", false, 160)]
        static void AddRobotIK()
        {
            EditorUtility.DisplayDialog("Info",
                "RobotIK is only included in realvirtual Professional.",
                "OK");
        }

        [MenuItem("Tools/realvirtual/Add Component/Robot Path (Pro)", false, 160)]
        static void AddIKPath()
        {
            EditorUtility.DisplayDialog("Info",
                "Robot Path is only included in realvirtual Professional.",
                "OK");
        }
#endif

        [MenuItem("Tools/realvirtual/Add Object/Sensor Beam", false, 151)]
        static void AddSensorBeamn()
        {
            AddComponent("Packages/io.realvirtual.starter/Assets/Prefabs/SensorBeam.prefab");
        }

        [MenuItem("Tools/realvirtual/Add Object/Measure", false, 151)]
        static void AddMeasure()
        {
            AddComponent("Packages/io.realvirtual.starter/Assets/Prefabs/Measure.prefab");
        }

        [MenuItem("Tools/realvirtual/Add Object/MeasureRaycast", false, 151)]
        static void AddMeasureRaycast()
        {
            AddComponent("Packages/io.realvirtual.starter/Assets/Prefabs/MeasureRaycast.prefab");
        }

        [MenuItem("Tools/realvirtual/Add Object/Lamp", false, 170)]
        static void AddLamp()
        {
            AddComponent("Packages/io.realvirtual.starter/Assets/Prefabs/Lamp.prefab");
        }

        [MenuItem("Tools/realvirtual/Add Object/UI/Button", false, 170)]
        static void AddPushButton()
        {
            AddComponent("Packages/io.realvirtual.starter/Assets/Prefabs/UIButton.prefab");
        }

        [MenuItem("Tools/realvirtual/Add Object/UI/Lamp", false, 170)]
        static void AddUILamp()
        {
            AddComponent("Packages/io.realvirtual.starter/Assets/Prefabs/UILamp.prefab");
        }


        [MenuItem("Tools/realvirtual/Add Object/Signal/PLC Input Bool", false, 155)]
        static void AddPLCInputBool()
        {
            AddComponent("Packages/io.realvirtual.starter/Assets/Prefabs/PLCInputBool.prefab");
        }

        [MenuItem("Tools/realvirtual/Add Object/Signal/PLC Input Float", false, 155)]
        static void AddPLCInputFloat()
        {
            AddComponent("Packages/io.realvirtual.starter/Assets/Prefabs/PLCInputFloat.prefab");
        }

        [MenuItem("Tools/realvirtual/Add Object/Signal/PLC Input Int", false, 155)]
        static void AddPLCInpuInt()
        {
            AddComponent("Packages/io.realvirtual.starter/Assets/Prefabs/PLCInputInt.prefab");
        }

        [MenuItem("Tools/realvirtual/Add Object/Signal/PLC Output Bool", false, 155)]
        static void AddPLCOutputBool()
        {
            AddComponent("Packages/io.realvirtual.starter/Assets/Prefabs/PLCOutputBool.prefab");
        }

        [MenuItem("Tools/realvirtual/Add Object/Signal/PLC Output Float", false, 155)]
        static void AddPLCOutputFloat()
        {
            AddComponent("Packages/io.realvirtual.starter/Assets/Prefabs/PLCOutputFloat.prefab");
        }

        [MenuItem("Tools/realvirtual/Add Object/Signal/PLC Output Int", false, 155)]
        static void AddPLCOutputInt()
        {
            AddComponent("Packages/io.realvirtual.starter/Assets/Prefabs/PLCOutputInt.prefab");
        }


#if UNITY_STANDALONE_WIN
        [MenuItem("Tools/realvirtual/Add Interface/ABB RobotStudio (Pro)", false, 155)]
        static void AddRobotStudioInterface()
        {
            var find = AssetDatabase.FindAssets(
                "ABBRobotStudioInterface t:prefab");
            if (find.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(find[0]);
                AddComponent(path);
            }
            else
            {
                EditorUtility.DisplayDialog("Warning",
                    "This interface is only included in realvirtual.io Professional", "OK");
            }
        }
#endif

        [MenuItem("Tools/realvirtual/Add Interface/Bosch Rexroth ctrlX (Pro)", false, 155)]
        static void AddCtrlXInterface()
        {
            var type = System.AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == "CtrlXInterface");
            if (type != null)
            {
                var go = new GameObject("Bosch Rexroth ctrlX Interface");
                go.AddComponent(type);
                Selection.activeGameObject = go;
                Undo.RegisterCreatedObjectUndo(go, "Add ctrlX Interface");
            }
            else
            {
                EditorUtility.DisplayDialog("Warning",
                    "This interface is only included in realvirtual.io Professional", "OK");
            }
        }

        [MenuItem("Tools/realvirtual/Add Interface/Denso Robotics (Pro)", false, 156)]
        static void AddDensoInterface()
        {
            var find = AssetDatabase.FindAssets(
                "DensoInterface t:prefab");
            if (find.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(find[0]);
                AddComponent(path);
            }
            else
            {
                EditorUtility.DisplayDialog("Warning",
                    "This interface is only included in realvirtual.io Professional", "OK");
            }
        }

        [MenuItem("Tools/realvirtual/Add Interface/EthernetIP (Pro)", false, 157)]
        static void AddEthernetIPInterface()
        {
            var find = AssetDatabase.FindAssets(
                "EthernetIPInterface t:prefab");
            if (find.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(find[0]);
                AddComponent(path);
            }
            else
            {
                EditorUtility.DisplayDialog("Warning",
                    "This interface is only included in realvirtual.io Professional", "OK");
            }
        }

        [MenuItem("Tools/realvirtual/Add Interface/Fanuc (Pro)", false, 158)]
        static void AddFanucInterface()
        {
            var find = AssetDatabase.FindAssets(
                "FanucInterface t:prefab");
            if (find.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(find[0]);
                AddComponent(path);
            }
            else
            {
                EditorUtility.DisplayDialog("Warning",
                    "This interface is only included in realvirtual.io Professional", "OK");
            }
        }

        [MenuItem("Tools/realvirtual/Add Interface/Festo AX Controls (Pro)", false, 158)]
        static void AddFestoInterface()
        {
            var type = System.AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == "FestoInterface");
            if (type != null)
            {
                var go = new GameObject("Festo AX Controls Interface");
                go.AddComponent(type);
                Selection.activeGameObject = go;
                Undo.RegisterCreatedObjectUndo(go, "Add Festo Interface");
            }
            else
            {
                EditorUtility.DisplayDialog("Warning",
                    "This interface is only included in realvirtual.io Professional", "OK");
            }
        }

        [MenuItem("Tools/realvirtual/Add Interface/IgusRebel", false, 159)]
        static void AddIgusRebelInterface()
        {
            var find = AssetDatabase.FindAssets(
                "igusRebelInterface t:prefab");
            if (find.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(find[0]);
                AddComponent(path);
            }
            else
            {
                
            }
        }

        [MenuItem("Tools/realvirtual/Add Interface/Keba (Pro)", false, 160)]
        static void AddKebaInterface()
        {
            var find = AssetDatabase.FindAssets(
                "KebaInterface t:prefab");
            if (find.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(find[0]);
                AddComponent(path);
            }
            else
            {
                EditorUtility.DisplayDialog("Warning",
                    "This interface is only included in realvirtual.io Professional", "OK");
            }
        }

        [MenuItem("Tools/realvirtual/Add Interface/Kuka (Pro)", false, 161)]
        static void AddKukaInterface()
        {
            var find = AssetDatabase.FindAssets(
                "KUKAInterface t:prefab");
            if (find.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(find[0]);
                AddComponent(path);
            }
            else
            {
                EditorUtility.DisplayDialog("Warning",
                    "This interface is only included in realvirtual.io Professional", "OK");
            }
        }

        [MenuItem("Tools/realvirtual/Add Interface/Modbus (Pro)", false, 162)]
        static void AddPLCConnectInterface()
        {
            var find = AssetDatabase.FindAssets(
                "ModbusInterface t:prefab");
            if (find.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(find[0]);
                AddComponent(path);
            }
            else
            {
                EditorUtility.DisplayDialog("Warning",
                    "This interface is only included in realvirtual.io Professional", "OK");
            }
        }

        [MenuItem("Tools/realvirtual/Add Interface/MQTT (Pro)", false, 163)]
        static void AddMQTTInterface()
        {
            var find = AssetDatabase.FindAssets(
                "MQTTInterface t:prefab");
            if (find.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(find[0]);
                AddComponent(path);
            }
            else
            {
                EditorUtility.DisplayDialog("Warning",
                    "This interface is only included in realvirtual.io Professional", "OK");
            }
        }

        [MenuItem("Tools/realvirtual/Add Interface/Mitsubishi McpX (Pro)", false, 163)]
        static void AddMitsubishiInterface()
        {
            var find = AssetDatabase.FindAssets(
                "MitsubishiMcpXInterface t:prefab");
            if (find.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(find[0]);
                AddComponent(path);
            }
            else
            {
                EditorUtility.DisplayDialog("Warning",
                    "This interface is only included in realvirtual.io Professional", "OK");
            }
        }

        [MenuItem("Tools/realvirtual/Add Interface/OPCUA (Pro)", false, 165)]
        static void AddOPCUAInterface()
        {
            var find = AssetDatabase.FindAssets(
                "OPCUAInterface t:prefab");
            if (find.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(find[0]);
                AddComponent(path);
            }
            else
            {
                EditorUtility.DisplayDialog("Warning",
                    "This interface is only included in realvirtual.io Professional", "OK");
            }
        }

#if UNITY_STANDALONE_WIN
        [MenuItem("Tools/realvirtual/Add Interface/PLCSIMAdvanced (Pro)", false, 165)]
        static void AddPLCSimAdvancedInterface()
        {
            var find = AssetDatabase.FindAssets(
                "PLCSIMAdvancedInterface t:prefab");
            if (find.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(find[0]);
                AddComponent(path);
            }
            else
            {
                EditorUtility.DisplayDialog("Warning",
                    "This interface is only included in realvirtual.io Professional", "OK");
            }
        }
#endif

        [MenuItem("Tools/realvirtual/Add Interface/RFSuite (Pro)", false, 166)]
        static void AddRFSuiteInterface()
        {
            var find = AssetDatabase.FindAssets(
                "RFSuiteInterface t:prefab");
            if (find.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(find[0]);
                AddComponent(path);
            }
            else
            {
                EditorUtility.DisplayDialog("Warning",
                    "This interface is only included in realvirtual.io Professional", "OK");
            }
        }

#if UNITY_STANDALONE_WIN
        [MenuItem("Tools/realvirtual/Add Interface/RoboDK (Pro)", false, 167)]
        static void AddRoboDKInterface()
        {
            var find = AssetDatabase.FindAssets(
                "RoboDKInterface t:prefab");
            if (find.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(find[0]);
                AddComponent(path);
            }
            else
            {
                EditorUtility.DisplayDialog("Warning",
                    "This interface is only included in realvirtual.io Professional", "OK");
            }
        }
#endif

        [MenuItem("Tools/realvirtual/Add Interface/S7", false, 168)]
        static void AddS7Interface()
        {
            AddComponent("Packages/io.realvirtual.starter/Assets/Prefabs/S7Interface.prefab");
        }

        [MenuItem("Tools/realvirtual/Add Interface/SEW MQTT (Pro)", false, 169)]
        static void AddSEWMQTTINterface()
        {
            var find = AssetDatabase.FindAssets(
                "SEWSimInterface t:prefab");
            if (find.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(find[0]);
                AddComponent(path);
            }
            else
            {
                EditorUtility.DisplayDialog("Warning",
                    "This interface is only included in realvirtual.io Professional", "OK");
            }
        }

#if UNITY_STANDALONE_WIN
        [MenuItem("Tools/realvirtual/Add Interface/Siemens Simit (Pro)", false, 170)]
        static void AddSiemensSimitInterface()
        {
            var find = AssetDatabase.FindAssets(
                "SiemensSimitInterface t:prefab");
            if (find.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(find[0]);
                AddComponent(path);
            }
            else
            {
                EditorUtility.DisplayDialog("Warning",
                    "This interface is only included in realvirtual.io Professional", "OK");
            }
        }
#endif

#if UNITY_STANDALONE_WIN
        [MenuItem("Tools/realvirtual/Add Interface/SIMIT Shared Memory (Pro)", false, 171)]
        static void AddSharedMemoryInterface()
        {
            var find = AssetDatabase.FindAssets(
                "SharedMemoryInterface t:prefab");
            if (find.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(find[0]);
                AddComponent(path);
            }
            else
                {
                    EditorUtility.DisplayDialog("Warning",
                        "This interface is only included in realvirtual.io Professional", "OK");
                }
        }
#endif

#if UNITY_STANDALONE_WIN
        [MenuItem("Tools/realvirtual/Add Interface/Simulink (Pro)", false, 172)]
        static void AddSimulinkInterface()
        {
            var find = AssetDatabase.FindAssets(
                "SimulinkInterface t:prefab");
            if (find.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(find[0]);
                AddComponent(path);
            }
            else
            {
                EditorUtility.DisplayDialog("Warning",
                    "This interface is only included in realvirtual.io Professional", "OK");
            }
        }
#endif

#if UNITY_STANDALONE_WIN
        [MenuItem("Tools/realvirtual/Add Interface/TwinCAT ADS (Pro)", false, 173)]
        static void AddTwinCATInterface()
        {
            var find = AssetDatabase.FindAssets(
                "TwinCATInterface t:prefab");
            if (find.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(find[0]);
                AddComponent(path);
            }
            else
            {
                EditorUtility.DisplayDialog("Warning",
                    "This interface is only included in realvirtual.io Professional", "OK");
            }
        }
#endif

        [MenuItem("Tools/realvirtual/Add Interface/TwinCAT HMI (Pro)", false, 174)]
        static void AddTwinCATHMIInterface()
        {
            var find = AssetDatabase.FindAssets(
                "TwinCATHMIInterface t:prefab");
            if (find.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(find[0]);
                AddComponent(path);
            }
            else
            {
                EditorUtility.DisplayDialog("Warning",
                    "This interface is only included in realvirtual.io Professional", "OK");
            }
        }

        [MenuItem("Tools/realvirtual/Add Interface/UDP (Pro)", false, 175)]
        static void AddUDPInterface()
        {
            var find = AssetDatabase.FindAssets(
                "UDPInterface t:prefab");
            if (find.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(find[0]);
                AddComponent(path);
            }
            else
            {
                EditorUtility.DisplayDialog("Warning",
                    "This interface is only included in realvirtual.io Professional", "OK");
            }
        }

        [MenuItem("Tools/realvirtual/Add Interface/UniversalRobots (Pro)", false, 176)]
        static void AddUniversalRobots()
        {
            var find = AssetDatabase.FindAssets(
                "UniversalRobotsInterface t:prefab");
            if (find.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(find[0]);
                AddComponent(path);
            }
            else
            {
                EditorUtility.DisplayDialog("Warning",
                    "This interface is only included in realvirtual.io Professional", "OK");
            }
        }

        [MenuItem("Tools/realvirtual/Add Interface/Wandelbots NOVA (Pro)", false, 177)]
        static void AddWandelbotsInterface()
        {
            if (!CheckUnity6Compatibility("Wandelbots NOVA"))
                return;
                
            var find = AssetDatabase.FindAssets(
                "WandelbotsNOVAInterface t:prefab");
            if (find.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(find[0]);
                AddComponent(path);
            }
            else
            {
                EditorUtility.DisplayDialog("Warning",
                    "This interface is only included in realvirtual.io Professional", "OK");
            }
        }

        [MenuItem("Tools/realvirtual/Add Interface/Websocket Realtime (Pro)", false, 178)]
        static void AddWebsocketInterface()
        {
            if (!CheckUnity6Compatibility("Websocket Realtime"))
                return;
                
            var find = AssetDatabase.FindAssets(
                "WebsocketRealtimeInterface t:prefab");
            if (find.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(find[0]);
                AddComponent(path);
            }
            else
            {
                EditorUtility.DisplayDialog("Warning",
                    "This interface is only included in realvirtual.io Professional", "OK");
            }
        }

        [MenuItem("Tools/realvirtual/Add Interface/Winmod Y200 (Pro)", false, 179)]
        static void AddWinmodInterface()
        {
            var find = AssetDatabase.FindAssets(
                "WinmodY200Interface t:prefab");
            if (find.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(find[0]);
                AddComponent(path);
            }
            else
            {
                EditorUtility.DisplayDialog("Warning",
                    "This interface is only included in realvirtual.io Professional", "OK");
            }
        }

        [MenuItem("Tools/realvirtual/Add Object/Realvirtual", false, 179)]
        static void AddGame4Automatoin()
        {
            AddComponent("Packages/io.realvirtual.starter/Assets/Prefabs/realvirtual.prefab");
        }
        
        [MenuItem("Tools/realvirtual/Add Object/TransportGuided", false, 157)]
        static void AddTransportGuided()
        {
            AddComponent("Packages/io.realvirtual.starter/Assets/Prefabs/TransportGuided.prefab");
        }
        
        [MenuItem("Tools/realvirtual/Add Object/GuideLine", false, 158)]
        static void AddGuideLine()
        {
            AddComponent("Packages/io.realvirtual.starter/Assets/Prefabs/GuideLine.prefab");
        }
        
        [MenuItem("Tools/realvirtual/Add Object/GuideCircle", false, 159)]
        static void AddGuideCircle()
        {
            AddComponent("Packages/io.realvirtual.starter/Assets/Prefabs/GuideCircle.prefab");
        }


        [MenuItem("Tools/realvirtual/Settings/Apply standard settings", false, 911)]
        private static void SetStandardSettingsMenu()
        {
            ProjectSettingsTools.SetStandardSettings(true);
            if (Global.g4acontrollernotnull)
                Global.realvirtualcontroller.ResetView();
        }
        
        // Note: Removed Unity 2022/6 renderer switching menu items as they are no longer needed

        [MenuItem("Tools/realvirtual/Open demo scene", false, 700)]
        static void OpenDemoScene()
        {
            OpenSceneByPath("DemoRealvirtual.unity");
        }


        [MenuItem("Tools/realvirtual/Demo Scenes Browser", false, 700)]
        static void OpenDemoScenesBrowser()
        {
            DemoScenesWindow.ShowWindow();
        }

        [MenuItem("Tools/realvirtual/Documentation ", false, 701)]
        static void OpenDocumentation()
        {
            Application.OpenURL("https://doc.realvirtual.io");
        }
        
        [MenuItem("Tools/realvirtual/Unity Version Compatibility Info", false, 702)]
        static void ShowUnityCompatibilityInfo()
        {
#if UNITY_6000_0_OR_NEWER
            EditorUtility.DisplayDialog("Unity 6 Compatibility", 
                "You are running Unity 6 - all realvirtual interfaces and features are available.\n\n" +
                "This includes advanced interfaces like Websocket Realtime, Wandelbots NOVA, and all rendering features.",
                "OK");
#else
            EditorUtility.DisplayDialog("Unity 2022 Compatibility", 
                "You are running Unity 2022 - most realvirtual features are available with some limitations.\n\n" +
                "Available interfaces: S7, OPCUA, Modbus, TwinCAT ADS, PLCSIMAdvanced, MQTT, EthernetIP, and more.\n\n" +
                "Unity 6-only interfaces: Websocket Realtime, Wandelbots NOVA (require Unity 6 advanced APIs).\n\n" +
                "Recommendation: Upgrade to Unity 6 for full feature compatibility.",
                "OK");
#endif
        }
        
        static void Info()
        {
            Application.OpenURL("https://realvirtual.io");
        }


        static void AddScript(System.Type type)
        {
            GameObject component = Selection.activeGameObject;

            if (component != null)
            {
                Undo.AddComponent(component, type);
            }
            else
            {
                EditorUtility.DisplayDialog("Please select an Object",
                    "Please select first an Object where the script should be added to!",
                    "OK");
            }
        }

        static void AddScriptByName(string qualifiedTypeName, string missingMessage)
        {
            var type = Type.GetType(qualifiedTypeName);
            if (type == null)
            {
                EditorUtility.DisplayDialog("Info", missingMessage, "OK");
                return;
            }

            AddScript(type);
        }

        static void OpenSceneByPath(string sceneName)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Cannot open scenes during play mode. Please stop playing first.");
                return;
            }

            string scenePath = FindScenePath(sceneName);

            if (!string.IsNullOrEmpty(scenePath))
            {
                EditorSceneManager.OpenScene(scenePath);
            }
            else
            {
                // Show import dialog with callback to open scene after import
                SamplesImportChecker.ShowImportSamplesDialog(false, () =>
                {
                    // Try to find and open the scene after import
                    string importedScenePath = FindScenePath(sceneName);
                    if (!string.IsNullOrEmpty(importedScenePath))
                    {
                        EditorSceneManager.OpenScene(importedScenePath);
                    }
                });
            }
        }

        static string FindScenePath(string sceneName)
        {
            // Try imported samples location.
            // Since v6.3.0 reorganisation scenes are in category subfolders.
            // Search all known category folders + legacy paths for backwards compatibility.
            string samplesBasePath = "Assets/Samples/realvirtual Starter";
            if (AssetDatabase.IsValidFolder(samplesBasePath))
            {
                // Category sub-folder names as defined in package.json (new structure)
                string[] categoryFolders =
                {
                    "Getting Started",
                    "Drives",
                    "Transport Systems",
                    "Object Handling",
                };

                string[] versionFolders = AssetDatabase.GetSubFolders(samplesBasePath);
                foreach (string versionFolder in versionFolders)
                {
                    // New structure: search each category sub-folder
                    foreach (string cat in categoryFolders)
                    {
                        string potentialPath = versionFolder + "/" + cat + "/" + sceneName;
                        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(potentialPath) != null)
                            return potentialPath;
                    }

                    // Legacy fallback: old flat "Demo Scenes" subfolder (pre-reorganisation)
                    string legacyPath = versionFolder + "/Demo Scenes/" + sceneName;
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(legacyPath) != null)
                        return legacyPath;

                    // Fallback: directly in version folder
                    string directPath = versionFolder + "/" + sceneName;
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(directPath) != null)
                        return directPath;
                }
            }
            return null;
        }

        static GameObject AddComponent(string assetpath)
        {
            GameObject component = Selection.activeGameObject;
            Object prefab = AssetDatabase.LoadAssetAtPath(assetpath, typeof(GameObject));
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Warning",
                    $"Prefab not found at path:\n{assetpath}",
                    "OK");
                return null;
            }
            GameObject go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

            if (go != null)
            {
                go.transform.position = new Vector3(0, 0, 0);
                if (component != null)
                {
                    go.transform.parent = component.transform;
                }

                Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            }

            return go;
        }
    }
}
