// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

namespace realvirtual
{
    #region doc
    //! Static registry of all Demo Scene categories and their scenes for the Demo Scenes Browser window.

    //! DemoScenesData is a pure data class — it does not read the file system.
    //! All descriptions are hard-coded so they are available even when a category has not been imported yet.
    //!
    //! Structure:
    //! - Package (Starter / Professional) groups categories.
    //! - DemoCategory holds display metadata and references the exact displayName from package.json.
    //! - FolderName is the Samples~/ sub-folder name used by the /demoscenes skill for validation.
    //! - DemoScene contains the scene file name (without .unity) and a one-line description.
    //!
    //! Maintenance: use /demoscenes CLI skill to add, remove, update or validate entries.
    #endregion
    public static class DemoScenesData
    {
        //! Identifies which UPM package a category belongs to.
        public enum Package { Starter, Professional }

        //! Immutable description of a single demo scene.
        //! SceneName must match the .unity file name exactly (without extension).
        public class DemoScene
        {
            public readonly string SceneName;    //!< Scene file name without .unity extension
            public readonly string Description;  //!< One-line description shown in the browser
            public readonly string DocUrl;       //!< Optional GitBook documentation URL

            public DemoScene(string sceneName, string description, string docUrl = null)
            {
                SceneName = sceneName;
                Description = description;
                DocUrl = docUrl;
            }
        }

        //! Immutable description of a sample category with its scenes list.
        //! SampleDisplayName must match the displayName field in package.json exactly.
        //! FolderName is the Samples~/ sub-folder name (used by DemoScenesContent and the /demoscenes skill).
        public class DemoCategory
        {
            public readonly string DisplayName;        //!< Human-readable name shown in the Demo Scenes window
            public readonly string SampleDisplayName;  //!< Exact match to package.json displayName for Sample.FindByPackage
            public readonly string FolderName;         //!< Sub-folder name inside Samples~/ (e.g. "GettingStarted")
            public readonly string Description;        //!< One-line category summary shown below the category header
            public readonly Package Package;           //!< Starter or Professional
            public readonly string MaterialIcon;       //!< Material Design icon name for the category header
            public readonly DemoScene[] Scenes;        //!< Ordered list of scenes in this category

            public DemoCategory(string displayName, string sampleDisplayName, string folderName,
                string description, Package package_, string materialIcon, DemoScene[] scenes)
            {
                DisplayName = displayName;
                SampleDisplayName = sampleDisplayName;
                FolderName = folderName;
                Description = description;
                Package = package_;
                MaterialIcon = materialIcon;
                Scenes = scenes;
            }
        }

        //! Complete registry of all 48 demo scenes across 8 categories (4 Starter + 4 Professional).
        public static readonly DemoCategory[] Categories = new[]
        {
            // ────────────────────────────────── STARTER ──────────────────────────────────

            new DemoCategory(
                "Getting Started", "Getting Started", "GettingStarted",
                "Core features: conveyors, drives, sensors, MU handling",
                Package.Starter, "play_circle",
                new[]
                {
                    new DemoScene("DemoRealvirtual",
                        "Main introduction with conveyor belt system, drives, sensors, and MU handling",
                        "https://doc.realvirtual.io/getting-started"),
                }),

            new DemoCategory(
                "Drives", "Drives", "Drives",
                "Drive component demos: force drives, raycast limits, conditional drive control",
                Package.Starter, "settings",
                new[]
                {
                    new DemoScene("DemoDriveRaycastLimit",
                        "Drive with raycast-based position limits"),
                    new DemoScene("DemoForceDrive",
                        "Physics-based force drive with configurable force"),
                    new DemoScene("DemoStartDriveOnCondition",
                        "Conditional drive start based on signal logic"),
                }),

            new DemoCategory(
                "Transport Systems", "Transport Systems", "Transport",
                "Conveyors, transport surfaces, chain systems, guided transport (AGV)",
                Package.Starter, "conveyor_belt",
                new[]
                {
                    new DemoScene("DemoChain",
                        "Chain conveyor with linked transport units"),
                    new DemoScene("DemoConveyorRadial",
                        "Radial/curved conveyor belt sections"),
                    new DemoScene("DemoGuidedTransport",
                        "Automated guided vehicle (AGV) path following"),
                    new DemoScene("DemoGuidedTransportLoadUnload",
                        "AGV with loading/unloading stations"),
                    new DemoScene("DemoTransportSurfaceMoving",
                        "Moving transport surface with part routing"),
                    new DemoScene("DemoTransportSurfaceTurningLifting",
                        "Transport surface with turning and lifting capabilities"),
                }),

            new DemoCategory(
                "Object Handling", "Object Handling", "ObjectHandling",
                "Gripping, MU manipulation, cutting, physics",
                Package.Starter, "pan_tool",
                new[]
                {
                    new DemoScene("DemoChangeMU",
                        "Dynamic material unit type changes during simulation"),
                    new DemoScene("DemoCutter",
                        "Cutting operations on transport objects"),
                    new DemoScene("DemoGrippingAdvanced",
                        "Advanced gripping with kinematic hierarchies and multiple grip types"),
                    new DemoScene("DemoGrippingSimple",
                        "Simple pick-and-place demos with SCARA robot"),
                    new DemoScene("DemoPhysics",
                        "Physics-based object interaction and collision"),
                }),

            // ──────────────────────────────── PROFESSIONAL ───────────────────────────────

            new DemoCategory(
                "Robotics", "Robotics", "Robotics",
                "Robot IK: path planning, blending, Autonox, kinematic MU",
                Package.Professional, "precision_manufacturing",
                new[]
                {
                    new DemoScene("AutonoxRobots",
                        "Autonox robot models with kinematic chains"),
                    new DemoScene("AutonoxRobotsDemoAssetManager",
                        "Autonox robots with asset manager integration"),
                    new DemoScene("DemoKinematicMU",
                        "Kinematic MU attachment to robot tools"),
                    new DemoScene("DemoRobotIK",
                        "6-axis robot inverse kinematics with path planning",
                        "https://doc.realvirtual.io/components-and-scripts/robots/ik"),
                    new DemoScene("DemoRobotIKBlending",
                        "Smooth path blending between IK targets"),
                    new DemoScene("DemoStaeubli",
                        "Staeubli TX2-160L robot configuration"),
                }),

            new DemoCategory(
                "Robot Interfaces", "Robot Interfaces", "RobotInterfaces",
                "Robot brand integration: Denso, Fanuc, KUKA, Universal Robots, RoboDK, Wandelbots",
                Package.Professional, "smart_toy",
                new[]
                {
                    new DemoScene("DemoDenso",
                        "Denso robot interface integration"),
                    new DemoScene("DemoFanucRoboGuide",
                        "Fanuc RoboGuide interface and simulation"),
                    new DemoScene("DemoKUKA",
                        "KUKA robot interface configuration"),
                    new DemoScene("DemoRoboDK",
                        "RoboDK software integration for offline programming"),
                    new DemoScene("DemoUniversalRobots",
                        "Universal Robots UR-series interface"),
                    new DemoScene("DemoUR20",
                        "Universal Robots UR20 collaborative robot"),
                    new DemoScene("DemoUR30",
                        "Universal Robots UR30 collaborative robot"),
                    new DemoScene("DemoWandelbotsNOVA",
                        "Wandelbots NOVA AI-based robot programming interface"),
                }),

            new DemoCategory(
                "PLC Interfaces", "PLC Interfaces", "PLCInterfaces",
                "PLC and protocol demos: OPC-UA, TwinCAT, Modbus, EtherNet/IP, MQTT, SIMIT, and more",
                Package.Professional, "cable",
                new[]
                {
                    new DemoScene("DemoBoschRexrothCtrlxInterface",
                        "Bosch Rexroth ctrlX AUTOMATION interface integration"),
                    new DemoScene("DemoBoschRexrothMachiningCell",
                        "Bosch Rexroth machining cell with ctrlX virtual commissioning"),
                    new DemoScene("DemoEthernetIP",
                        "EtherNet/IP industrial protocol integration"),
                    new DemoScene("DemoKeba",
                        "Keba robot controller interface"),
                    new DemoScene("DemoMitsubishi",
                        "Mitsubishi MCP-X protocol interface"),
                    new DemoScene("DemoModbus",
                        "Modbus TCP/RTU protocol communication"),
                    new DemoScene("DemoMQTT",
                        "MQTT IoT messaging protocol integration"),
                    new DemoScene("DemoOPCUA",
                        "OPC-UA standard industrial communication",
                        "https://doc.realvirtual.io/components-and-scripts/interfaces/opc-ua"),
                    new DemoScene("DemoOpenCommissioning",
                        "Open Commissioning standard interface"),
                    new DemoScene("DemoSEWMQTT",
                        "SEW-Eurodrive MQTT simulation interface"),
                    new DemoScene("DemoSiemensSimit",
                        "Siemens SIMIT simulation interface"),
                    new DemoScene("DemoTwinCAT",
                        "Beckhoff TwinCAT ADS protocol integration",
                        "https://doc.realvirtual.io/components-and-scripts/interfaces/twincat"),
                    new DemoScene("DemoUDP",
                        "UDP socket-based data communication"),
                    new DemoScene("DemoWebsocketRealtime",
                        "WebSocket real-time interface for live data streaming"),
                    new DemoScene("DemoWinmodY200",
                        "Winmod Y200 simulation interface"),
                }),

            new DemoCategory(
                "Advanced Features", "Advanced Features", "AdvancedFeatures",
                "Multiplayer, statistics, volume tracking, distance connectors, scene selection",
                Package.Professional, "dashboard",
                new[]
                {
                    new DemoScene("DemoDistanceConnector",
                        "Distance-based conveyor connectors for flexible layouts"),
                    new DemoScene("DemoGroupCollision",
                        "Runtime collision detection between named groups (robot vs. machine door) with GPU highlight and PLC signal",
                        "https://doc.realvirtual.io/components-and-scripts/collision/group-collision-checker"),
                    new DemoScene("DemoMultiplayer",
                        "Multi-user collaborative simulation environment"),
                    new DemoScene("DemoSceneSelection",
                        "Runtime scene selection and management"),
                    new DemoScene("DemoStatistics",
                        "Production statistics and throughput analysis"),
                    new DemoScene("DemoVolumeTracking",
                        "3D volume tracking for material flow visualization"),
                    new DemoScene("ModelZoo",
                        "Gallery of available realvirtual 3D model assets"),
                }),
        };
    }
}
