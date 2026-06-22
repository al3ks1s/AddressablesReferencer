using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor.Build.Content;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;
namespace UnityEditor.AddressableAssets.Settings.GroupSchemas
{
    /// <summary>
    /// Class that represents a mapping between editor assets and the addressable assets found within a bundle.
    /// </summary>
    [Serializable]
    public class AddressableReferenceEntry
    {

        [SerializeField]
        public string internalName;

        [SerializeField]
        public string cabName;

        [SerializeField]
        public string baseInternalId;

        [SerializeField]
        public List<ObjectMapping> m_ObjectMapping = new();
        // public Dictionary<ObjectIdentifier, long> m_ObjectMapping;

        [SerializeField]
        public bool isDone = false;


        public Dictionary<ObjectIdentifier, long> ObjectMappingDict
        {
            get { 
                if (m_ObjectMapping == null) 
                    m_ObjectMapping = new();

                Dictionary<ObjectIdentifier, long> mapping = new();

                foreach (var kvp in m_ObjectMapping)
                {
                    if (kvp.m_objectId != null)
                        mapping.TryAdd(kvp.m_objectId, kvp.m_pathId);
                }

                return mapping; 
            }
        }

    }

    [Serializable]
    public class ObjectMapping
    {

        public ObjectMapping(ObjectIdentifier obid, long pathId)
        {
            m_objectId = obid;
            m_pathId = pathId;
        }


        [SerializeField]
        public ObjectIdentifier m_objectId;

        [SerializeField]
        public long m_pathId;

    }

}
