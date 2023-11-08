using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using HarmonyLib;
using UniGLTF;
using UnityEngine;
using VRM;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace ValheimVRM
{
    public class VRM
	{
		public enum SourceType
		{
			Local,  // my VRM from my computer
			Shared // VRM, downloaded from other player
		}
		
		public GameObject VisualModel { get; private set; }
		public byte[] Src;
		public byte[] SrcHash;
		public byte[] SettingsHash;
		public string Name { get; private set; }
		public SourceType Source = SourceType.Local;

		public VRM(GameObject visualModel, string name)
		{
			VisualModel = visualModel;
			Name = name;
		}

		~VRM()
		{
			if (VisualModel != null)
			{
				Object.Destroy(VisualModel);
			}
		}

		public void RecalculateSrcBytesHash()
		{
			using (var md5 = System.Security.Cryptography.MD5.Create())
			{
				SrcHash = md5.ComputeHash(Src);
			}
		}

		public void RecalculateSettingsHash()
		{
			using (var md5 = System.Security.Cryptography.MD5.Create())
			{
				byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(Settings.GetSettings(Name).ToStringDiffOnly());
				SettingsHash = md5.ComputeHash(inputBytes);
			}
		}
		
		public static GameObject ImportVisual(string path, float scale)
		{
			Debug.Log("[ValheimVRM] loading vrm from file, " + new FileInfo(path).Length + " bytes");
			Debug.Log("[ValheimVRM] vrm file path: " + path);
			
			try
			{
				var data = new GlbFileParser(path).Parse();
				var vrm = new VRMData(data);
				var context = new VRMImporterContext(vrm);
				var loaded = default(RuntimeGltfInstance);
				
				try
				{
					loaded = context.Load();
				}
				catch (TypeLoadException ex)
				{
					Debug.LogError("Failed to load type: " + ex.TypeName);
					Debug.LogError(ex);
				}
				
				loaded.ShowMeshes();

				loaded.Root.transform.localScale = Vector3.one * scale;

				Debug.Log("[ValheimVRM] VRM read successful");

				return loaded.Root;
			}
			catch (Exception ex)
			{
				Debug.LogError(ex);
			}

			return null;
		}

		public static GameObject ImportVisual(byte[] buf, string path, float scale)
		{
			Debug.Log("[ValheimVRM] loading vrm from memory, " + buf.Length + " bytes");
			
			try
			{
				var data = new GlbBinaryParser(buf, path).Parse();
				var vrm = new VRMData(data);
				var context = new VRMImporterContext(vrm);
				var loaded = default(RuntimeGltfInstance);
				
				try
				{
					loaded = context.Load();
				}
				catch (TypeLoadException ex)
				{
					Debug.LogError("Failed to load type: " + ex.TypeName);
					Debug.LogError(ex);
				}
				
				loaded.ShowMeshes();

				loaded.Root.transform.localScale = Vector3.one * scale;

				Debug.Log("[ValheimVRM] VRM read successful");

				return loaded.Root;
			}
			catch (Exception ex)
			{
				Debug.LogError(ex);
			}

			return null;
		}
		
		public void SetToPlayer(Player player)
		{
			var settings = Settings.GetSettings(Name);
			player.m_maxInteractDistance *= settings.InteractionDistanceScale;
 
			var vrmModel = Object.Instantiate(VisualModel);
			VrmManager.PlayerToVrmInstance[player] = vrmModel;
			vrmModel.name = "VRM_Visual";
			vrmModel.SetActive(true);
			player.GetComponent<VrmController>().visual = vrmModel;

			var oldModel = player.GetComponentInChildren<Animator>().transform.parent.Find("VRM_Visual");
			if (oldModel != null)
			{
				Object.Destroy(oldModel);
			}
			
			vrmModel.transform.SetParent(player.GetComponentInChildren<Animator>().transform.parent, false);

			float newHeight = settings.PlayerHeight;
			float newRadius = settings.PlayerRadius;

			var collider = player.gameObject.GetComponent<CapsuleCollider>();
			collider.height = newHeight;
			collider.radius = newRadius;
			collider.center = new Vector3(0, newHeight / 2, 0);

			player.GetComponent<Rigidbody>().centerOfMass = collider.center;

			foreach (var smr in player.GetVisual().GetComponentsInChildren<SkinnedMeshRenderer>())
			{
				smr.forceRenderingOff = true;
				smr.updateWhenOffscreen = true;
			}

			var orgAnim = AccessTools.FieldRefAccess<Player, Animator>(player, "m_animator");
			orgAnim.keepAnimatorStateOnDisable = true;
			orgAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

			vrmModel.transform.localPosition = orgAnim.transform.localPosition;

			// Synchronizing animation
			var animSync = vrmModel.GetComponent<VRMAnimationSync>() ?? vrmModel.AddComponent<VRMAnimationSync>();
			animSync.Setup(orgAnim, settings, false);

			// Adjusting camera position
			if (settings.FixCameraHeight)
			{
				var vrmEye = vrmModel.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.LeftEye) 
				             ?? vrmModel.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head) 
				             ?? vrmModel.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Neck);

				if (vrmEye != null)
				{
					var eyePosSync = player.gameObject.GetComponent<VRMEyePositionSync>() ?? player.gameObject.AddComponent<VRMEyePositionSync>();
					eyePosSync.Setup(vrmEye);
				}
			}

			// Reflecting ambient light's effect on color when using MToon
			if (settings.UseMToonShader)
			{
				var mToon = vrmModel.GetComponent<MToonColorSync>() ?? vrmModel.AddComponent<MToonColorSync>();
				mToon.Setup(vrmModel);
			}

			if (settings.UseBlendshapeArmorSwap)
			{
				var blendShapeSync = vrmModel.GetComponent<VRMBlendShapeSync>() ?? vrmModel.AddComponent<VRMBlendShapeSync>();
				blendShapeSync.Setup(vrmModel);
			}

			
			// SpringBone設定
			foreach (var springBone in vrmModel.GetComponentsInChildren<VRMSpringBone>())
			{
				springBone.m_stiffnessForce *= settings.SpringBoneStiffness;
				springBone.m_gravityPower *= settings.SpringBoneGravityPower;
				springBone.m_updateType = VRMSpringBone.SpringBoneUpdateType.FixedUpdate;
				springBone.m_center = null;
			}
			
			player.GetComponent<VrmController>().ReloadSpringBones();
        }
    }
}