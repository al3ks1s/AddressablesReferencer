using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine;

namespace AddressableReferencer.Editor.Build
{

    /// <summary>
    /// Class that provides the build pipeline with the internal file name and object id from the game bundles during addressables build.
    /// </summary>
    public class ReferenceIdentifier : IDeterministicIdentifiers
    {

        public Dictionary<ObjectIdentifier, long> m_objectReferences;
        public Dictionary<string, string> m_bundleReferences;

        private IDeterministicIdentifiers defaultIdentifier;

        public ReferenceIdentifier(Dictionary<string, string> bundleReferences, Dictionary<ObjectIdentifier, long> objectReferences, bool contiguousBundles = false)
        {
            m_bundleReferences = bundleReferences;
            m_objectReferences = objectReferences;
            defaultIdentifier = contiguousBundles ? new PrefabPackedIdentifiers() : (IDeterministicIdentifiers)new Unity5PackedIdentifiers();
        }

        /// <summary>
        /// Retrieves the CABName of a reference bundle depending on the initial name identifier. As a fallback, generates a deterministic internal file name from the passed in name for bundles that are not references.
        /// </summary>
        /// <param name="name">Name identifier for internal file name generation</param>
        /// <returns>Deterministic file name.</returns>
        public virtual string GenerateInternalFileName(string name)
        {
            if (m_bundleReferences.TryGetValue(name, out var cabName))
            {
                return cabName;
            }
            return defaultIdentifier.GenerateInternalFileName(name);
        }

        /// <summary>
        /// Retrieves the base game bundle PathID for the <see cref="ObjectIdentifier"/> . As a fallback, generates a deterministic id for a given object in the build.
        /// </summary>
        /// <param name="objectID">Object identifier to for id generation.</param>
        /// <returns><c>long</c> representing the id of the objectID.</returns>
        public virtual long SerializationIndexFromObjectIdentifier(ObjectIdentifier objectID)
        {
            if (m_objectReferences.TryGetValue(objectID, out var serialIndex))
            {
                return serialIndex;
            }
            return defaultIdentifier.SerializationIndexFromObjectIdentifier(objectID);
        }
    }
}