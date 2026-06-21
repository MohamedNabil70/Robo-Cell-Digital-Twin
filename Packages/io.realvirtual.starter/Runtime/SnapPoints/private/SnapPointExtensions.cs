// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license

using System.Collections.Generic;
using System.Linq;

namespace realvirtual
{
    //! Extension methods for SnapPoint to simplify querying connected components.
    //!
    //! These extension methods provide a fluent, LINQ-friendly API for working with
    //! snap point connections. They handle both single mate and multiple mates scenarios
    //! transparently, making it easy to query connected components.
    //!
    //! Key Features:
    //! - Query connected components by type with GetConnectedComponents<T>()
    //! - Check for any connections with HasConnections()
    //! - Get first connected component with GetFirstConnected<T>()
    //! - LINQ-compatible IEnumerable results
    //! - Null-safe implementations
    //!
    //! Usage Example:
    //! <code>
    //! // Check if any connected conveyor is occupied
    //! bool anyOccupied = snapOut.GetConnectedComponents<IPalletHandling>()
    //!                           .Any(h => h.GetOccupied());
    //!
    //! // Get all connected conveyors
    //! var conveyors = snapOut.GetConnectedComponents<PalletConveyor>().ToList();
    //!
    //! // Quick connection check
    //! if (snapIn.HasConnections())
    //! {
    //!     // Has predecessors
    //! }
    //! </code>
    public static class SnapPointExtensions
    {
        //! Gets all connected components of the specified type from a snap point.
        //!
        //! This method traverses both single mate and multiple mates connections,
        //! searching for components of type T in the parent hierarchy of each connected snap point.
        //! Returns an IEnumerable for LINQ compatibility.
        //!
        //! <typeparam name="T">The component type to search for (e.g., IPalletHandling, Drive, Sensor)</typeparam>
        //! <param name="snapPoint">The snap point to query for connections</param>
        //! <returns>An enumerable of all connected components of type T (may be empty)</returns>
        //!
        //! Example:
        //! <code>
        //! // Get all connected pallet handlers
        //! var handlers = mySnapOut.GetConnectedComponents<IPalletHandling>();
        //!
        //! // Check if any are occupied
        //! bool anyOccupied = handlers.Any(h => h.GetOccupied());
        //!
        //! // Count connections
        //! int count = handlers.Count();
        //!
        //! // Process each one
        //! foreach (var handler in handlers)
        //! {
        //!     handler.UpdatePalletHandling();
        //! }
        //! </code>
        public static IEnumerable<T> GetConnectedComponents<T>(this SnapPoint snapPoint) where T : class
        {
            if (snapPoint == null)
                yield break;

            // Check single mate connection
            if (snapPoint.mate != null)
            {
                var component = snapPoint.mate.GetComponentInParent<T>();
                if (component != null)
                    yield return component;
            }

            // Check multiple mates (multi-snap mode)
            if (snapPoint.mates != null && snapPoint.mates.Count > 0)
            {
                foreach (var mate in snapPoint.mates)
                {
                    if (mate != null)
                    {
                        var component = mate.GetComponentInParent<T>();
                        if (component != null)
                            yield return component;
                    }
                }
            }
        }

        //! Gets the first connected component of the specified type, or null if none found.
        //!
        //! Convenience method for when you only need the first connected component
        //! or know there should only be one connection.
        //!
        //! <typeparam name="T">The component type to search for</typeparam>
        //! <param name="snapPoint">The snap point to query for connections</param>
        //! <returns>The first connected component of type T, or null if none found</returns>
        //!
        //! Example:
        //! <code>
        //! // Get first connected handler
        //! var successor = mySnapOut.GetFirstConnected<IPalletHandling>();
        //! if (successor != null && successor.GetOccupied())
        //! {
        //!     // Stop drive
        //! }
        //! </code>
        public static T GetFirstConnected<T>(this SnapPoint snapPoint) where T : class
        {
            return snapPoint.GetConnectedComponents<T>().FirstOrDefault();
        }

        //! Checks if the snap point has any connections (mate or mates).
        //!
        //! Quick boolean check for whether a snap point is connected to anything.
        //! More efficient than checking GetConnectedComponents().Any() since it doesn't
        //! need to traverse the component hierarchy.
        //!
        //! <param name="snapPoint">The snap point to check</param>
        //! <returns>True if the snap point has at least one connection, false otherwise</returns>
        //!
        //! Example:
        //! <code>
        //! if (snapOut.HasConnections())
        //! {
        //!     Logger.Message("This conveyor has successors");
        //! }
        //!
        //! // Use in initialization logic
        //! bool hasSuccessors = SnapOut?.HasConnections() ?? false;
        //! if (!hasSignals && !hasSuccessors)
        //! {
        //!     // Autonomous mode
        //! }
        //! </code>
        public static bool HasConnections(this SnapPoint snapPoint)
        {
            if (snapPoint == null)
                return false;

            // Check if single mate exists
            if (snapPoint.mate != null)
                return true;

            // Check if multiple mates exist
            if (snapPoint.mates != null && snapPoint.mates.Count > 0)
                return true;

            return false;
        }

        //! Checks if any connected component of type T satisfies a condition.
        //!
        //! Convenience method combining GetConnectedComponents with a predicate check.
        //! More efficient than ToList().Any() since it uses deferred execution.
        //!
        //! <typeparam name="T">The component type to search for</typeparam>
        //! <param name="snapPoint">The snap point to query for connections</param>
        //! <param name="predicate">The condition to test each connected component against</param>
        //! <returns>True if any connected component of type T satisfies the predicate</returns>
        //!
        //! Example:
        //! <code>
        //! // Check if any successor is occupied
        //! bool anyOccupied = snapOut.AnyConnected<IPalletHandling>(h => h.GetOccupied());
        //!
        //! // Check if any successor is a specific type
        //! bool hasPalletConveyor = snapOut.AnyConnected<PalletConveyor>(c => c.Sensor != null);
        //! </code>
        public static bool AnyConnected<T>(this SnapPoint snapPoint, System.Func<T, bool> predicate) where T : class
        {
            return snapPoint.GetConnectedComponents<T>().Any(predicate);
        }
    }
}
