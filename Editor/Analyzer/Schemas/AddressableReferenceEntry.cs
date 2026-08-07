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
        public string primaryKey;

        [SerializeField]
        public string internalName;

        [SerializeField]
        public string cabName;

        [SerializeField]
        public string baseInternalId;

        [SerializeField]
        public List<ObjectMapping> m_ObjectMapping = new();

        [SerializeField]
        public bool isDone = false;

        public Dictionary<ObjectIdentifier, long> ObjectMappingDict
        {
            get
            {
                if (m_ObjectMapping == null)
                    m_ObjectMapping = new();


                Dictionary<ObjectIdentifier, long > mapping = new();

                foreach (var obmp in m_ObjectMapping)
                {
                    if (obmp.Overridden) 
                    { 
                        mapping.TryAdd(obmp.ObjectId, obmp.m_pathIdOverride); 
                    } 
                    else
                    {
                        mapping.TryAdd(obmp.ObjectId, obmp.m_pathId);
                    }
                }

                return mapping;
            }
        }
    }

    /// <summary>
    /// Defines a link between an editor-side object or component and the in-bundle object identifier.
    /// 
    /// The ObjectIdentifier had to be decomposed into its internal values as the ObjectIdentifier's fields aren't quite serializable.
    /// </summary>
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

        [SerializeField]
        public long m_pathIdOverride;

        public bool Overridden
        {
            get { return m_pathIdOverride != 0; }
        }

        public void ResetOverride()
        {
            m_pathIdOverride = 0;
        }

        /// <summary>
        /// The <see cref="ObjectIdentifier"/> represented by this object.
        /// </summary>
        public ObjectIdentifier ObjectId
        {
            get
            {
                return CreateObjectIdentifier(m_GUID, m_LocalIdentifierInFile, m_FileType, m_FilePath);
            }
        }

        /// <summary>
        /// Recreate an ObjectIdentifier 
        /// </summary>
        /// <param name="GUID">The specific asset that contains this object.</param>
        /// <param name="localIdentifierInFile">The index of the object inside a serialized file.</param>
        /// <param name="fileType">Type of file that contains this object.</param>
        /// <param name="filePath">The file path on disk that contains this object. (Only used for objects not known by the AssetDatabase).</param>
        /// <returns>The reconstructed <see cref="ObjectIdentifier"/></returns>
        public static ObjectIdentifier CreateObjectIdentifier(string GUID, long localIdentifierInFile, FileType fileType, string filePath)
        {

            object boxed = new ObjectIdentifier();
            System.Type obidT = typeof(ObjectIdentifier);

            obidT.GetField("m_GUID", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(boxed, new GUID(GUID));
            obidT.GetField("m_LocalIdentifierInFile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(boxed, localIdentifierInFile);
            obidT.GetField("m_FileType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(boxed, fileType);
            obidT.GetField("m_FilePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(boxed, filePath);

            return (ObjectIdentifier)boxed;

        }

    }
}
