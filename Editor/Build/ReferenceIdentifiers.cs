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

public class ReferenceIdentifier : IDeterministicIdentifiers
{

    private Dictionary<ObjectIdentifier, long> m_objectReferences;
    private Dictionary<string, string> m_bundleReferences;

    private IDeterministicIdentifiers defaultIdentifier;

    public ReferenceIdentifier(Dictionary<string, string> bundleReferences, Dictionary<ObjectIdentifier, long> objectReferences, bool contiguousBundles = false)
    {
        m_bundleReferences = bundleReferences;
        m_objectReferences = objectReferences; 
        defaultIdentifier = contiguousBundles ? new PrefabPackedIdentifiers() : (IDeterministicIdentifiers)new Unity5PackedIdentifiers();
    }

    /// <inheritdoc />
    public virtual string GenerateInternalFileName(string name)
    {
        if (m_bundleReferences.TryGetValue(name, out var cabName)) { 
            return cabName;
        }
        return defaultIdentifier.GenerateInternalFileName(name);
    }

    /// <inheritdoc />
    public virtual long SerializationIndexFromObjectIdentifier(ObjectIdentifier objectID)
    {
        if (m_objectReferences.TryGetValue(objectID, out var serialIndex)) { 
            return serialIndex;
        }
        return defaultIdentifier.SerializationIndexFromObjectIdentifier(objectID);
    }
}
