using System;
using System.Collections.Generic;
using UnityEditor.Build.Content;
using UnityEngine;

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


        public Dictionary<(GUID, long, FileType, string), long> ObjectMappingDict
        {
            get {
                if (m_ObjectMapping == null)
                    m_ObjectMapping = new();

                Dictionary<(GUID, long, FileType, string), long> mapping = new();

                foreach (var obmp in m_ObjectMapping)
                {
                    mapping.TryAdd((new GUID(obmp.m_GUID), obmp.m_LocalIdentifierInFile, obmp.m_FileType, obmp.m_FilePath), obmp.m_pathId);
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

            m_GUID = obid.guid.ToString();
            m_LocalIdentifierInFile = obid.localIdentifierInFile;
            m_FilePath = obid.filePath;
            m_FileType = obid.fileType;

            m_pathId = pathId;
        }


        [SerializeField]
        public string m_GUID;

        [SerializeField]
        public long m_LocalIdentifierInFile;

        [SerializeField]
        public FileType m_FileType;

        [SerializeField]
        public string m_FilePath;

        [SerializeField]
        public long m_pathId;

    } 

}
