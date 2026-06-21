// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;

namespace realvirtual
{
    //! Test component to verify PrePost FixedUpdate event system functionality
    public class TestFixedUpdateEvents : MonoBehaviour, IPreFixedUpdate, IPostFixedUpdate
    {
        [Header("Test Settings")]
        public bool EnableLogging = true; //!< Enable logging for testing PrePost FixedUpdate events

        private int _preCallCount = 0;
        private int _postCallCount = 0;
        private int _fixedUpdateCallCount = 0;

        //! Called before Unity's FixedUpdate
        //! IMPLEMENTS IPreFixedUpdate::PreFixedUpdate
        public void PreFixedUpdate()
        {
            _preCallCount++;
            if (EnableLogging && _preCallCount % 60 == 0) // Log every 60 calls (~1 second at 60Hz)
            {
                Logger.Message($"PreFixedUpdate called {_preCallCount} times", this);
            }
        }

        //! Called after Unity's FixedUpdate
        //! IMPLEMENTS IPostFixedUpdate::PostFixedUpdate
        public void PostFixedUpdate()
        {
            _postCallCount++;
            if (EnableLogging && _postCallCount % 60 == 0) // Log every 60 calls (~1 second at 60Hz)
            {
                Logger.Message($"PostFixedUpdate called {_postCallCount} times", this);
            }
        }

        void FixedUpdate()
        {
            _fixedUpdateCallCount++;
            if (EnableLogging && _fixedUpdateCallCount % 60 == 0) // Log every 60 calls (~1 second at 60Hz)
            {
                Logger.Message($"FixedUpdate called {_fixedUpdateCallCount} times", this);
            }
        }

        void Start()
        {
            if (EnableLogging)
            {
                Logger.Message("TestFixedUpdateEvents component started - monitoring PrePost FixedUpdate events", this);
            }
        }

        [ContextMenu("Show Call Statistics")]
        public void ShowCallStatistics()
        {
            Logger.Message($"Call Statistics - Pre: {_preCallCount}, FixedUpdate: {_fixedUpdateCallCount}, Post: {_postCallCount}", this);
        }

        [ContextMenu("Reset Statistics")]
        public void ResetStatistics()
        {
            _preCallCount = 0;
            _postCallCount = 0;
            _fixedUpdateCallCount = 0;
            Logger.Message("Call statistics reset", this);
        }
    }
}