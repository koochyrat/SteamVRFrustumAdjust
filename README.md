# SteamVRFrustumAdjust
For canted headsets like Pimax, calculate proper culling matrix to avoid objects being culled too early at far edges in Unity. Prevents objects popping in and out of view.

It is a general fix for all headsets with canted displays in non-parallel projection mode. The conclusion is that Unity calculates the projection and culling matrices without accounting for the eye rotation angle, causing it to come up 20 degrees short in the case of Pimax.

See linked thread here as well: https://community.openmr.ai/t/this-is-what-vr-game-developers-need-to-add-to-remove-clipping-culling-in-peripheral-view-on-pimax/26544/11
