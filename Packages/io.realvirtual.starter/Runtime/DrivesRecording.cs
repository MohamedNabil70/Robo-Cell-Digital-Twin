using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace realvirtual
{
    public class RecordingHandle
    {
        public DrivesRecorder DrivesRecorder;
        public int Frame = 0;
        public int Currentindex;
        public Hashtable iddrive;
        public Hashtable driveid;
    }

    [CreateAssetMenu(fileName = "DrivesRecording", menuName = "realvirtual/Add DrivesRecording", order = 1)]
    public class DrivesRecording : ScriptableObject
    {
        public bool Protected = false;

        [System.Serializable]
        public class Snapshot
        {
            public int Frame;
            public int DriveID;
            public float Position;
        }
        
        [System.Serializable]
        public class RecordingSequence
        {
            public string Name;
            public int StartFrame;
            public int EndFrame;
        }

        [System.Serializable]
        public class RecordedDrive
        {
            public string Path;
            public int Id;
        }

        public class CompareSnapshot : IComparer<Snapshot>
        {
            public int Compare(Snapshot x, Snapshot y)
            {
                return x.Frame - y.Frame;
            }
        }

        public List<RecordingSequence> Sequences;
        public List<int> IgnoredDriveIds;
        [ReadOnly] public List<RecordedDrive> RecordedDrives;
        [ReadOnly] public int NumberFrames;
         private List<Drive> Drives;


  
        [HideInInspector] public List<Snapshot> Snapshots;

        private string GetPath(Drive drive)
        {
            string path = "/" + drive.name;
            var obj = drive.gameObject;

            // If there are multiple Drive components on the same GameObject, append component index
            var drives = obj.GetComponents<Drive>();
            if (drives.Length > 1)
            {
                var index = System.Array.IndexOf(drives, drive);
                path += $"[{index}]";
            }

            while (obj.transform.parent != null)
            {
                obj = obj.transform.parent.gameObject;
                if (obj.name.Contains("/"))
                {
                    Logger.Error($"Drive {drive.name} has a parent {obj.name} with a / in the name. This is not allowed", this);
                }
                path = "/" + obj.name + path;
            }

            return path;
        }

        public RecordingHandle NewRecording(DrivesRecorder drivesRecorder)
        {
            var handle = new RecordingHandle();
            handle.iddrive = new Hashtable();
            handle.driveid = new Hashtable();
            Snapshots = new List<Snapshot>();
            handle.Frame = 0;
            Drive[] drives;
            if (!drivesRecorder.RecordAllDrivesWithinScene)
                drives = drivesRecorder.GetComponentsInChildren<Drive>();
            else
                drives = FindObjectsByType<Drive>(FindObjectsSortMode.None);
            
            RecordedDrives = new List<RecordedDrive>();
            var id = 0;
            string path;
            foreach (var drive in drives)
            {
                path = GetPath(drive);
                var recordeddrive = new RecordedDrive();
                recordeddrive.Id = id;
                recordeddrive.Path = path;
                RecordedDrives.Add(recordeddrive);
                handle.iddrive.Add(id, drive);
                handle.driveid.Add(drive, id);
                id++;
                drive.PositionOverwrite = false;
            }

            Drives = new List<Drive>();
            Drives.AddRange(drives);

            return handle;
        }

        public void CreateHashtables(RecordingHandle handle)
        {
            handle.iddrive = new Hashtable();
            handle.driveid = new Hashtable();

            foreach (var recordedDrive in RecordedDrives)
            {
                var thispath = recordedDrive.Path;

                // Extract component index if present (format: /path/to/Drive[0])
                int componentIndex = 0;
                var indexMatch = System.Text.RegularExpressions.Regex.Match(thispath, @"\[(\d+)\]$");
                if (indexMatch.Success)
                {
                    componentIndex = int.Parse(indexMatch.Groups[1].Value);
                    thispath = thispath.Substring(0, indexMatch.Index); // Remove [index] from path
                }

                // remove first / in path
                if (thispath.StartsWith("/"))
                    thispath = thispath.Substring(1);

                var go = GameObject.Find(thispath);

                if (go != null)
                {
                    Drive drive = null;
                    var drives = go.GetComponents<Drive>();

                    // Get the specific component by index if multiple exist
                    if (drives.Length > componentIndex)
                    {
                        drive = drives[componentIndex];
                    }
                    else if (drives.Length > 0)
                    {
                        // Fallback to first component if index is invalid
                        Logger.Warning($"Component index {componentIndex} out of range for path [{recordedDrive.Path}]. Using first Drive component.", this);
                        drive = drives[0];
                    }

                    if (drive != null)
                    {
                        // Check if ID already exists to prevent duplicate key errors
                        if (handle.iddrive.ContainsKey(recordedDrive.Id))
                        {
                            Logger.Warning($"Duplicate Drive ID {recordedDrive.Id} found for path [{recordedDrive.Path}]. Skipping duplicate entry.", this);
                            continue;
                        }

                        // Check if drive already tracked to prevent duplicate component references
                        if (handle.driveid.ContainsKey(drive))
                        {
                            Logger.Warning($"Drive at path [{recordedDrive.Path}] is already tracked with ID {handle.driveid[drive]}. Skipping duplicate reference.", this);
                            continue;
                        }

                        handle.iddrive.Add(recordedDrive.Id, drive);
                        handle.driveid.Add(drive, recordedDrive.Id);
                        drive.PositionOverwrite = true;
                    }
                    else
                    {
                        Logger.Error($"realvirtual Recording, GameObject found but Drive component missing at [{recordedDrive.Path}] for DrivesRecorder [{handle.DrivesRecorder.name}]", this);
                    }
                }
                else
                {
                    Logger.Warning($"realvirtual Recording, Drive GameObject could not be found at path [{recordedDrive.Path}] for DrivesRecorder [{handle.DrivesRecorder.name}]", this);
                }
            }
        }

        public void RecordFrame(RecordingHandle handle)
        {
            handle.Frame++;
            NumberFrames = handle.Frame;
            foreach (var drive in Drives)
            {
                var snap = new Snapshot();
                snap.DriveID = (int) handle.driveid[drive];
                snap.Frame = handle.Frame;
                snap.Position = drive.CurrentPosition;
                Snapshots.Add(snap);
            }

            handle.Currentindex = Snapshots.Count;
        }

        private RecordingHandle CreateHandle(DrivesRecorder drivesRecorder)
        {
            var handle = new RecordingHandle();
            handle.Currentindex = 0;
            handle.Frame = 0;
            handle.DrivesRecorder = drivesRecorder;
            CreateHashtables(handle);
            return handle;
        }


        public int GetSequenceStart(string Sequence)
        {
            foreach (var sqeuence in Sequences)
            {
                if (sqeuence.Name == Sequence)
                    return sqeuence.StartFrame;
            }
            return 0;
        }
        
        public int GetSequenceEnd(string Sequence)
        {
            foreach (var sqeuence in Sequences)
            {
                if (sqeuence.Name == Sequence)
                    return sqeuence.EndFrame;
            }
            return 0;
        }
        
        public RecordingHandle MoveToFrame(DrivesRecorder drivesRecorder, int frame)
        {
            var sorter = new CompareSnapshot();
            var snap = new Snapshot();
            var handle = CreateHandle(drivesRecorder);
            handle.Currentindex = 0;
            handle.Frame = 0;
            snap.Frame = frame + 1;
            var res = Snapshots.BinarySearch(snap, sorter);
            if (res > 0)
            {
                while (res > 0 && Snapshots[res].Frame == snap.Frame)
                {
                    res--;
                }

                handle.Frame = frame + 1;
                handle.Currentindex = res;
            }

            var tframe = handle.Frame;
            var tindex = handle.Currentindex;
            PlayNextFrame(handle);
            handle.Frame = tframe;
            handle.Currentindex = tindex;
            return handle;
        }


        public RecordingHandle MoveToFrame(DrivesRecorder drivesRecorder, float pos)
        {
            var abspos = (int) (NumberFrames / 100 * pos);
            return MoveToFrame(drivesRecorder, abspos);
        }

        public RecordingHandle StartReplay(DrivesRecorder drivesRecorder)
        {
            var handle = CreateHandle(drivesRecorder);
            return handle;
        }


        public bool PlayNextFrame(RecordingHandle handle)
        {
            var notatend = true;
            var loop = true;
            while (loop && Snapshots[handle.Currentindex].Frame <= handle.Frame)
            {
                var snap = Snapshots[handle.Currentindex];
   
                if (!IgnoredDriveIds.Contains(snap.DriveID))
                {
                    var drive = (Drive) handle.iddrive[snap.DriveID];
                    drive.PositionOverwriteValue = snap.Position;
                }
                handle.Currentindex++;
                if (handle.Currentindex >= Snapshots.Count - 2)
                    loop = false;
            }

            handle.Frame++;

            if (handle.Currentindex > Snapshots.Count - 1)
                notatend = false;

            return notatend;
        }
    }
}