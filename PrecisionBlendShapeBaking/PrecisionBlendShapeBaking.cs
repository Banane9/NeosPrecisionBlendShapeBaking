using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseX;
using FrooxEngine;
using HarmonyLib;
using NeosModLoader;

namespace PrecisionBlendShapeBaking
{
    public class PrecisionBlendShapeBaking : NeosMod
    {
        public override string Author => "Banane9";
        public override string Link => "https://github.com/Banane9/NeosPrecisionBlendShapeBaking";
        public override string Name => "PrecisionBlendShapeBaking";
        public override string Version => "1.0.0";

        public override void OnEngineInit()
        {
            Harmony harmony = new Harmony($"{Author}.{Name}");
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(GlueTip))]
        private static class GlueTipPatches
        {
            [HarmonyPostfix]
            [HarmonyPatch(nameof(GlueTip.OnSecondaryPress))]
            private static void OnSecondaryPressPostfix(GlueTip __instance)
            {
                if ((__instance.GlueMode.Value != Glue.Mode.BakeSkinnedMeshes && __instance.GlueMode.Value != Glue.Mode.BakeMeshes)
                 || !__instance.ActiveTool.Grabber.IsHoldingObjects
                 || !(__instance.ActiveTool.Grabber.HolderSlot.GetComponentInChildren<ReferenceProxy>() is ReferenceProxy proxy)
                 || !(proxy.Reference.Target is Sync<float> field) || field.IsDriven
                 || !(field.FindNearestParent<SkinnedMeshRenderer>() is SkinnedMeshRenderer renderer)
                 || !(renderer.Mesh.Target is StaticMesh staticMesh))
                    return;

                var point = proxy.Slot.GlobalPosition;
                proxy.Slot.Destroy();

                var blendshape = renderer.GetBlendshapeWeights(f => f == field);

                if (blendshape.Count == 0)
                {
                    __instance.Debug.Text(point, "Couldn't find blend shape entry.", duration: 3);
                    return;
                }

                __instance.Debug.Text(point + new float3(0, .013f, 0), $"Baking {renderer.BlendShapeName(blendshape.Keys.First())}...", duration: 5);
                __instance.StartTask(async () =>
                {
                    await default(ToBackground);

                    var locker = new object();
                    await staticMesh.Asset.RequestReadLock(locker).ConfigureAwait(false);

                    var mesh = new MeshX(staticMesh.Asset.Data);
                    staticMesh.Asset.ReleaseReadLock(locker);

                    mesh.BakeBlendShapes(blendshape);

                    var uri = await __instance.Engine.LocalDB.SaveAssetAsync(mesh);
                    if (uri == null)
                    {
                        __instance.Debug.Text(point, "Couldn't save asset.", duration: 3);
                        return;
                    }

                    await default(ToWorld);

                    staticMesh.URL.Value = uri;
                    foreach (var i in blendshape.Keys.OrderByDescending(i => i))
                        renderer.BlendShapeWeights.RemoveAt(i);

                    __instance.Debug.Text(point, "Done!", duration: 2);
                });
            }
        }
    }
}