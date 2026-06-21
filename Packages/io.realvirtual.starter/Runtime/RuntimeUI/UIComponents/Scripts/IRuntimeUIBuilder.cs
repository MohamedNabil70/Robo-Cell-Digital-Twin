
namespace realvirtual
{
    ﻿/// <summary>
    /// Interface for classes that want to build runtime UI using RuntimeUIBuilder.
    /// Implement this interface to create dynamic UI.
    /// </summary>
    public interface IRuntimeUIBuilder
    {
        /// <summary>
        /// Called to build or rebuild the UI using the provided builder.
        /// </summary>
        /// <param name="builder">The RuntimeUIBuilder instance to use for creating UI</param>
        public void BuildRuntimeUI(RuntimeUIBuilder builder);
    }
    
}
