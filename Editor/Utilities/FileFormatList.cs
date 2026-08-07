using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AddressableReferencer.Editor.Utilities
{

    /// <summary>
    /// A list of alternative file extensions for different types of objects.
    /// Used as fallback file extension when the exact path found in the bundles aren't the ones exported by AssetRipper or other such tools
    /// </summary>
    public class FileFormatList
    {

        private static Dictionary<string, List<string>> m_formatList = new()
    {
        { "AudioClip" , new List<string>(){ ".ogg", ".wav" } },
        { "GameObject" , new List<string>(){ ".prefab" } },
        { "Texture2D" , new List<string>(){ ".png", ".jpg" } }
    };

        public static List<string> GetFormatList(string bundleType)
        {


            m_formatList.TryGetValue(bundleType, out var list);

            if (list == null)
            {
                list = new List<string>();
            }

            // Last chance?
            list.Add(".asset");

            return list;
        }

    }
}