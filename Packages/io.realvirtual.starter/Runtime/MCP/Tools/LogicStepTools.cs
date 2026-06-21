// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

#if REALVIRTUAL_MCP
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using realvirtual;
using realvirtual.MCP;
using UnityEngine;

namespace realvirtual.MCP.Tools
{
    //! MCP tool for introspecting LogicStep automation flows.
    //!
    //! Returns the full LogicStep hierarchy as structured JSON, showing container structure,
    //! step types, signal/sensor references with current values, and active step state.
    //! Enables instant debugging of automation flows without multiple component_get calls.
    public static class LogicStepTools
    {
        //! Returns the full LogicStep flow as structured JSON
        [McpTool("Get LogicStep flow hierarchy with step types, signal/sensor references, current values, and active step state")]
        public static string LogicStepGetFlow(
            [McpParam("GameObject name or path (empty = find all top-level containers)")] string name = "",
            [McpParam("Max recursion depth for nested containers")] int depth = 10)
        {
            if (string.IsNullOrEmpty(name))
                return GetAllFlows(depth);

            var go = GameObject.Find(name);
            if (go == null)
                return new JObject { ["error"] = $"GameObject '{name}' not found" }
                    .ToString(Newtonsoft.Json.Formatting.None);

            var flows = new JArray();
            // Check if the root itself has a LogicStep
            var rootStep = go.GetComponent<LogicStep>();
            if (rootStep != null)
            {
                flows.Add(BuildStepNode(rootStep, 0, depth));
            }
            else
            {
                // Walk direct children for LogicSteps
                foreach (Transform child in go.transform)
                {
                    var step = child.GetComponent<LogicStep>();
                    if (step != null)
                        flows.Add(BuildStepNode(step, 0, depth));
                }
            }

            return new JObject
            {
                ["status"] = "ok",
                ["root"] = GetGameObjectPath(go),
                ["flows"] = flows,
                ["count"] = flows.Count
            }.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static string GetAllFlows(int depth)
        {
            var allContainers = new List<LogicStep>();

            // Find all SerialContainers
            foreach (var sc in Object.FindObjectsByType<LogicStep_SerialContainer>(FindObjectsSortMode.None))
                allContainers.Add(sc);

            // Find all ParallelContainers
            foreach (var pc in Object.FindObjectsByType<LogicStep_ParallelContainer>(FindObjectsSortMode.None))
                allContainers.Add(pc);

            // Filter to top-level only: parent has no LogicStep component
            var topLevel = new List<LogicStep>();
            foreach (var container in allContainers)
            {
                var parent = container.transform.parent;
                if (parent == null || parent.GetComponent<LogicStep>() == null)
                    topLevel.Add(container);
            }

            var flows = new JArray();
            foreach (var container in topLevel)
                flows.Add(BuildStepNode(container, 0, depth));

            return new JObject
            {
                ["status"] = "ok",
                ["flows"] = flows,
                ["count"] = flows.Count
            }.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static JObject BuildStepNode(LogicStep step, int currentDepth, int maxDepth)
        {
            var go = step.gameObject;
            var typeName = step.GetType().Name.Replace("LogicStep_", "");

            var node = new JObject
            {
                ["name"] = go.name,
                ["path"] = GetGameObjectPath(go),
                ["type"] = typeName,
                ["stepActive"] = step.StepActive,
                ["isWaiting"] = step.IsWaiting,
                ["state"] = step.State
            };

            // Container-specific fields
            if (step is LogicStep_SerialContainer serial)
            {
                node["activeStep"] = serial.ActiveLogicStep;
                node["totalSteps"] = serial.NumberLogicSteps;
                node["completedCycles"] = serial.CompletedCycles;
                if (serial.CompletedCycles > 0)
                {
                    node["minCycleTime"] = System.Math.Round(serial.MinCycleTime, 2);
                    node["maxCycleTime"] = System.Math.Round(serial.MaxCycleTime, 2);
                    node["medianCycleTime"] = System.Math.Round(serial.MedianCycleTime, 2);
                }

                if (currentDepth < maxDepth)
                    node["children"] = BuildChildren(go.transform, currentDepth, maxDepth);
            }
            else if (step is LogicStep_ParallelContainer)
            {
                if (currentDepth < maxDepth)
                    node["children"] = BuildChildren(go.transform, currentDepth, maxDepth);
            }
            else
            {
                // Leaf step - extract type-specific params
                node["params"] = ExtractParams(step);
            }

            return node;
        }

        private static JArray BuildChildren(Transform parent, int currentDepth, int maxDepth)
        {
            var children = new JArray();
            int index = 0;
            foreach (Transform child in parent)
            {
                var step = child.GetComponent<LogicStep>();
                if (step != null)
                {
                    var childNode = BuildStepNode(step, currentDepth + 1, maxDepth);
                    childNode["index"] = index;
                    children.Add(childNode);
                    index++;
                }
            }
            return children;
        }

        private static JObject ExtractParams(LogicStep step)
        {
            var p = new JObject();

            switch (step)
            {
                case LogicStep_SetSignalBool ssb:
                    p["signal"] = GetRefPath(ssb.Signal);
                    p["signalValue"] = GetSignalValue(ssb.Signal);
                    p["setToTrue"] = ssb.SetToTrue;
                    break;

                case LogicStep_WaitForSignalBool wsb:
                    p["signal"] = GetRefPath(wsb.Signal);
                    p["signalValue"] = GetSignalValue(wsb.Signal);
                    p["waitForTrue"] = wsb.WaitForTrue;
                    break;

                case LogicStep_SetSignalFloat ssf:
                    p["signal"] = GetRefPath(ssf.Signal);
                    p["signalValue"] = GetSignalValue(ssf.Signal);
                    p["value"] = ssf.Value;
                    break;

                case LogicStep_WaitForSignalFloat wsf:
                    p["signal"] = GetRefPath(wsf.Signal);
                    p["signalValue"] = GetSignalValue(wsf.Signal);
                    p["comparison"] = wsf.Comparison.ToString();
                    p["value"] = wsf.Value;
                    p["tolerance"] = wsf.Tolerance;
                    break;

                case LogicStep_WaitForSensor ws:
                    p["sensor"] = GetRefPath(ws.Sensor);
                    p["sensorOccupied"] = ws.Sensor != null && ws.Sensor.Occupied;
                    p["waitForOccupied"] = ws.WaitForOccupied;
                    break;

                case LogicStep_Delay d:
                    p["duration"] = d.Duration;
                    break;

                case LogicStep_DriveTo dt:
                    p["drive"] = GetRefPath(dt.drive);
                    p["destination"] = dt.Destination;
                    p["relative"] = dt.Relative;
                    p["direction"] = dt.Direction.ToString();
                    if (dt.drive != null)
                        p["drivePosition"] = System.Math.Round(dt.drive.CurrentPosition, 2);
                    break;

                case LogicStep_StartDriveTo sdt:
                    p["drive"] = GetRefPath(sdt.drive);
                    p["destination"] = sdt.Destination;
                    p["relative"] = sdt.Relative;
                    p["direction"] = sdt.Direction.ToString();
                    break;

                case LogicStep_StartDriveSpeed sds:
                    p["drive"] = GetRefPath(sds.drive);
                    p["speed"] = sds.Speed;
                    break;

                case LogicStep_WaitForDrivesAtTarget wdt:
                    var drives = new JArray();
                    if (wdt.Drives != null)
                    {
                        foreach (var drv in wdt.Drives)
                        {
                            if (drv != null)
                                drives.Add(new JObject
                                {
                                    ["path"] = GetGameObjectPath(drv.gameObject),
                                    ["isAtTarget"] = drv.IsAtTarget
                                });
                        }
                    }
                    p["drives"] = drives;
                    break;

                case LogicStep_Enable en:
                    p["gameObject"] = en.Gameobject != null ? GetGameObjectPath(en.Gameobject) : null;
                    p["enable"] = en.Enable;
                    break;

                case LogicStep_GripPick gp:
                    p["grip"] = GetRefPath(gp.Grip);
                    p["blocking"] = gp.Blocking;
                    break;

                case LogicStep_GripPlace gpl:
                    p["grip"] = GetRefPath(gpl.Grip);
                    p["blocking"] = gpl.Blocking;
                    break;

                case LogicStep_JumpOnSignal js:
                    p["jumpToStep"] = js.JumpToStep;
                    p["signal"] = GetRefPath(js.Signal);
                    p["signalValue"] = GetSignalValue(js.Signal);
                    p["jumpOn"] = js.JumpOn;
                    break;

                case LogicStep_SetActiveOnly sao:
                    var behaviors = new JArray();
                    if (sao.Behaviors != null)
                    {
                        foreach (var b in sao.Behaviors)
                        {
                            if (b != null)
                                behaviors.Add(GetGameObjectPath(b.gameObject));
                        }
                    }
                    p["behaviors"] = behaviors;
                    p["setToAlways"] = sao.SetToAlways;
                    break;
            }

            return p;
        }

        private static string GetRefPath(Component comp)
        {
            if (comp == null) return null;
            return GetGameObjectPath(comp.gameObject);
        }

        private static JToken GetSignalValue(Signal signal)
        {
            if (signal == null) return JValue.CreateNull();
            try
            {
                var val = signal.GetValue();
                if (val is bool b) return b;
                if (val is float f) return f;
                if (val is int i) return i;
                if (val is double d) return d;
                return val?.ToString();
            }
            catch
            {
                return JValue.CreateNull();
            }
        }

        private static string GetGameObjectPath(GameObject obj)
        {
            var path = obj.name;
            var parent = obj.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}
#endif
