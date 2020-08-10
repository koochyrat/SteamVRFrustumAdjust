# SteamVRFrustumAdjust
For canted headsets like Pimax, calculate proper culling matrix to avoid objects being culled too early at far edges in Unity. Prevents objects popping in and out of view.

It is a general fix for all headsets with canted displays in non-parallel projection mode. The cause is that Unity calculates the culling matrices without accounting for the eye rotation angle, causing the horizontal FOV to come up 20 degrees short in the case of Pimax.

In addition, due to the canting, the vertical FOV is not constant and actually increases towards the outer edges. See https://risa2000.github.io/vrdocs/docs/hmd_fov_calculation.html for explanation. This increased vertical FOV needs to be accounted for in calculating the culling matrix otherwise premature culling will happen at the top/bottom corners near the outer edges as well.

<h2>How to use</h2>

<b>Built-in renderer legacy OpenVR Single Pass, Single Pass Instanced or Multi Pass:</b>
<br>Add SteamVRFrustumAdjust.cs script to your eye camera

<b>Built-in renderer Unity XR OpenVR Single Pass Instanced:</b>
<br>Add SteamVRFrustumAdjust.cs script to your eye camera

<b>Built-in renderer Unity XR OpenVR Multi pass:</b>
<br>No fix necessary as Unity uses two cameras for culling

<b>HDRP/URP renderer legacy OpenVR Single Pass, Single Pass Instanced or Multi Pass:</b>
<br>Add SteamVRFrustumAdjustSRP.cs script to your eye camera

<b>HDRP/URP renderer Unity XR OpenVR Single Pass Instanced or Multi Pass:</b>
<br>Add SteamVRFrustumAdjustSRP.cs script to your eye camera

When the camera is enabled, the proper culling matrix for both horizontal and vertical FOV will be calculated and then applied on each frame when it is enabled. You do not have to check for Pimax hardware, the script will automatically activate if it detects a canted view. So far the Pimax, StarVR, Index in non parallel projection mode, and HP Reverb can have canting.

I have submitted a bug report to Unity, but their response is that they will not fix it as Pimax is not a supported headset and they have no plans to support it. So this is the only way to fix it for the foreseeable future.  

Additional files are included in this repository where all the math stuff was done to derive the vertical culling FOV just for reference.

Special thanks to risa2000 for help https://risa2000.github.io/hmdgdb/

StarVR's official fix using this code:
https://www.starvr.com/developers_stage/7/30/

dmel642's Unity game mods using this code:
https://github.com/dmel642/UnityCullingFix
