using UnityEngine;
using UnityEngine.U2D;

public class SpriteAtlasAnalyzer : IAssetAnalyzer<SpriteAtlas>
{

    /// <inheritdoc />
    public string CreateAssetEntry<SpriteAtlas>()
    {
        return string.Empty;
    }


    /// <inheritdoc />
    public string CreateAssetReference<SpriteAtlas>()
    {
        return string.Empty;
    }

}
