using Steamworks;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class AnalyzerWindow : EditorWindow
{

    TextField text;

    [MenuItem("Window/Asset Management/Addressable Referencer/Analyzer")]
    public static void ShowAnalyzerWindow()
    {
        EditorWindow wnd = EditorWindow.GetWindow<AnalyzerWindow>();
        wnd.titleContent = new GUIContent("Addressable Referencer - Analyze bundles");
    }


    public void CreateGUI()
    {

        var button = new Button();
        button.text = "Test stuff";

        button.clicked += lesgo;

        text = new TextField("Enter StreamingAssets/aa path");
        text.value = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Hollow Knight Silksong\\Hollow Knight Silksong_Data\\StreamingAssets\\aa";

        rootVisualElement.Add(button);
        rootVisualElement.Add(text);

    }


    public void lesgo()
    {
        
        CatalogAnalyzer cat = new(text.value);

        // using (var scope = new AssetDatabase.AssetEditingScope()) { 
            cat.LoadCatalog(Path.Join(cat.StreamingAssetsPath, "catalog.bin"));
            cat.ProcessGroups();
        // }

    }
}
