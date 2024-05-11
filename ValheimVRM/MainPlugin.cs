﻿using System.Globalization;
using BepInEx;
using HarmonyLib;
using System.Reflection;

#if DEBUG

using System.Diagnostics;
using System.Threading;

#endif

namespace ValheimVRM
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("valheim.exe")]
    public class MainPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.yoship1639.plugins.valheimvrm";
        public const string PluginName = "ValheimVRM";
        public const string PluginVersion = "1.2.3.0";

        void Awake()
        {
            
// #if DEBUG
//             int attempts = 0;
//             while (!Debugger.IsAttached && attempts < 150) // let's timeout after 30 seconds
//             {
//                 Thread.Sleep(100); // wait for 100ms
//                 attempts++;
//
//                 if(attempts % 10 == 0) // log every second
//                 {
//                     Logger.LogInfo("Waiting for debugger attachment...");
//                 }
//             }
//             
//             if(attempts >= 300)
//             {
//                 Logger.LogError("Timed out waiting for debugger attachment!");
//             }
// #endif


            
            // avoid float parsing error on computers with different cultures
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            
            // we have some global settings to load
            Settings.ReloadGlobalSettings();
            
            // a semi hacky way of loading a default character, no one can name a character with and underscore as far as i am aware. 
            Settings.AddSettingsFromFile("___Default", false);
            
            // Harmonyパッチ作成
            var harmony = new Harmony("com.yoship1639.plugins.valheimvrm.patch");

            // Harmonyパッチ全てを適用する
            harmony.PatchAll();
            if(Settings.globalSettings.EnableProfileCode) PatchAllUpdateMethods.ApplyPatches(harmony);

            // MToonシェーダ初期化
            VRMShaders.Initialize();
        }
    }
}
