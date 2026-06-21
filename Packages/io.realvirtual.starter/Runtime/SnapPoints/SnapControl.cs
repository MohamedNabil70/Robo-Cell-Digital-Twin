using System;
using UnityEngine;

namespace realvirtual
{
    //! Provides snap point functionality through composition pattern.
    //!
    //! This component implements ISnapable and handles all snap point logic automatically.
    //! Components that need snap functionality can simply add [RequireComponent(typeof(SnapControl))]
    //! instead of implementing ISnapable themselves. This eliminates boilerplate code and ensures
    //! consistent snap behavior across all components.
    //!
    //! Key Features:
    //! - Automatic snap point management for child SnapPoint components
    //! - Movement detection triggers snap checks automatically
    //! - Optional X-axis alignment via AlignXOnSnap property
    //! - Unity events for snap/unsnap notifications
    //! - Works in both edit mode and play mode
    //!
    //! Usage Example:
    //! <code>
    //! [RequireComponent(typeof(SnapControl))]
    //! public class MyComponent : MonoBehaviour
    //! {
    //!     // No ISnapable implementation needed - SnapControl handles it
    //! }
    //! </code>
    //!
    //! For detailed documentation see: https://doc.realvirtual.io/components-and-scripts/snap-points
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/snap-points")]
    [SelectionBase]
    [ExecuteInEditMode]
    public class SnapControl : MonoBehaviour,ISnapable
    {
        
        public bool AlignXOnSnap = true; //!< Align the object x-axis to the snap point
        
        
        // Unity Event for onsnapped giving the own isnappable and the snapped isnappable
        [Serializable]
        public class OnIsSnappedEvent : UnityEngine.Events.UnityEvent<ISnapable, ISnapable> { }

        [SerializeField]
        [Tooltip("Event triggered when the object is snapped to another object")]
        public OnIsSnappedEvent OnIsSnapped;
            
        // Unity Event for unsnapped giving the own ISnapable and the unsnapped ISnapable
        [Serializable]
        public class OnIsUnsnappedEvent : UnityEngine.Events.UnityEvent<ISnapable, ISnapable> { }

        [SerializeField]
        [Tooltip("Event triggered when the object is unsnapped from another object")]
        public OnIsUnsnappedEvent OnIsUnsnapped;
        
        
        public OnSnappedEvent OnSnapped { get; set; }
        
        private Vector3 lastpos;
        private Quaternion lastrot;
        
        private const float PositionThreshold = 0.01f; // Adjust as needed
        private const float RotationThreshold = 0.1f;  // Adjust as needed
        
        //! Checks all child snap points for connections.
        //! IMPLEMENTS ISnapable::CheckSnap
        public void CheckSnap()
        {
            SnapPointHelper.CheckChildSnapPoints(this);
        }

        private void Awake()
        {
            lastpos = transform.position;
            lastrot = transform.rotation;
        }

        public void Update()
        {
            if (Vector3.Distance(lastpos, transform.position) > PositionThreshold || 
        Quaternion.Angle(lastrot, transform.rotation) > RotationThreshold)
    {
                var snappoints = GetComponentsInChildren<SnapPoint>(true);
                foreach (var snappoint in snappoints)
                    if (snappoint != null)
                        snappoint.CheckSnap();
            }
            lastpos = transform.position;
            lastrot = transform.rotation;
        }

        public void Connect(SnapPoint ownSnapPoint, SnapPoint snapPointMate, ISnapable mateObject, bool ismoved)
        {
            if (ismoved)
            {
                Align(ownSnapPoint, snapPointMate, Quaternion.Euler(0, 0, 0));
            }
            if (OnSnapped != null)
            {
                OnSnapped.Invoke(ownSnapPoint, snapPointMate);
            }
            
            // call the event
            if (OnIsSnapped != null)
            {
                OnIsSnapped.Invoke(this, mateObject);
            }
        }
        
        public void Align(SnapPoint ownsnappoint, SnapPoint matesnappoint, Quaternion additonalrotation)
        {
            var oldpos = transform.position;
            var oldrot = transform.rotation;
            transform.Translate(matesnappoint.transform.position - ownsnappoint.transform.position, Space.World);
            
            if (AlignXOnSnap)
            {
                var qdelta = Quaternion.FromToRotation(transform.right, matesnappoint.transform.right);
                transform.rotation = qdelta * transform.rotation;
            }
            transform.rotation = additonalrotation * transform.rotation;
            transform.Translate(matesnappoint.transform.position - ownsnappoint.transform.position, Space.World);
        }

        public void Disconnect(SnapPoint snapPoint, SnapPoint snapPointMate, ISnapable Mateobj, bool ismoved)
        {
            // call the event
            if (OnIsUnsnapped != null)
            {
                OnIsUnsnapped.Invoke(this, Mateobj);
            }
        }

        //! Called when properties are modified in the editor.
        //! IMPLEMENTS ISnapable::Modify
        public void Modify()
        {
            SnapPointHelper.CheckChildSnapPoints(this);
        }

        //! Attaches this object to a snap point.
        //! IMPLEMENTS ISnapable::AttachTo
        public void AttachTo(SnapPoint attachto)
        {
            SnapPointHelper.AttachTo(transform, attachto);
        }
    }
}
