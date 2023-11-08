using System;
using UnityEngine;
using System.Collections.Generic;
using HarmonyLib;
using Object = UnityEngine.Object;

namespace ValheimVRM
{
    public class MeshSync
    {
        private static MeshSync _instance;
        private bool isReady = false;
        private static string objPrefix = "vrm_";
        private bool hasRunOnce = false;

        public bool IsReady
        {
            get => isReady;
            private set => isReady = value;
        }

        public enum Equipment
        {
            Megingjord,
        }

        private static Dictionary<Equipment, string> equipmentNameMap = new Dictionary<Equipment, string>
        {
            { Equipment.Megingjord, "BeltStrength" }
        };

        public enum Bones
        {
            Hips,
        }

        private static Dictionary<Bones, List<string>> boneMap = new Dictionary<Bones, List<string>>
        {
            { Bones.Hips, new List<string> { "Hips", "Pelvis" } }
        };

        public string GetEquipmentName(Equipment equipmentName)
        {
            if (equipmentNameMap.TryGetValue(equipmentName, out string name) && !string.IsNullOrEmpty(name))
            {
                return name;
            }

            Debug.LogError($"Failed to get the name for the equipment: {name}");
            return null;
        }
        
        private Transform FindDeepChild(Transform parent, string childName, int depth = 0, int maxDepth = 6)
        {
            if (depth > maxDepth)
                return null;

            foreach (Transform child in parent)
            {
                if (child.name.IndexOf(childName, StringComparison.OrdinalIgnoreCase) >= 0)
                    return child;

                var result = FindDeepChild(child, childName, depth + 1, maxDepth);
                if (result != null)
                    return result;
            }
            return null;
        }
        private bool TryGetBone(Bones bone, Transform rootTransform, out Transform targetBone)
        {
            targetBone = null;

            if (boneMap.TryGetValue(bone, out List<string> paths))
            {
                foreach (var path in paths)
                {
                    targetBone = FindDeepChild(rootTransform, path);
                    if (targetBone != null)
                        return true;
                }
            }

            Debug.LogError($"Failed to get the hierarchy path for the bone: {bone}");
            return false;
        }


        private struct MeshData
        {
            public Mesh Mesh;
            public Material[] Materials;
            public string Name;
        }

        private Dictionary<string, MeshData> meshDataDictionary = new Dictionary<string, MeshData>();


        [HarmonyPatch(typeof(VisEquipment), "SetUtilityItem")]
        public static class VisEquipment_SetItem_Patch
        {
            [HarmonyPrefix]
            public static void Prefix(string name)
            {
                if (string.IsNullOrEmpty(name)) return;

                if(Instance.hasRunOnce) return;
                Instance.hasRunOnce = true;
                
                var allCreateSuccess = false;

                allCreateSuccess |= Instance.CreateMesh(Equipment.Megingjord, -460902046);

                if (allCreateSuccess)
                {
                    Instance.isReady = true;


                    VisEquipment visEquipmentInstance = Object.FindObjectOfType<VisEquipment>();
                    if (visEquipmentInstance != null)
                    {
                        var method = visEquipmentInstance.GetType().GetMethod("UpdateLodgroup",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (method != null)
                        {
                            method.Invoke(visEquipmentInstance, null);
                        }
                    }
                }
            }
        }

        // [HarmonyPatch(typeof(ObjectDB), "Awake")]
        // public static class ObjectDB_Awake_Patch
        // {
        //     [HarmonyPostfix]
        //     public static void Postfix()
        //     {
        //
        //
        //     }
        // }


        // Singleton
        public static MeshSync Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new MeshSync();
                }

                return _instance;
            }
        }

        private MeshSync()
        {
        }


 
        public void MakeTransforms(ref GameObject meshObject, Settings.VrmSettingsContainer settings)
        {
            string[] splitName = meshObject.name.Split('_');
            string itemName = splitName[splitName.Length - 1];
            
            if (itemName == equipmentNameMap[Equipment.Megingjord]) 
            {  
                Debug.Log("meshObject.name INSIDE Megingjord");
                var playerScaleRatio = (settings.PlayerHeight / 1.85f);
                var scaleFactor = settings.MegingjordScale * playerScaleRatio * 100;
                var objectScale = new Vector3(scaleFactor,scaleFactor,scaleFactor);
                
                //var scaledPivot = new Vector3(-0.000011f * scaleFactor, -0.000025f * scaleFactor, -0.012647f * scaleFactor);
                var scaledOffset = new Vector3(-0.000011f * scaleFactor, 0.012647f * scaleFactor, 0.0006f * scaleFactor);
                
                meshObject.transform.localScale = objectScale;
                meshObject.transform.localRotation = Quaternion.Euler(90, 180, 0);
                meshObject.transform.localPosition = scaledOffset;

            }
        }

        private bool DoCreateMesh(string name, GameObject go)
        {
            if (meshDataDictionary.ContainsKey(name))
            {
                Debug.LogWarning($"Mesh with name {name} already exists in the dictionary. Skipping creation.");
                return false;
            }

            MeshData meshData;
            meshData.Name = name;
            meshData.Mesh = BakeMesh(go, out meshData.Materials);

            bool wasMeshCreated = false;


            if (meshData.Mesh != null)
            {
                wasMeshCreated = true;

                meshDataDictionary[name] = meshData;
            }
            else
            {
                Debug.LogError($"Failed to bake mesh for {name}.");
            }

            return wasMeshCreated;
        }


        public bool CreateMesh(Equipment equipmentName)
        {
            var name = GetEquipmentName(equipmentName);

            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            GameObject prefab = ObjectDB.instance.GetItemPrefab(name);
            if (prefab == null)
            {
                Debug.LogError($"Failed to find prefab for {name}. Cannot create mesh for {name}.");
                return false;
            }

            return DoCreateMesh(name, prefab);
        }


        public bool CreateMesh(Equipment equipmentName, string objectName)
        {
            var name = GetEquipmentName(equipmentName);

            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            GameObject prefab = ObjectDB.instance.GetItemPrefab(objectName);
            if (prefab == null)
            {
                Debug.LogError($"Failed to find prefab for {objectName}. Cannot create mesh for {name}.");
                return false;
            }

            return DoCreateMesh(name, prefab);
        }

        public bool CreateMesh(Equipment equipmentName, int hash)
        {
            var name = GetEquipmentName(equipmentName);

            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            GameObject prefab = ObjectDB.instance.GetItemPrefab(hash);
            if (prefab == null)
            {
                Debug.LogError($"Failed to find prefab for hash {hash}. Cannot create mesh for {name}.");
                return false;
            }

            return DoCreateMesh(name, prefab);
        }

        private Mesh BakeMesh(GameObject go, out Material[] materials)
        {
            materials = null;


            SkinnedMeshRenderer[] smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>();

            foreach (SkinnedMeshRenderer smr in smrs)
            {
                if (smr != null)
                {
                    Mesh mesh = new Mesh();
                    smr.BakeMesh(mesh);

                    materials = smr.materials;

                    return mesh;
                }
            }


            MeshFilter[] mfs = go.GetComponentsInChildren<MeshFilter>();
            foreach (MeshFilter mf in mfs)
            {
                if (mf != null && mf.gameObject.GetComponent<MeshRenderer>() != null)
                {
                    materials = mf.gameObject.GetComponent<MeshRenderer>().materials;

                    return mf.sharedMesh;
                }
            }

            // If we get here, no mesh was found
            return null;
        }


        public Mesh GetMesh(string name)
        {
            if (meshDataDictionary.TryGetValue(name, out MeshData meshData))
            {
                return meshData.Mesh;
            }

            Debug.LogWarning($"Mesh with name {name} not found in dictionary.");
            return null;
        }

        private MeshData? GetMeshData(string name)
        {
            if (meshDataDictionary.TryGetValue(name, out MeshData meshData))
            {
                return meshData;
            }

            Debug.LogWarning($"Mesh with name {name} not found in dictionary.");
            return null;
        }

        private void DoDetachFrom(Bones bone, string meshName, GameObject vrm)
        {
            // Find the target bone

            if (!TryGetBone(bone, vrm.transform, out Transform targetBone))
            {
                Debug.LogError($"Bone named {bone} not found in VRM model.");
                return;
            }

            // Find the child GameObject by name and destroy it
            Transform meshObject = targetBone.Find($"{objPrefix}{meshName}");
            if (meshObject != null)
            {
                Object.Destroy(meshObject.gameObject);
            }
            else
            {
                Debug.LogWarning($"GameObject named {objPrefix}{meshName} not found as child of bone {bone}.");
            }
        }

        public void DetachFrom(Bones bone, Equipment equipmentName, GameObject vrm)
        {
            var name = GetEquipmentName(equipmentName);
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            DoDetachFrom(bone, name, vrm);
        }

        private GameObject DoAttachTo(Bones bone, MeshData meshData, GameObject vrm, bool createNested)
        {
            if (vrm == null)
            {
                Debug.LogError("Provided VRM GameObject is null.");
                return null;
            }

            if (meshData.Mesh == null)
            {
                Debug.LogError("Provided Mesh is null.");
                return null;
            }
            
            if (!TryGetBone(bone, vrm.transform, out Transform targetBone))
            {
                Debug.LogError($"Bone named {bone} not found in VRM model.");
                return null;
            }

            GameObject meshObject = new GameObject($"{objPrefix}{meshData.Name}");
            GameObject meshObjectChild = null;

            if (meshObject == null)
            {
                Debug.LogError("Failed to create new GameObject for mesh.");
                return null;
            }

            if (createNested)
            {
                meshObjectChild = new GameObject($"child_{meshData.Name}");
                meshObjectChild.transform.SetParent(meshObject.transform);
            }

            GameObject objectToSetup = createNested ? meshObjectChild : meshObject;

            MeshFilter meshFilter = objectToSetup.AddComponent<MeshFilter>();
            if (meshFilter == null)
            {
                Debug.LogError("Failed to add MeshFilter to new GameObject.");
                return null;
            }

            MeshRenderer meshRenderer = objectToSetup.AddComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                Debug.LogError("Failed to add MeshRenderer to new GameObject.");
                return null;
            }

            meshFilter.sharedMesh = meshData.Mesh;
            if (meshFilter.sharedMesh == null)
            {
                Debug.LogError("Failed to assign mesh to MeshFilter.");
                return null;
            }

            meshRenderer.materials = meshData.Materials;

            meshObject.transform.SetParent(targetBone);
            if (meshObject.transform.parent != targetBone)
            {
                Debug.LogError($"Failed to set {meshObject.name}'s parent to {targetBone.name}.");
                return null;
            }

            meshObject.SetActive(true);
            return meshObject; // This will return the holder object, not the nested child.
        }


        public GameObject AttachTo(Bones bone, Equipment equipmentName, GameObject vrm, bool createNested = false)
        {
            var name = GetEquipmentName(equipmentName);
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            var meshData = GetMeshData(name);
            if (meshData.HasValue)
            {
                return DoAttachTo(bone, meshData.Value, vrm, createNested);
            }

            Debug.LogError($"{name} Does not exist");
            return null;
        }


        public bool IsAttached(Bones bone, Equipment equipmentName, GameObject vrm)
        {
            var name = GetEquipmentName(equipmentName);

            if (string.IsNullOrEmpty(name))
            {
                return false;
            }
            
            if (!TryGetBone(bone, vrm.transform, out Transform targetBone))
            {
                return false;
            }

            Transform meshObject = targetBone.Find($"{objPrefix}{name}");
            return meshObject != null;
        }
    }
}