using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRM;

namespace ValheimVRM
{
    public class VRMBlendShapeSync : MonoBehaviour
    {
        private VRMBlendShapeProxy proxy;
        private GameObject vrm;
        private List<BlendShapeKey> dirtyBlendshapes = new List<BlendShapeKey>();

        public class BlendshapeMap
        {
            public SkinnedMeshRenderer Smr;
            public int Index;
            public BlendShapeBinding Binding;
        }

        private Dictionary<string, List<BlendshapeMap>> blendshapeDict = new Dictionary<string, List<BlendshapeMap>>();


        private readonly List<string> armorBlendshapes = new List<string>
        {
            "v_armorswap_rag",
            "v_armorswap_leather",
            "v_armorswap_troll",
            "v_armorswap_root",
            "v_armorswap_iron",
            "v_armorswap_fenris",
            "v_armorswap_wolf",
            "v_armorswap_padded",
            "v_armorswap_etirweave",
            "v_armorswap_carapace"
        };

        string GetRelativePath(SkinnedMeshRenderer smr)
        {
            Transform current = smr.transform;
            string path = current.name;
            while (current.parent != null && current.parent != vrm.transform)
            {
                current = current.parent;
                path = current.name + "/" + path;
            }

            return path;
        }

        public void SetValues(Dictionary<BlendShapeKey, float> values)
        {
            proxy.SetValues(values);
            foreach (var key in values.Keys)
            {
                dirtyBlendshapes.Add(key);
            }
        }

        void AddBlendShapeClip(SkinnedMeshRenderer smr, int i, string blendShapeName)
        {
            var avatar = proxy.BlendShapeAvatar;

            bool exists = avatar.Clips.Exists(existingClip => existingClip.BlendShapeName == blendShapeName);
            if (exists)
            {
                Debug.LogWarning($"BlendShapeClip '{blendShapeName}' already exists. Skipping.");
                return;
            }

            Debug.Log($"BlendShapeClip Adding: {blendShapeName}");


            var clip = ScriptableObject.CreateInstance<BlendShapeClip>();

            // unity asset name
            clip.name = blendShapeName;
            // vrm export name
            clip.BlendShapeName = blendShapeName;
            clip.Preset = BlendShapePreset.Unknown;

            clip.IsBinary = false;
            clip.Values = new BlendShapeBinding[]
            {
                new BlendShapeBinding
                {
                    RelativePath = GetRelativePath(smr), // target Renderer relative path from root 
                    Index = i,
                    Weight = 100f
                },
            };
            clip.MaterialValues = Array.Empty<MaterialValueBinding>();
            // clip.MaterialValues = new MaterialValueBinding[]
            // {
            //     new MaterialValueBinding
            //     {
            //         MaterialName = "Target", // target material name
            //         ValueName = "_Color", // target material property name,
            //         BaseValue = new Vector4(1, 1, 1, 1), // Target value when the Weight value of BlendShapeClip is 0
            //         TargetValue = new Vector4(0, 0, 0, 1), // Target value when the Weight value of BlendShapeClip is 1
            //     },
            // };
            avatar.Clips.Add(clip);
        }


        public void Setup(GameObject vrm)
        {
            this.proxy = vrm.GetComponent<VRMBlendShapeProxy>();
            this.vrm = vrm;

//1. Build a list of all blendShapeClips.
            foreach (var clip in proxy.BlendShapeAvatar.Clips)
            {
                if (!blendshapeDict.ContainsKey(clip.BlendShapeName))
                {
                    var blendshapeMaps = new List<BlendshapeMap>();
                    foreach (var value in clip.Values)
                    {

                        blendshapeMaps.Add(new BlendshapeMap
                        {
                            Smr = null,
                            Index = value.Index,
                            Binding = value
                        });
                    }

                    blendshapeDict[clip.BlendShapeName] = blendshapeMaps;
                }
            }

            
            var allSmr = vrm.GetComponentsInChildren<SkinnedMeshRenderer>();

//  2. If an armor blendshape is spoted as a blendshape and not a blendShapeClip, turn it into a clip.
            foreach (var smr in allSmr)
            {
                for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
                {
                    string blendshapeName = smr.sharedMesh.GetBlendShapeName(i);
                    if (armorBlendshapes.Contains(blendshapeName) && !blendshapeDict.ContainsKey(blendshapeName) )
                    {
                        Debug.Log("Armor Blendshape Found Outside of BlendShape Clip, Auto Making Clip: " + blendshapeName);
                        AddBlendShapeClip(smr, i, blendshapeName);

                        var blendshapeClip = proxy.BlendShapeAvatar.Clips
                            .FirstOrDefault(clip => clip.BlendShapeName == blendshapeName);

                        if (blendshapeClip == null)
                        {
                            Debug.LogError($"No BlendShapeClip found for blendshape {blendshapeName}. This should not happen.");
                            continue;
                        }

                        blendshapeDict[blendshapeName] = new List<BlendshapeMap>
                        {
                            new BlendshapeMap
                            {
                                Smr = smr,
                                Index = i,
                                Binding = blendshapeClip.Values.FirstOrDefault(b =>
                                    b.RelativePath == GetRelativePath(smr) && b.Index == i)
                            }
                        };
                    }
                }
            }


//3. Find the correct smr refrence for the map
            foreach (var key in blendshapeDict.Keys.ToList())
            {
                var blendshapeMaps = blendshapeDict[key];
                foreach (var blendshapeMap in blendshapeMaps)
                {
                    foreach (var smr in allSmr)
                    {
                        if (blendshapeMap.Smr != null || blendshapeMap.Binding.RelativePath != GetRelativePath(smr))
                        {
                            continue;
                        }
                        string blendshapeName = smr.sharedMesh.GetBlendShapeName(blendshapeMap.Index);
                        blendshapeMap.Smr = smr;
                        Debug.Log($"Blend ShapeClip: {key} Add blendshape smr for {blendshapeName} ");
                    }
                }
            }
        }


        public List<BlendshapeMap> this[string blendshapeClipName]
        {
            get
            {
                if (blendshapeDict.TryGetValue(blendshapeClipName, out var blendshapes))
                {
                    return blendshapes;
                }

                throw new KeyNotFoundException($"Blendshape {blendshapeClipName} not found!");
            }
        }

        void Update()
        {
            if (dirtyBlendshapes.Count == 0) return; // Exit if no dirty blendshapes to process.


            foreach (var blendShapeKey in dirtyBlendshapes)
            {
                var weight = proxy.GetValue(blendShapeKey);
                if (blendshapeDict.TryGetValue(blendShapeKey.Name, out var blendshapeMaps))
                {
                    foreach (var blendshape in blendshapeMaps)
                    {
                        // Check for Null Reference
                        if (blendshape.Smr == null)
                        {
                            Debug.LogError($"SkinnedMeshRenderer for blendshape {blendShapeKey.Name} is null!");
                            continue;
                        }

                        // Validate Index
                        if (blendshape.Index < 0 || blendshape.Index >= blendshape.Smr.sharedMesh.blendShapeCount)
                        {
                            Debug.LogError(
                                $"Blendshape index {blendshape.Index} for {blendShapeKey.Name} is out of range!");
                            continue;
                        }

                        // Validate Weight
                        if (float.IsNaN(weight) || weight < 0 || weight > 100)
                        {
                            Debug.LogError($"Blendshape weight {weight} for {blendShapeKey.Name} is invalid!");
                            continue;
                        }
                            //NOTE: the blendshape.Binding.Weight is the max weight of the clip,
                                 // weight is a normalized 0-1 value
                        blendshape.Smr.SetBlendShapeWeight(blendshape.Index, weight * blendshape.Binding.Weight);
                    }
                }
                else
                {
                    Debug.LogWarning($"No blendshape found in dictionary for {blendShapeKey.Name}");
                }
            }

            dirtyBlendshapes.Clear(); // Clear the list
        }
    }
}