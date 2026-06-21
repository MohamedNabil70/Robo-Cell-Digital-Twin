// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

#if REALVIRTUAL_MCP
using Newtonsoft.Json.Linq;
using realvirtual;
using realvirtual.MCP;
using UnityEngine;

namespace realvirtual.MCP.Tools
{
    //! MCP tools for controlling and querying Drive components.
    //!
    //! Provides commands to list drives, query drive status, move drives to positions,
    //! and control drive motion. These tools enable AI agents to interact with the
    //! realvirtual motion system.
    public static class DriveTools
    {
        //! Lists all Drive components in the scene
        [McpTool("List all drives")]
        public static string DriveList()
        {
            var drives = Object.FindObjectsOfType<Drive>();
            var arr = new JArray();

            foreach (var drive in drives)
            {
                var pos = drive.transform.position;
                arr.Add(new JObject
                {
                    ["name"] = drive.gameObject.name,
                    ["path"] = GetGameObjectPath(drive.gameObject),
                    ["currentPosition"] = drive.CurrentPosition,
                    ["isPosition"] = drive.IsPosition,
                    ["targetPosition"] = drive.TargetPosition,
                    ["currentSpeed"] = drive.CurrentSpeed,
                    ["targetSpeed"] = drive.TargetSpeed,
                    ["direction"] = drive.Direction.ToString(),
                    ["isMoving"] = drive.IsRunning,
                    ["globalPosition"] = new JObject
                    {
                        ["x"] = pos.x,
                        ["y"] = pos.y,
                        ["z"] = pos.z
                    }
                });
            }

            return new JObject
            {
                ["drives"] = arr,
                ["count"] = drives.Length
            }.ToString(Newtonsoft.Json.Formatting.None);
        }

        //! Gets detailed status of a specific drive
        [McpTool("Get drive status and properties")]
        public static string DriveGet(
            [McpParam("Drive name or path")] string name)
        {
            var drive = FindDrive(name);
            if (drive == null)
                return new JObject { ["error"] = $"Drive '{name}' not found" }.ToString(Newtonsoft.Json.Formatting.None);

            var obj = new JObject
            {
                ["name"] = drive.gameObject.name,
                ["path"] = GetGameObjectPath(drive.gameObject),
                ["currentPosition"] = drive.CurrentPosition,
                ["targetPosition"] = drive.TargetPosition,
                ["currentSpeed"] = drive.CurrentSpeed,
                ["targetSpeed"] = drive.TargetSpeed,
                ["direction"] = drive.Direction.ToString(),
                ["isMoving"] = drive.IsRunning,
                ["isAtTarget"] = drive.IsAtTarget
            };

            if (drive.UseLimits)
            {
                obj["lowerLimit"] = drive.LowerLimit;
                obj["upperLimit"] = drive.UpperLimit;
            }

            obj["jogForward"] = drive.JogForward;
            obj["jogBackward"] = drive.JogBackward;

            return obj.ToString(Newtonsoft.Json.Formatting.None);
        }

        //! Moves a drive to a target position
        [McpTool("Move drive to target position")]
        public static string DriveTo(
            [McpParam("Drive name or path")] string name,
            [McpParam("Target position (mm or degrees)")] float position)
        {
            var drive = FindDrive(name);
            if (drive == null)
                return new JObject { ["error"] = $"Drive '{name}' not found" }.ToString(Newtonsoft.Json.Formatting.None);

            drive.TargetPosition = position;
            drive.DriveTo(position);

            return new JObject
            {
                ["status"] = "driving",
                ["drive"] = drive.gameObject.name,
                ["targetPosition"] = position,
                ["currentPosition"] = drive.CurrentPosition
            }.ToString(Newtonsoft.Json.Formatting.None);
        }

        //! Starts jogging a drive forward
        [McpTool("Jog drive forward")]
        public static string DriveJogForward(
            [McpParam("Drive name or path")] string name)
        {
            var drive = FindDrive(name);
            if (drive == null)
                return new JObject { ["error"] = $"Drive '{name}' not found" }.ToString(Newtonsoft.Json.Formatting.None);

            drive.JogForward = true;
            drive.JogBackward = false;

            return new JObject
            {
                ["status"] = "jogging_forward",
                ["drive"] = drive.gameObject.name,
                ["currentPosition"] = drive.CurrentPosition
            }.ToString(Newtonsoft.Json.Formatting.None);
        }

        //! Starts jogging a drive backward
        [McpTool("Jog drive backward")]
        public static string DriveJogBackward(
            [McpParam("Drive name or path")] string name)
        {
            var drive = FindDrive(name);
            if (drive == null)
                return new JObject { ["error"] = $"Drive '{name}' not found" }.ToString(Newtonsoft.Json.Formatting.None);

            drive.JogForward = false;
            drive.JogBackward = true;

            return new JObject
            {
                ["status"] = "jogging_backward",
                ["drive"] = drive.gameObject.name,
                ["currentPosition"] = drive.CurrentPosition
            }.ToString(Newtonsoft.Json.Formatting.None);
        }

        //! Stops a drive (stops jogging)
        [McpTool("Stop drive motion")]
        public static string DriveStop(
            [McpParam("Drive name or path")] string name)
        {
            var drive = FindDrive(name);
            if (drive == null)
                return new JObject { ["error"] = $"Drive '{name}' not found" }.ToString(Newtonsoft.Json.Formatting.None);

            drive.JogForward = false;
            drive.JogBackward = false;

            return new JObject
            {
                ["status"] = "stopped",
                ["drive"] = drive.gameObject.name,
                ["currentPosition"] = drive.CurrentPosition
            }.ToString(Newtonsoft.Json.Formatting.None);
        }

        //! Sets the target speed of a drive
        [McpTool("Set drive target speed")]
        public static string DriveSetSpeed(
            [McpParam("Drive name or path")] string name,
            [McpParam("Speed (mm/s or deg/s)")] float speed)
        {
            var drive = FindDrive(name);
            if (drive == null)
                return new JObject { ["error"] = $"Drive '{name}' not found" }.ToString(Newtonsoft.Json.Formatting.None);

            if (speed < 0)
                return new JObject { ["error"] = "Speed must be positive" }.ToString(Newtonsoft.Json.Formatting.None);

            drive.TargetSpeed = speed;

            return new JObject
            {
                ["status"] = "ok",
                ["drive"] = drive.gameObject.name,
                ["targetSpeed"] = speed
            }.ToString(Newtonsoft.Json.Formatting.None);
        }

        //! Helper to find a drive by name or path
        private static Drive FindDrive(string nameOrPath)
        {
            var go = GameObject.Find(nameOrPath);
            if (go != null)
            {
                var drive = go.GetComponent<Drive>();
                if (drive != null)
                    return drive;
            }

            var drives = Object.FindObjectsOfType<Drive>();
            foreach (var drive in drives)
            {
                if (drive.gameObject.name == nameOrPath)
                    return drive;
                if (GetGameObjectPath(drive.gameObject) == nameOrPath)
                    return drive;
            }

            return null;
        }

        //! Gets the full hierarchy path of a GameObject
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
