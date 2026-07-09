using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AddressableReferencer.Editor.Utilities
{
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