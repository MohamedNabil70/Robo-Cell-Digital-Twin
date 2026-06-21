// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

#if REALVIRTUAL_MCP
using Newtonsoft.Json.Linq;
using realvirtual;
using realvirtual.MCP;
using UnityEngine;

namespace realvirtual.MCP.Tools
{
    //! MCP tools for querying MU, Grip, GripTarget, and TransportSurface components.
    public static class MUTools
    {
        //! Lists all MU components in the scene with their status
        [McpTool("List all MUs (Moving Units) with status")]
        public static string MUList()
        {
            var mus = Object.FindObjectsByType<MU>(FindObjectsSortMode.None);
            var arr = new JArray();

            foreach (var mu in mus)
            {
                var obj = new JObject
                {
                    ["name"] = mu.gameObject.name,
                    ["path"] = GetGameObjectPath(mu.gameObject),
                    ["id"] = mu.ID,
                    ["parent"] = mu.transform.parent != null ? mu.transform.parent.name : "root",
                    ["fixedBy"] = mu.FixedBy != null ? mu.FixedBy.name : null,
                    ["loadedOn"] = mu.LoadedOn != null ? mu.LoadedOn.name : null,
                    ["velocity"] = mu.Velocity,
                    ["active"] = mu.gameObject.activeInHierarchy,
                    ["debugMode"] = mu.DebugMode
                };

                if (mu.CollidedWithSensors != null && mu.CollidedWithSensors.Count > 0)
                {
                    var sensors = new JArray();
                    foreach (var s in mu.CollidedWithSensors)
                        if (s != null) sensors.Add(s.name);
                    obj["sensors"] = sensors;
                }

                if (mu.LoadedMus != null && mu.LoadedMus.Count > 0)
                {
                    var loaded = new JArray();
                    foreach (var m in mu.LoadedMus)
                        if (m != null) loaded.Add(m.name);
                    obj["loadedMUs"] = loaded;
                }

                var pos = mu.transform.position;
                obj["position"] = new JObject
                {
                    ["x"] = pos.x,
                    ["y"] = pos.y,
                    ["z"] = pos.z
                };

                arr.Add(obj);
            }

            return new JObject
            {
                ["mus"] = arr,
                ["count"] = mus.Length
            }.ToString(Newtonsoft.Json.Formatting.None);
        }

        //! Lists all Grip components in the scene with their status
        [McpTool("List all Grips with status")]
        public static string GripList()
        {
            var grips = Object.FindObjectsByType<Grip>(FindObjectsSortMode.None);
            var arr = new JArray();

            foreach (var grip in grips)
            {
                var obj = new JObject
                {
                    ["name"] = grip.gameObject.name,
                    ["path"] = GetGameObjectPath(grip.gameObject),
                    ["gripRange"] = grip.GripRange,
                    ["pickObjects"] = grip.PickObjects,
                    ["placeObjects"] = grip.PlaceObjects,
                    ["placeMode"] = grip.PlaceMode.ToString(),
                    ["oneBitControl"] = grip.OneBitControl
                };

                if (grip.PickedMUs != null && grip.PickedMUs.Count > 0)
                {
                    var picked = new JArray();
                    foreach (var muObj in grip.PickedMUs)
                        if (muObj != null) picked.Add(muObj.name);
                    obj["pickedMUs"] = picked;
                }
                else
                {
                    obj["pickedMUs"] = new JArray();
                }

                arr.Add(obj);
            }

            return new JObject
            {
                ["grips"] = arr,
                ["count"] = grips.Length
            }.ToString(Newtonsoft.Json.Formatting.None);
        }

        //! Lists all GripTarget components in the scene with their status
        [McpTool("List all GripTargets with occupancy status")]
        public static string GripTargetList()
        {
            var targets = Object.FindObjectsByType<GripTarget>(FindObjectsSortMode.None);
            var arr = new JArray();

            foreach (var target in targets)
            {
                var obj = new JObject
                {
                    ["name"] = target.gameObject.name,
                    ["path"] = GetGameObjectPath(target.gameObject),
                    ["isFree"] = target.IsFree,
                    ["occupiedBy"] = target.OccupiedBy != null ? target.OccupiedBy.name : null,
                    ["alignPosition"] = target.AlignPosition,
                    ["alignRotation"] = target.AlignRotation
                };

                var pos = target.transform.position;
                obj["position"] = new JObject
                {
                    ["x"] = pos.x,
                    ["y"] = pos.y,
                    ["z"] = pos.z
                };

                arr.Add(obj);
            }

            return new JObject
            {
                ["gripTargets"] = arr,
                ["count"] = targets.Length
            }.ToString(Newtonsoft.Json.Formatting.None);
        }

        //! Lists all TransportSurface components in the scene with their status
        [McpTool("List all TransportSurfaces with speed and status")]
        public static string TransportSurfaceList()
        {
            var surfaces = Object.FindObjectsByType<TransportSurface>(FindObjectsSortMode.None);
            var arr = new JArray();

            foreach (var surface in surfaces)
            {
                var obj = new JObject
                {
                    ["name"] = surface.gameObject.name,
                    ["path"] = GetGameObjectPath(surface.gameObject),
                    ["speed"] = surface.speed,
                    ["active"] = surface.gameObject.activeInHierarchy
                };

                if (surface.Drive != null)
                    obj["drive"] = surface.Drive.gameObject.name;

                arr.Add(obj);
            }

            return new JObject
            {
                ["transportSurfaces"] = arr,
                ["count"] = surfaces.Length
            }.ToString(Newtonsoft.Json.Formatting.None);
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
