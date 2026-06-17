using UnityEngine;
using UnityEngine.U2D;

public class SpriteAtlasAnalyzer : GenericAnalyzer<SpriteAtlas>
{

    /// <inheritdoc />
    public override string CreateAssetEntry<SpriteAtlas>()
    {
        return string.Empty;
    }


    /// <inheritdoc />
    public override string CreateAssetReference<SpriteAtlas>()
    {
        return string.Empty;
    }

}
