using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections;
using UnityEngine;
using VRM;
using Object = UnityEngine.Object;

namespace ValheimVRM
{
	[HarmonyPatch(typeof(Shader))]
	[HarmonyPatch(nameof(Shader.Find))]
	static class ShaderPatch
	{
		 private static readonly Dictionary<string, Shader> ShaderDictionary = new Dictionary<string, Shader>();
		 
		 
		 // im not sure but there might be a bug in Shader.Find?
		 // its returning null anytime the Standard Shader is searched for.
		 // Searching though Resources.FindObjectsOfTypeAll<Shader>() shows that standard exists in Resources.
		 // so the bellow is a work around to find it.
		 
		static ShaderPatch()
		{
			Shader[] allShaders = Resources.FindObjectsOfTypeAll<Shader>();
			foreach (Shader shader in allShaders)
			{
				if (!ShaderDictionary.ContainsKey(shader.name))
				{
					ShaderDictionary.Add(shader.name, shader);
				}
			}
			Debug.Log("[ValheimVRM ShaderPatch] All shaders loaded into ShaderDictionary.");
		}
		

		static bool Prefix(ref Shader __result, string name)
		{
			Shader shader;
			if (ShaderDictionary.TryGetValue(name, out shader))
			{
				
				Debug.Log("[ValheimVRM ShaderPatch] Shader '" + name + "' found in preloaded ShaderDictionary.");
				__result = shader;
				return false;
			}
			
			
			if (VRMShaders.Shaders.TryGetValue(name, out shader))
			{
				Debug.Log("[ValheimVRM ShaderPatch] Shader '" + name + "' found in VRMShaders.Shaders");
				__result = shader;
				return false;
			}
			

			Debug.Log("[ValheimVRM ShaderPatch] Shader '" + name + "' NOT FOUND in ShaderDictionary. passing method to original Shader.Find.");
			return true;
		}
	}

	public static class VRMShaders
	{
		public static Dictionary<string, Shader> Shaders { get; } = new Dictionary<string, Shader>();

		public static void Initialize()
		{
			var bundlePath = "";
			if (Settings.globalSettings.UseShaderBundle == "current")
			{
				bundlePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"UniVrm.shaders");
			} else if (Settings.globalSettings.UseShaderBundle == "old")
			{
				bundlePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"OldUniVrm.shaders");
			}
			else
			{
				Debug.LogWarning("[ValheimVRM] Invalid UseShaderBundle; old, current");
			}
			
			if (File.Exists(bundlePath))
			{
				var assetBundle = AssetBundle.LoadFromFile(bundlePath);
				var assets = assetBundle.LoadAllAssets<Shader>();
				foreach (var asset in assets)
				{
					try
					{
						UnityEngine.Debug.Log("[ValheimVRM] Add Shader: " + asset.name);
						Shaders.Add(asset.name, asset);
					}
					catch (Exception ex)
					{
						Debug.LogError(ex);
					}
				}
			}
		}
	}

	public static class VrmManager
	{
		public static Dictionary<Player, GameObject> PlayerToVrmInstance = new Dictionary<Player, GameObject>();
		public static Dictionary<Player, string> PlayerToName = new Dictionary<Player, string>();
		public static Dictionary<string, VRM> VrmDic = new Dictionary<string, VRM>();
		
		public static VRM RegisterVrm(VRM vrm, LODGroup sampleLODGroup, Player player)
		{ 
			if (vrm.VisualModel == null) return null;
			
			foreach (var registered in VrmDic)
			{
				if (registered.Key == vrm.Name) continue;

				if (registered.Value == vrm || registered.Value.VisualModel == vrm.VisualModel)
				{
					Debug.LogError("[ValheimVRM] attempt to register a vrm that is already registered as " + registered.Key);
					return null;
				}
			}
 
			if (VrmDic.ContainsKey(vrm.Name))
			{
				var existing = VrmDic[vrm.Name];
				
				if (existing == vrm) return vrm;
				
				if (existing.VisualModel != vrm.VisualModel)
				{
					Object.Destroy(existing.VisualModel);
				}

				VrmDic[vrm.Name] = null;
			}

			Object.DontDestroyOnLoad(vrm.VisualModel);
			
			VrmDic[vrm.Name] = vrm;

			//[Error: Unity Log] _Cutoff: Range
			//[Error: Unity Log] _MainTex: Texture
			//[Error: Unity Log] _SkinBumpMap: Texture
			//[Error: Unity Log] _SkinColor: Color
			//[Error: Unity Log] _ChestTex: Texture
			//[Error: Unity Log] _ChestBumpMap: Texture
			//[Error: Unity Log] _ChestMetal: Texture
			//[Error: Unity Log] _LegsTex: Texture
			//[Error: Unity Log] _LegsBumpMap: Texture
			//[Error: Unity Log] _LegsMetal: Texture
			//[Error: Unity Log] _BumpScale: Float
			//[Error: Unity Log] _Glossiness: Range
			//[Error: Unity Log] _MetalGlossiness: Range

			// Shader replacement
			var settings = Settings.GetSettings(vrm.Name);
			var materials = new List<Material>();
			foreach (var smr in vrm.VisualModel.GetComponentsInChildren<SkinnedMeshRenderer>())
			{
				foreach (var mat in smr.materials)
				{
					if (!materials.Contains(mat)) materials.Add(mat);
				}
			}
			foreach (var mr in vrm.VisualModel.GetComponentsInChildren<MeshRenderer>())
			{
				foreach (var mat in mr.materials)
				{
					if (!materials.Contains(mat)) materials.Add(mat);
				}
			}


			CoroutineHelper.Instance.StartCoroutine(ProcessMaterialsCoroutine(vrm, materials, settings));
 

			var lodGroup = vrm.VisualModel.AddComponent<LODGroup>();
			if (settings.EnablePlayerFade)
			{
				lodGroup.SetLODs(new LOD[]
				{
					new LOD(0.1f, vrm.VisualModel.GetComponentsInChildren<SkinnedMeshRenderer>())
				});
			}
			lodGroup.RecalculateBounds();

			lodGroup.fadeMode = sampleLODGroup.fadeMode;
			lodGroup.animateCrossFading = sampleLODGroup.animateCrossFading;

			vrm.VisualModel.SetActive(false);

			return vrm;
    }

    public static IEnumerator ProcessMaterialsCoroutine(VRM vrm, List<Material> materials, Settings.VrmSettingsContainer settings)
    {
	    
        Shader foundShader = Shader.Find("Custom/Player");


        // if (foundShader != null)
        // {
	       //  var count = foundShader.GetPropertyCount();
	       //  for (int i = 0; i < count; i++)
	       //  {
		      //   Debug.Log($"Shader Name: {foundShader.name} Prop: {i} -> {foundShader.GetPropertyName(i)}");
        //
	       //  }
        // }
        
        foreach (var mat in materials)
        {

            if (settings.UseMToonShader && !settings.AttemptTextureFix && mat.HasProperty("_Color"))
            {
                var color = mat.GetColor("_Color");
                color.r *= settings.ModelBrightness;
                color.g *= settings.ModelBrightness;
                color.b *= settings.ModelBrightness;
                mat.SetColor("_Color", color);
            }
            else if(settings.AttemptTextureFix)
            {
                if (mat.shader != foundShader)
                {
                    var color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;

                    var mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") as Texture2D : null;
                    Texture2D tex = mainTex;
                    
                    if (mainTex != null)
                    {
	                    tex = new Texture2D(mainTex.width, mainTex.height);
                        var pixels = mainTex.GetPixels();
                        
                        var pixelsTask = Task.Run(() =>
                        {
	                        for (int i = 0; i < pixels.Length; i++)
	                        {
		                        var col = pixels[i] * color;   
		                        Color.RGBToHSV(col, out float h, out float s, out float v);
		                        v *= settings.ModelBrightness;
		                        pixels[i] = Color.HSVToRGB(h, s, v, true);
		                        pixels[i].a = col.a;
	                        }
                        });

                        while (!pixelsTask.IsCompleted)
                        {
	                        yield return new WaitUntil(() => pixelsTask.IsCompleted);
                        }

                        pixelsTask.Wait();
                        
                        
 
                        tex.SetPixels(pixels);
                        tex.Apply();

                    }
                    
                    var bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
                    mat.shader = foundShader;

                    mat.SetTexture("_MainTex", tex);
                    mat.SetTexture("_SkinBumpMap", bumpMap);
                    mat.SetColor("_SkinColor", color);
                    mat.SetTexture("_ChestTex", tex);
                    mat.SetTexture("_ChestBumpMap", bumpMap);
                    mat.SetTexture("_LegsTex", tex);
                    mat.SetTexture("_LegsBumpMap", bumpMap);
                    mat.SetFloat("_Glossiness", 0.2f);
                    mat.SetFloat("_MetalGlossiness", 0.0f);
                }
            }

            yield return null;
        }
        Debug.Log("[ValheimVRM] Material processing completed.");
    }
}


	[HarmonyPatch(typeof(VisEquipment), "UpdateLodgroup")]
	static class Patch_VisEquipment_UpdateLodgroup
	{
		[HarmonyPostfix]
		static void Postfix(VisEquipment __instance)
		{
			if (!__instance.m_isPlayer) return;
			var player = __instance.GetComponent<Player>();
			if (player == null || !VrmManager.PlayerToVrmInstance.ContainsKey(player)) return;
			
			var name = VrmManager.PlayerToName[player];

			var settings = Settings.GetSettings(name);

			var hair = __instance.GetField<VisEquipment, GameObject>("m_hairItemInstance");
			if (hair != null) SetVisible(hair, false);

			var beard = __instance.GetField<VisEquipment, GameObject>("m_beardItemInstance");
			if (beard != null) SetVisible(beard, false);

			var chestList = __instance.GetField<VisEquipment, List<GameObject>>("m_chestItemInstances");
			if (chestList != null)
			{
				if (!settings.ChestVisible)
				{
					foreach (var chest in chestList) SetVisible(chest, false);
				}
			}

			var legList = __instance.GetField<VisEquipment, List<GameObject>>("m_legItemInstances");
			if (legList != null)
			{
				if (!settings.LegsVisible)
				{
					foreach (var leg in legList) SetVisible(leg, false);
				}
			}

			var shoulderList = __instance.GetField<VisEquipment, List<GameObject>>("m_shoulderItemInstances");
			if (shoulderList != null)
			{
				if (shoulderList != null)
				{
					if (!settings.ShouldersVisible)
					{
						foreach (var shoulder in shoulderList) SetVisible(shoulder, false);
					}
				}
			}

			var utilityList = __instance.GetField<VisEquipment, List<GameObject>>("m_utilityItemInstances");
			if (utilityList != null)
			{
				if (!settings.UtilityVisible)
				{
					foreach (var utility in utilityList) SetVisible(utility, false);
				}
			}

			var helmet = __instance.GetField<VisEquipment, GameObject>("m_helmetItemInstance");
			if (helmet != null)
			{
				if (!settings.HelmetVisible)
				{
					SetVisible(helmet, false);
				}
				else
				{
					helmet.transform.localScale = settings.HelmetScale;
					helmet.transform.localPosition = settings.HelmetOffset;
				}
			}

			// 武器位置合わせ
			float equipmentScale = settings.EquipmentScale;
			Vector3 equipmentScaleVector = new Vector3(equipmentScale, equipmentScale, equipmentScale);
			
			var leftItem = __instance.GetField<VisEquipment, GameObject>("m_leftItemInstance");
			if (leftItem != null)
			{
				leftItem.transform.localPosition = settings.LeftHandItemPos;
				leftItem.transform.localScale = equipmentScaleVector;
			}

			var rightItem = __instance.GetField<VisEquipment, GameObject>("m_rightItemInstance");
			if (rightItem != null)
			{
				rightItem.transform.localPosition = settings.RightHandItemPos;
				rightItem.transform.localScale = equipmentScaleVector;
			}
			
			// divided  by 100 to keep the settings file positions in the same number range. (position offset appears to be on the world, not local)
			var rightBackItem = __instance.GetField<VisEquipment, GameObject>("m_rightBackItemInstance");
			if (rightBackItem != null)
			{
				var rightBackName = Utils.GetField<VisEquipment>("m_rightBackItem").GetValue(__instance);
				var isKnife = rightBackName.ToString().Substring(0, 5) == "Knife";
				var isStaff = rightBackName.ToString().Substring(0, 5) == "Staff";
				
				Vector3 offset = Vector3.zero;
				
				if (isKnife)
				{
					offset = settings.KnifeSidePos;
					rightBackItem.transform.Rotate(settings.KnifeSideRot);
				} else if (isStaff)
				{
					offset = settings.StaffPos;
					rightBackItem.transform.Rotate(settings.StaffRot);
				}
				else
				{
					offset = rightBackItem.transform.parent == __instance.m_backTool ? settings.RightHandBackItemToolPos : settings.RightHandBackItemPos;
				}

				rightBackItem.transform.localPosition = offset / 100.0f;
				rightBackItem.transform.localScale = equipmentScaleVector / 100.0f;
			}
			
			var leftBackItem = __instance.GetField<VisEquipment, GameObject>("m_leftBackItemInstance");
			if (leftBackItem != null)
			{
				
				var leftBackName = Utils.GetField<VisEquipment>("m_leftBackItem").GetValue(__instance);
				//Debug.Log(leftBackName.ToString());

				var isBow = leftBackName.ToString().Substring(0, 3) == "Bow";
				var isStaffSkeleton = leftBackName.ToString() == "StaffSkeleton";
				if (isBow)
				{
					leftBackItem.transform.localPosition = settings.BowBackPos / 100.0f;
				} else if (isStaffSkeleton)
				{
					leftBackItem.transform.localPosition = settings.StaffSkeletonPos / 100.0f;
				}
				else
				{
					leftBackItem.transform.localPosition = settings.LeftHandBackItemPos / 100.0f;
				}
				leftBackItem.transform.localScale = equipmentScaleVector / 100.0f;
			}
		}

		private static void SetVisible(GameObject obj, bool flag)
		{
			foreach (var mr in obj.GetComponentsInChildren<MeshRenderer>()) mr.enabled = flag;
			foreach (var smr in obj.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.enabled = flag;
		}
	}

	[HarmonyPatch(typeof(Humanoid), "OnRagdollCreated")]
	static class Patch_Humanoid_OnRagdollCreated
	{
		[HarmonyPostfix]
		static void Postfix(Humanoid __instance, Ragdoll ragdoll)
		{
			if (__instance is Player player)
			{
				foreach (var smr in ragdoll.GetComponentsInChildren<SkinnedMeshRenderer>())
				{
					smr.forceRenderingOff = true;
					smr.updateWhenOffscreen = true;
				}
			

				var ragAnim = ragdoll.gameObject.AddComponent<Animator>();
				ragAnim.keepAnimatorStateOnDisable = true;
				ragAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

				var orgAnim = (player.GetField<Player, Animator>("m_animator"));
				ragAnim.avatar = orgAnim.avatar;

				if (VrmManager.PlayerToVrmInstance.TryGetValue(player, out var vrm))
				{
					vrm.transform.SetParent(ragdoll.transform);
					vrm.GetComponent<VRMAnimationSync>().Setup(ragAnim, Settings.GetSettings(VrmManager.PlayerToName[player]), true);
				}
			}
		}
	}
	
	[HarmonyPatch(typeof(Character), "SetVisible")]
	static class Patch_Character_SetVisible
	{
		[HarmonyPostfix]
		static void Postfix(Character __instance, bool visible)
		{
			if (!__instance.IsPlayer()) return;
			// Make sure to perform a null check before accessing VRM components
			if (VrmManager.PlayerToVrmInstance.TryGetValue((Player)__instance, out var vrm) && vrm != null)
			{
				// Avoid reference errors by using a null check for LODGroup
				var lodGroup = vrm.GetComponent<LODGroup>();
				if (lodGroup != null)
				{
					if (visible)
					{
						lodGroup.localReferencePoint = __instance.GetField<Character, Vector3>("m_originalLocalRef");
					}
					else
					{
						lodGroup.localReferencePoint = new Vector3(999999f, 999999f, 999999f);
					}
				}
				else
				{
					// Log or handle the case where LODGroup is null
					// Example: Debug.LogError("LODGroup is null for VRM instance.");
				}
			}
			else
			{
				// Log or handle the case where VRM instance is null
			}
        }
	}

    [HarmonyPatch(typeof(Player), "OnDeath")]
	static class Patch_Player_OnDeath
	{
		[HarmonyPostfix]
		static void Postfix(Player __instance)
		{
			string name = null;
			if (VrmManager.PlayerToName.ContainsKey(__instance)) name = VrmManager.PlayerToName[__instance];
			if (name != null && Settings.GetSettings(name).FixCameraHeight)
			{
				GameObject.Destroy(__instance.GetComponent<VRMEyePositionSync>());
			}
		}
	}

	[HarmonyPatch(typeof(Character), "GetHeadPoint")]
	static class Patch_Character_GetHeadPoint
	{
		[HarmonyPostfix]
		static bool Prefix(Character __instance, ref Vector3 __result)
		{
			var player = __instance as Player;
			if (player == null) return true;

			if (VrmManager.PlayerToVrmInstance.TryGetValue(player, out var vrm))
			{
				var animator = vrm.GetComponentInChildren<Animator>();
				if (animator == null) return true;

				var head = animator.GetBoneTransform(HumanBodyBones.Head);
				if (head == null) return true;

				__result = head.position;
				return false;
			}
			
			return true;
		}
	}
	

	// Remove stealth factor check, show stealth hud only if crouching
	[HarmonyPatch(typeof(Hud), "UpdateStealth")]
	public static class Patch_Hud_UpdateStealth
	{
		[HarmonyReversePatch()]
		static void Postfix(Hud __instance, Player player, float bowDrawPercentage)
		{
			if (player.IsCrouching() && (double) bowDrawPercentage == 0.0)
			{
				if (player.IsSensed())
				{
					__instance.m_targetedAlert.SetActive(true);
					__instance.m_targeted.SetActive(false);
					__instance.m_hidden.SetActive(false);
				}
				else if (player.IsTargeted())
				{
					__instance.m_targetedAlert.SetActive(false);
					__instance.m_targeted.SetActive(true);
					__instance.m_hidden.SetActive(false);
				}
				else
				{
					__instance.m_targetedAlert.SetActive(false);
					__instance.m_targeted.SetActive(false);
					__instance.m_hidden.SetActive(true);
				}
				__instance.m_stealthBar.gameObject.SetActive(true);
				__instance.m_stealthBar.SetValue(player.GetStealthFactor());
			}
			else
			{
				__instance.m_targetedAlert.SetActive(false);
				__instance.m_hidden.SetActive(false);
				__instance.m_targeted.SetActive(false);
				__instance.m_stealthBar.gameObject.SetActive(false);
			}
		}
	}

	[HarmonyPatch(typeof(Humanoid), "StartAttack")]
	static class Patch_Humanoid_StartAttack
	{
		[HarmonyPostfix]
		static void Postfix(Humanoid __instance, Character target, bool secondaryAttack)
		{
			if (__instance is Player player)
			{
				ref Attack attack = ref AccessTools.FieldRefAccess<Player, Attack>("m_currentAttack").Invoke(player);

				if (attack == null) return;
				
				ref float time = ref AccessTools.FieldRefAccess<Attack, float>("m_time").Invoke(attack);
				if (time != 0) return;
				
				string playerName;
				if (VrmManager.PlayerToName.TryGetValue(player, out playerName))
				{
					if (Settings.ContainsSettings(playerName))
					{
						var settings = Settings.GetSettings(playerName);
						attack.m_attackRange *= settings.AttackDistanceScale;
						
						//TODO: find out if this should be removed.
						attack.m_attackHeight *= settings.PlayerHeight / 1.85f;
						attack.m_attackOffset *= settings.PlayerHeight / 1.85f;
						
						//var anim = attack.GetField<Attack, ZSyncAnimation>("m_zanim");
						//var nview = anim.GetField<ZSyncAnimation, ZNetView>("m_nview");
						//ref var animSpeedID = ref AccessTools.StaticFieldRefAccess<ZSyncAnimation, int>("m_animSpeedID");
						//nview.GetZDO().Set(animSpeedID, nview.GetZDO().GetFloat(animSpeedID) * settings.AttackSpeedScale);
						//var animator = anim.GetField<ZSyncAnimation, Animator>("m_animator");
						//animator.speed *= settings.AttackSpeedScale;
						
					}
				}
			}
		}
	}

	[HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
	static class Patch_Player_UpdatePlacementGhost
	{
		[HarmonyPostfix]
		static void UpdatePlacementGhost(Player __instance, bool flashGuardStone)
		{
			var fi = typeof(Player).GetField("m_placementStatus", BindingFlags.NonPublic | BindingFlags.Instance);
			var val = (int)fi.GetValue(__instance);
			if (val == 10)
			{
				fi.SetValue(__instance, 0);
			}
		}
	}

	[HarmonyPatch(typeof(Game), "SpawnPlayer")]
	static class Patch_Game_SpawnPlayer
	{
		[HarmonyPostfix]
		static void Postfix(Game __instance, bool ___m_firstSpawn, Player __result)
		{
			if (___m_firstSpawn)
			{
				__result.GetComponent<VrmController>().ShareVrm();
				__result.GetComponent<VrmController>().QueryAllVrm();
			}
		}
	}
	
	[HarmonyPatch(typeof(Player), "OnDestroy")]
	static class Patch_Player_OnDestroy
	{
		[HarmonyPostfix]
		static void Postfix(Player __instance)
		{
			VrmManager.PlayerToName.Remove(__instance);
            VrmManager.PlayerToVrmInstance.Remove(__instance);
		}
	}
	

	[HarmonyPatch(typeof(Player), "Awake")]
	static class Patch_Player_Awake
	{
		[HarmonyPostfix]
		static void Postfix(Player __instance, ZNetView ___m_nview)
		{
			Commands.Trigger();

			string playerName = null;
			string localPlayerName = null;
			if (Game.instance != null)
			{
				localPlayerName = Game.instance.GetPlayerProfile().GetName();
				playerName = __instance.GetPlayerName();
				if (playerName == "" || playerName == "...") playerName = localPlayerName;
			}
			else
			{
				var index = FejdStartup.instance.GetField<FejdStartup, int>("m_profileIndex");
				var profiles = FejdStartup.instance.GetField<FejdStartup, List<PlayerProfile>>("m_profiles");
				if (index >= 0 && index < profiles.Count) playerName = profiles[index].GetName();
				localPlayerName = playerName;
			}
			
			VrmManager.PlayerToName[__instance] = playerName;
			
			bool isInMenu = __instance.gameObject.scene.name == "start";

			if (isInMenu)
			{
				var names = new List<string>(VrmManager.VrmDic.Keys);
				foreach (var name in names)
				{
					var vrm = VrmManager.VrmDic[name];
					if (vrm.Source == VRM.SourceType.Shared)
					{
						VrmManager.VrmDic.Remove(name);
						Settings.RemoveSettings(name);
					}
				}

                VrmController.CleanupLoadings();
			}

			bool online = ___m_nview.GetZDO() != null;

			var vrmController = __instance.gameObject.AddComponent<VrmController>();

			if (!string.IsNullOrEmpty(playerName))
			{
				bool settingsUpdated = false;
				
				var path = Path.Combine(Environment.CurrentDirectory, "ValheimVRM", $"{playerName}.vrm");
				var sharedPath = Path.Combine(Environment.CurrentDirectory, "ValheimVRM", "Shared", $"{playerName}.vrm");

				if (!Settings.ContainsSettings(playerName) || Settings.globalSettings.ReloadInMenu && isInMenu)
				{
					if (File.Exists(path))
					{
						Settings.AddSettingsFromFile(playerName, false);
						settingsUpdated = true;
					}
					else if (File.Exists(sharedPath))
					{
						Settings.AddSettingsFromFile(playerName, true);
						settingsUpdated = true;
					}
				}

				VRM vrm = null;
				
				var settings = Settings.GetSettings(playerName);

				if (settings != null)
				{
					if (!VrmManager.VrmDic.ContainsKey(playerName) || Settings.globalSettings.ReloadInMenu && isInMenu)
					{
						if (File.Exists(path))
						{
							
							
							if (localPlayerName == playerName)
							{
								var bytes = File.ReadAllBytes(path);
								var vrmVisual  =  VRM.ImportVisual(bytes, path, settings.ModelScale);
								if (vrmVisual != null)
								{
									vrm = CreateVrm(vrmVisual, __instance, bytes, playerName );
								}
				
							}
							else
							{
								CoroutineHelper.Instance.StartCoroutine(LoadVrm(__instance, playerName, localPlayerName, path, settings.ModelScale, settingsUpdated, settings, false));
							}
							
							
						}
						else if (File.Exists(sharedPath))
						{ // isShared true
							
							if (localPlayerName == playerName) // i do not think sharing is implmented, even if it is, i dont think there can be a local player instance here
							                                   // or at least shouldn't be.
							{
								var bytes = File.ReadAllBytes(sharedPath);
								var vrmVisual  =  VRM.ImportVisual(bytes, sharedPath, settings.ModelScale);
								if (vrmVisual != null)
								{
									vrm = CreateVrm(vrmVisual, __instance, bytes, playerName, true );
								}
							}
							else
							{
								CoroutineHelper.Instance.StartCoroutine(LoadVrm(__instance, playerName,localPlayerName, sharedPath, settings.ModelScale, settingsUpdated, settings,true));

							}
							
						}
						else
						{ //default character stuff
							if (!VrmManager.VrmDic.ContainsKey("___Default"))
							{
								var defaultPath = Path.Combine(Environment.CurrentDirectory, "ValheimVRM", "___Default.vrm");

								if (File.Exists(defaultPath))
								{
									
									if (localPlayerName == playerName)
									{
										var bytes = File.ReadAllBytes(defaultPath);
										var vrmVisual  =  VRM.ImportVisual(bytes, defaultPath, settings.ModelScale);
										if (vrmVisual != null)
										{
											vrm = CreateVrm(vrmVisual, __instance, bytes, playerName);
										}
				
									}
									else
									{
										CoroutineHelper.Instance.StartCoroutine(LoadVrm(__instance, "___Default", localPlayerName, defaultPath, settings.ModelScale, settingsUpdated, settings, false));
									}
									
									
								}
							}
							else
							{
								vrm = VrmManager.VrmDic["___Default"];
							}
		
						}

					}
					else
					{
						vrm = VrmManager.VrmDic[playerName];
					}
				}
				else
				{
					Debug.LogError("Settings are still null");
				}

				SetVrm(__instance, vrm, settingsUpdated);
			}
		}

		private static void SetVrm(Player player, VRM vrm, bool settingsUpdated)
		{
			if (vrm != null)
			{
				if (settingsUpdated)
				{
					vrm.RecalculateSettingsHash();
				}
 

				CoroutineHelper.Instance.StartCoroutine(vrm.SetToPlayer(player));
			}
		}
		
		static VRM CreateVrm(GameObject vrmVisual, Player player, byte[] bytes, string name, bool isShared = false)
		{
			VRM vrm = new VRM(vrmVisual, name);
			vrm = VrmManager.RegisterVrm(vrm, player.GetComponentInChildren<LODGroup>(), player);
			
			if (vrm != null)
			{
				vrm.Src = bytes;
				vrm.RecalculateSrcBytesHash();

				if (isShared)
				{
					vrm.Source = VRM.SourceType.Shared;
				}
			}
			
			return vrm;
		}
		
		
		private static IEnumerator LoadVrm(Player player, string playerName, string localPlayerName, string path, float scale, bool settingsUpdated, Settings.VrmSettingsContainer settings, bool isShared = false)
		{

 
			Task<byte[]> bytesTask = Task.Run(() => File.ReadAllBytes(path));
			
			while (!bytesTask.IsCompleted)
			{
				yield return new WaitUntil(() => bytesTask.IsCompleted);
			}

			if (bytesTask.IsFaulted)
			{
				Debug.LogError($"Error loading VRM: {bytesTask.Exception.Flatten().InnerException}");
				yield break;
			}
			
			yield return player.StartCoroutine(VRM.ImportVisualAsync(bytesTask.Result, path, settings.ModelScale, loadedRoot =>
			{
				if (loadedRoot != null)
				{
					var vrm = CreateVrm(loadedRoot, player, bytesTask.Result, playerName, isShared);
					if (vrm != null)
					{
						SetVrm(player, vrm, settingsUpdated);
					}
				}
			}));

			// var loadTask = VRM.ImportVisualAsync(bytesTask.Result, path, settings.ModelScale);
			//
			//
			// while (!loadTask.IsCompleted)
			// {
			// 	yield return new WaitUntil(() => loadTask.IsCompleted);
			// }
			//
			// if (loadTask.IsFaulted)
			// {
			// 	Debug.LogError($"Error loading VRM: {loadTask.Exception.Flatten().InnerException}");
			// 	yield break;
			// }
			//
			//
			// if (loadTask.Result != null)
			// {
			// 	var vrm = CreateVrm(loadTask.Result, player, bytesTask.Result, playerName, isShared);
			// 	if (vrm != null)
			// 	{
			// 		SetVrm(player, vrm, settingsUpdated);
			// 	}
			// }
			
		}


		
 

	}
}
