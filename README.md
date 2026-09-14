### This fork of ValheimVRM is no longer maintained, is has been replaced by:

# New Version: [EnhancedValheimVRM](https://github.com/aMidnightNova/EnhancedValheimVRM)

If you have questions, find us on [discord](https://discord.gg/q3wuVMCvXE).

---

### Notes
If the model has MToon shaders you should not have ssao on, this is true even if you do not have MToon stuff enabled in the settings. You can set AttemptTextureFix to true, it will convert shaders to standard at game runtime.

### How to Install
[Download](https://github.com/aMidnightNova/ValheimVRM/releases/latest) the latest release and extract it. There will be a folder called release, copy the folders inside (BepInEx,valheim_Data,ValheimVRM) into your valheim install directory.
The folders are setup to put the files where they need to go.

### First Time Install
- make sure to copy / rename all files that end with .example into the correct corresponding file. E.G. \
 global_settings.txt.example -> global_settings.txt.

### File Install Locations
The VRM and settings files should be in the ValheimVRM folder inside the Valheim game dir.

### Settings File
The name of the character in the game needs to correspond to a VRM and settings file like so.

**Character**: Midnight Nova \
**Settings File**: settings_Midnight Nova.txt \
**VRM**: Midnight Nova.vrm


### Default Settings and avatar for people you do not have custom stuff for.

**Settings File**: settings____Default.txt \
**VRM**: ___Default.vrm

**NOTE:** settings____Default.txt has 4 underscores, and ___Default.vrm has 3.

### Usefull Info
- If you have a shader compile error you probably need to use the old shader bundle. \
  the newer current bundle should work, but JIC ive included the old one still\
  Its in General settings. UseShaderBundle=<old,current>. Note that this will affect all models.


# Stop Here
unless you wish to develop this repo, then the following info will help you get started.

### Technical Stuff for maintaining this repo
- You might need to build an Asset Bundle of shaders to stay inline with UniVrm. This is probably a non issue
  unless Valheim Updates Unity. - see next point.
- Current UniVrm version is 121, for Unity 2022. UniVrm was 111 previous to  Valheim Patch 0.217.46. 111 is the last version to support Unity 2020.
- Most Recent AssetBundle of shaders is UniVrm.shaders. This has shaders that are required since version 67 - 70(I dont know exactly when).
- You will need to install UniVrm into a blank project (create the shader asset bundle there too)
  once that's done(install from git the assetBundle Browser), you will need to build the Unity Project. Find the (build folder)_Data and set that 
  as a system Path. I called my project "UniVrm v121" so the data folder would be UniVrm v121_Data - **VALHEIM_UNITY_LIBS**
- inside your UniVrm Project you will need to install UnityAsyncImageLoader https://github.com/aMidnightNova/UnityAsyncImageLoader
- Set your Valheim Folder as a system path. **VALHEIM_INSTALL**


- If for whatever reason you are targeting 111 still, Make sure in Unity you have Mono  and .NET 4.x selected.
