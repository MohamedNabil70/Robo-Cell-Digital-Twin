// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz


namespace realvirtual
{
    //! Interface for components that need time-synced physics updates with explicit deltaTime for external time synchronization (e.g., Simit)
    public interface ITimeSyncedPhysics
    {
        void CalcFixedUpdate(float deltaTime);
    }
}
