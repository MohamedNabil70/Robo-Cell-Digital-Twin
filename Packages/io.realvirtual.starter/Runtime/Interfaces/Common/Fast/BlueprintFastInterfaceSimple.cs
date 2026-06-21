// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System.Threading;
using System.Threading.Tasks;

namespace realvirtual
{
    //! Minimal template for creating custom fast interfaces
    //! Copy this file and replace the connection and communication logic with your protocol
    public class BlueprintFastInterfaceSimple : FastInterfaceBase
    {
        //! Establish connection to external system (runs on background thread)
        protected override async Task EstablishConnection(CancellationToken cancellationToken)
        {
            // TODO: Connect to your system here
            // Example: await tcpClient.ConnectAsync(ip, port);
            await Task.Delay(100, cancellationToken); // Simulated connection
        }
        
        //! Main communication loop (runs repeatedly on background thread)
        protected override async Task CommunicationLoop(CancellationToken cancellationToken)
        {
            // Read input signals to send to external system
            var inputs = GetInputsForPLC();
            
            // TODO: Send inputs to your external system
            // Example: await stream.WriteAsync(SerializeData(inputs));
            
            // TODO: Receive data from external system
            // Example: var data = await stream.ReadAsync(buffer);
            
            // Write received data to output signals
            // SetOutputsFromPLC(receivedData);
            
            await Task.Delay(10, cancellationToken); // Simulated work
        }
        
        //! Clean up connection (runs on background thread)
        protected override void CloseConnection()
        {
            // TODO: Close your connection here
            // Example: tcpClient?.Close();
        }
    }
}