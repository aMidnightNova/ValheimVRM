# ValheimVRM

**This mod requires BepInEx to be installed.**

This fork is actively developed and maintained. If you need help, want to request a feature, or found a bug; Head on over to the [discord](https://discord.gg/q3wuVMCvXE).

### How to install 
[Download](https://github.com/aMidnightNova/ValheimVRM/releases/latest) the latest release and extract it. There will be a folder called release, copy the folders inside (BepInEx,valheim_Data) into your valheim install directory.
The folders are setup to put the files where they need to go.


### Settings File
The name of the character in the game needs to correspond to a VRM and settings file like so.

**Character**: Midnight Nova \
**Settings File**: Midnight Nova_settings.txt \
**VRM**: Midnight Nova.vrm

### Settings (armorswap)
When you swap armor in game, if your model has the below blendshapes or BlendShapeClips the mod will swap to that armor set
once you equip that to the chest that style of armor.

- if you use a BlendShapeClip, you can set what ever values are needed to make your armor work..
  however if you use a normal Blendshape. A value of 100 will be assigned to it for its max weight
  and will toggle between 0 and 100 for hidden and shown.
 
```txt
v_armorswap_rag
v_armorswap_leather
v_armorswap_troll
v_armorswap_root
v_armorswap_bronze
v_armorswap_iron
v_armorswap_fenris
v_armorswap_wolf
v_armorswap_padded
v_armorswap_etirweave
v_armorswap_carapace
```


### Default Settings and avatar for people you do not have custom stuff for.

You can have Default settings now, to use this. create a settings___Default.txt file, and pair
___Default.vrm with it. \
**NOTE:** settings___Default.txt has 4 underscores, and ___Default.vrm has 3.

### Usefull Info
- If you have a shader compile error you probably need to use the old shader bundle. \
  the newer current bundle should work, but JIC ive included the old one still\
  Its in General settings. UseShaderBundle=old,current. Note that this will affect all models.
- Export as VRM 0, Under blendshapes check "keep animations" if you want facial expressions to work\
  (future release) without that checked they will not be exported

### Technical Stuff for maintaining this repo
- You might need to build an Asset Bundle of shaders to stay inline with UniVrm. This is probably a non issue
  unless Valheim Updates Unity. - see next point.
- Current UniVrm version is 111, which is the last version that supports Unity 2020.
- Most Recent AssetBundle of shaders is UniVrm.shaders. This has shaders that are required since like version 70.
- You will need to install UniVrm into a blank project (create the shader asset bundle there too)
  once that's done, you will need to build the Unity Project. Find the Managed folder and set that
  as a system Path. - **VALHEIM_UNITY_LIBS**
- Set your Valheim Folder as a system path. **VALHEIM_INSTALL**
- Make sure in Unity you have Mono 4.x selected as the target Dot Net version.