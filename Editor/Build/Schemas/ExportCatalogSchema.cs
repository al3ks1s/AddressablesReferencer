using System.ComponentModel;
using UnityEngine;
namespace UnityEditor.AddressableAssets.Settings.GroupSchemas
{
    /// <summary>
    /// 
    /// </summary>
    [DisplayName("Export Catalog to Build Location")]
    public class ExportCatalogSchema : AddressableAssetGroupSchema
    {

        [SerializeField]
        private bool m_enableExport;

        public bool EnableExport
        {
            get { return m_enableExport; }
            set { m_enableExport = value; SetDirty(true); }
        }


    }
}