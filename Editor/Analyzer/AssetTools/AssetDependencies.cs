using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Collections.Generic;

// MIT License Copyright (c) 2026 flibber-hk - https://github.com/flibber-hk/AssetHelperLib/

namespace AddressableReferencer.Editor.Analyzer { 

    /// <summary>
    /// Helper class for calculating the dependencies of assets.
    /// </summary>
    public class AssetDependencies
    {

        public AssetDependencies(AssetsManager mgr, AssetsFileInstance afileInst, AssetDependencies.Config? settings = null)
        {
            _mgr = mgr;
            _afileInst = afileInst;

            if (settings != null)
            {
                Settings = settings;
            }
            else
            {
                Settings = new();
            }

        }

        /// <summary>
        /// Class holding settings for the asset dependencies resolver.
        /// </summary>
        public class Config
        {
            /// <summary>
            /// If false, will not consider the parent of a transform as a dependency.
            /// </summary>
            public bool FollowTransformParent { get; set; } = false;
        }

        /// <summary>
        /// Record representing a PPtr in a bundle.
        /// </summary>
        /// <param name="FileId"></param>
        /// <param name="PathId"></param>
        /// <param name="TypeName">The name of the type (including PPtr&lt;...&gt;).</param>
        public record PPtrData(int FileId, long PathId, string TypeName);

        /// <summary>
        /// Record representing a collection of PPtrs associated with an asset.
        /// </summary>
        /// <param name="InternalPaths">Path IDs within the current file.</param>
        /// <param name="ExternalPaths">Dependency records external to the current file.
        /// There may be duplicates in this set if the same dependency is pointed at
        /// as two different types (e.g. once as a Component and once as a specified MonoBehaviour).</param>
        public record ChildPPtrs(HashSet<long> InternalPaths, HashSet<PPtrData> ExternalPaths)
        {
            /// <summary>
            /// Create a new empty <see cref="ChildPPtrs"/> instance.
            /// </summary>
            /// <returns></returns>
            public static ChildPPtrs CreateNew() => new(new(), new());

            /// <summary>
            /// Add a new PPtr to the collection.
            /// </summary>
            public bool Add(int fileId, long pathId, string typeName)
            {
                if (pathId == 0) return false;
                if (fileId == 0) return InternalPaths.Add(pathId);
                return ExternalPaths.Add(new(fileId, pathId, typeName));
            }

            /// <inheritdoc cref="Add(int, long, string)" />
            public bool Add(AssetTypeValueField valueField, string typeName)
                => Add(valueField["m_FileID"].AsInt, valueField["m_PathID"].AsLong, typeName);
        }


        private readonly AssetsManager _mgr;
        private readonly AssetsFileInstance _afileInst;

        /// <summary>
        /// The settings for this instance.
        /// </summary>
        public Config Settings { get; set; }

        private readonly Dictionary<long, ChildPPtrs> _immediateDeps = new();
        private readonly Dictionary<long, ChildPPtrs> _GameObjectHierarchyDeps = new();
        private readonly Dictionary<long, ChildPPtrs> _bundleDeps = new();

        /// <summary>
        /// The number of cache hits for the <see cref="FindImmediateDeps(long)"/> function.
        /// </summary>
        public int Hits { get; private set; } = 0;

        /// <summary>
        /// The number of cache misses for the <see cref="FindImmediateDeps(long)"/> function.
        /// </summary>
        public int Misses { get; private set; } = 0;

        /// <summary>
        /// Find all PPtr nodes pointed to by the given asset.
        /// </summary>
        /// <param name="assetPathId">The path ID for the asset to check.</param>
        public ChildPPtrs FindImmediateDeps(long assetPathId)
        {
            if (_immediateDeps.TryGetValue(assetPathId, out ChildPPtrs cached))
            {
                Hits++;
                return cached;
            }

            Misses++;

            AssetFileInfo info = _afileInst.file.GetAssetInfo(assetPathId);

            ChildPPtrs childPPtrs = ChildPPtrs.CreateNew();

            if (!Settings.FollowTransformParent && (
                info.TypeId == (int)AssetClassID.Transform
                || info.TypeId == (int)AssetClassID.RectTransform
                ))
            {
                AssetTypeValueField tfValueField = _mgr.GetBaseField(_afileInst, info);

                childPPtrs.Add(tfValueField["m_GameObject"], "PPtr<GameObject>");

                foreach (AssetTypeValueField childVf in tfValueField["m_Children.Array"].Children)
                {
                    childPPtrs.Add(childVf, "PPtr<Transform>");
                }

                return _immediateDeps[assetPathId] = childPPtrs;
            }

            AssetTypeValueIterator atvIterator = _mgr.CreateIterator(_afileInst, info);

            while (atvIterator.ReadNext())
            {
                string typeName = atvIterator.TempField.Type;

                if (!typeName.StartsWith("PPtr<")) continue;

                AssetTypeValueField valueField = atvIterator.ReadValueField();
                childPPtrs.Add(valueField, typeName);
            }

            return _immediateDeps[assetPathId] = childPPtrs;
        }

        /// <summary>
        /// Enumerate all pptrs that are dependencies of this asset. PPtrs within the current bundle will be followed
        /// but external pptrs will not.
        /// </summary>
        /// <param name="assetPathId">The path ID for the asset to check.</param>
        public ChildPPtrs FindBundleDeps(long assetPathId)
        {
            if (_bundleDeps.TryGetValue(assetPathId, out ChildPPtrs deps))
            {
                return deps;
            }

            HashSet<long> internalSeen = new() { assetPathId };
            HashSet<PPtrData> externalSeen = new();

            Queue<long> toProcess = new();
            toProcess.Enqueue(assetPathId);

            // Acquire the lock for the whole procedure
            lock (_afileInst.LockReader)
            {
                while (toProcess.TryDequeue(out long current))
                {
                    ChildPPtrs childPptrs = FindImmediateDeps(current);

                    externalSeen.UnionWith(childPptrs.ExternalPaths);

                    foreach (long pathId in childPptrs.InternalPaths)
                    {
                        if (internalSeen.Add(pathId))
                        {
                            toProcess.Enqueue(pathId);
                        }
                    }
                }
            }

            internalSeen.Remove(assetPathId);

            return _bundleDeps[assetPathId] = new(internalSeen, externalSeen);
        }
    
        /// <summary>
        /// 
        /// </summary>
        /// <param name="assetPathId"></param>
        /// <returns></returns>
        public ChildPPtrs FindChildGO(long assetPathId, bool recursive = false)
        {
            if (_immediateDeps.TryGetValue(assetPathId, out ChildPPtrs cached))
            {
                Hits++;
                return cached;
            }

            Misses++;

            AssetFileInfo info = _afileInst.file.GetAssetInfo(assetPathId);
            ChildPPtrs childPPtrs = ChildPPtrs.CreateNew();

            AssetTypeValueField transformField = _mgr.GetTransform(_afileInst, info).ValueField;

            foreach (AssetTypeValueField childVf in transformField["m_Children.Array"].Children)
            {
                long childPptr = childVf["m_PathID"].AsLong;
                childPPtrs.InternalPaths.Add(childPptr);   
            }

            return childPPtrs;

        }
    
    }
}