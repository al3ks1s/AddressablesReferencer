using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Injector;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine;


namespace UnityEditor.Build.Pipeline.Tasks
{

    /// <summary>
    /// Replaces calculated references to objects by the ones found in the already existing game bundles.
    /// </summary>
    public class ReferenceBaseBundles : IBuildTask
    {
        /// <inheritdoc />
        public int Version { get { return 1; } }

    #pragma warning disable 649
        [InjectContext(ContextUsage.In)]
        IDependencyData m_DependencyData;

        [InjectContext]
        IBundleWriteData m_WriteData;

        [InjectContext(ContextUsage.In)]
        IBuildContent m_Content;

        [InjectContext(ContextUsage.InOut, true)]
        IBuildResults m_Results;

        [InjectContext(ContextUsage.In, true)]
        IProgressTracker m_Tracker;

        [InjectContext(ContextUsage.In, true)]
        IBuildCache m_Cache;

        [InjectContext(ContextUsage.In, true)]
        IBuildLogger m_Log;
    #pragma warning restore 649




        /// <inheritdoc />
        public ReturnCode Run()
        {

            foreach (var operation in m_WriteData.WriteOperations)
            {
                var internalName = operation.Command.internalName;
                using (m_Log.ScopedStep(LogLevel.Info, $"Operation: {internalName}"))
                {
                    var refMap = m_WriteData.FileToReferenceMap[internalName];
                    var uSet = m_WriteData.FileToUsageSet[internalName];
                    var objects = m_WriteData.FileToObjects[internalName];
                    var bundle = m_WriteData.FileToBundle[internalName];
                    
                    foreach (var obj in objects)
                    {
                        using (m_Log.ScopedStep(LogLevel.Info, $"{obj.guid} {obj.localIdentifierInFile}")) {                             
                            m_Log.AddEntrySafe(LogLevel.Info, $"");
                        }
                    }               
                
                }
            }

            return ReturnCode.Success;
        }

    }
}