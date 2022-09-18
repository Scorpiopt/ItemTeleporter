using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using Verse;
using Verse.AI;
using Verse.Noise;

namespace ItemTeleporter
{
    [StaticConstructorOnStartup]
    public static class ItemTeleporterStartup
    {
        static ItemTeleporterStartup()
        {
            new Harmony("ItemTeleporter.Mod").PatchAll();
        }
    }


    //[HarmonyPatch(typeof(GenSpawn), "Spawn", new System.Type[] { typeof(Thing), typeof(IntVec3), typeof(Map), typeof(Rot4), typeof(WipeMode), typeof(bool) })]
    //public static class GenSpawn_Spawn_Patch
    //{
    //    public static void Postfix(Thing __result, bool respawningAfterLoad)
    //    {
    //        if (Building_ItemTeleporter.buildings.TryGetValue(__result.Map, out var list))
    //        {
    //            if (__result is Plant || __result is Building || __result is UnfinishedThing)
    //            {
    //                return;
    //            }
    //            if (!__result.Map.reservationManager.IsReservedByAnyoneOf(__result, Faction.OfPlayer)
    //                && !__result.Map.physicalInteractionReservationManager.IsReserved(__result))
    //            {
    //                var storages = list.Where(x => x.compPower.PowerOn).OrderByDescending(x => x.GetStoreSettings().Priority)
    //                    .ThenBy(x => x.Position.DistanceTo(__result.Position)).ToList();
    //                if (!__result.Position.GetThingList(__result.Map).Any(x => x is Building_ItemTeleporter))
    //                {
    //                    foreach (var storage in storages)
    //                    {
    //                        if (storage.GetStoreSettings().AllowedToAccept(__result))
    //                        {
    //                            var goodCells = storage.AllSlotCells().Where(x => StoreUtility.IsGoodStoreCell(x,
    //                                __result.Map, __result, null, Faction.OfPlayer));
    //                            if (goodCells.TryRandomElement(out var cell))
    //                            {
    //                                __result.Position = cell;
    //                                FleckMaker.ThrowLightningGlow(__result.DrawPos, __result.Map, 0.5f);
    //                                break;
    //                            }
    //                        }
    //                    }
    //                }
    //            }
    //        }
    //    }
    //}

    [HarmonyPatch(typeof(Pawn_JobTracker), "StartJob")]
    public static class Pawn_JobTracker_StartJob_Patch
    {
        public static bool Prefix(Pawn ___pawn, Job newJob, JobCondition lastJobEndCondition = JobCondition.None, ThinkNode jobGiver = null, bool resumeCurJobAfterwards = false, bool cancelBusyStances = true, ThinkTreeDef thinkTree = null, JobTag? tag = null, bool fromQueue = false, bool canReturnCurJobToPool = false)
        {
            if (newJob != null)
            {
                if (newJob.def == JobDefOf.DoBill && Building_ItemTeleporter.buildings.TryGetValue(___pawn.Map, out var list))
                {
                    var storagesAround = list.Where(x => x.compPower.PowerOn && x.billInterceptionEnabled &&
                        x.Position.DistanceTo(newJob.targetA.Thing.Position) <= 10f)
                        .OrderBy(x => x.Position.DistanceTo(newJob.targetA.Thing.Position)).ToList();
                    if (storagesAround.Any())
                    {
                        for (var i = 0; i < newJob.countQueue.Count; i++)
                        {
                            if (!newJob.targetQueueB[i].Thing.Position.GetThingList(newJob.targetA.Thing.Map)
                                .Any(x => x is Building_ItemTeleporter))
                            {
                                foreach (var storage in storagesAround)
                                {
                                    if (storage.GetStoreSettings().AllowedToAccept(newJob.targetQueueB[i].Thing))
                                    {
                                        var goodCells = storage.AllSlotCells().Where(x => StoreUtility.IsGoodStoreCell(x,
                                            newJob.targetA.Thing.Map, newJob.targetQueueB[i].Thing, ___pawn, ___pawn.Faction));
                                        if (goodCells.TryRandomElement(out var cell))
                                        {
                                            var thing = newJob.targetQueueB[i].Thing;
                                            if (thing.stackCount <= newJob.countQueue[i])
                                            {
                                                thing.Position = cell;
                                                FleckMaker.ThrowLightningGlow(thing.DrawPos, thing.Map, 0.5f);
                                            }
                                            else if (thing.stackCount > newJob.countQueue[i])
                                            {
                                                var newThing = newJob.targetQueueB[i].Thing.SplitOff(newJob.countQueue[i]);
                                                GenSpawn.Spawn(newThing, cell, thing.Map);
                                                newThing.Position = cell;
                                                newJob.targetQueueB[i] = newThing;
                                                FleckMaker.ThrowLightningGlow(newThing.DrawPos, newThing.Map, 0.5f);
                                            }
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else if (newJob.def == JobDefOf.HaulToCell 
                    && Building_ItemTeleporter.buildings.TryGetValue(___pawn.Map, out var list2))
                {
                    var thing = newJob.targetA.Thing;
                    var cellForHauling = newJob.targetB.Cell;
                    var slotGroup = cellForHauling.GetSlotGroup(___pawn.Map);
                    var allThings = thing.Position.GetThingList(thing.Map);
                    if (allThings.Any(x => x is Building_ItemTeleporter) is false)
                    {
                        var storagesAround = list2.Where(x => x.compPower.PowerOn && x.haulingInterceptionEnabled &&
                            (slotGroup is null || slotGroup.Settings.Priority < x.GetStoreSettings().Priority))
                            .OrderBy(x => x.Position.DistanceTo(thing.Position)).ToList();
                        if (storagesAround.Any())
                        {
                            foreach (var storage in storagesAround)
                            {
                                if (storage.GetStoreSettings().AllowedToAccept(newJob.targetA.Thing))
                                {
                                    var goodCells = storage.AllSlotCells().Where(x => StoreUtility.IsGoodStoreCell(x,
                                        thing.Map, thing, ___pawn, ___pawn.Faction));
                                    if (goodCells.TryRandomElement(out var cell))
                                    {
                                        if (thing.stackCount <= newJob.count)
                                        {
                                            thing.Position = cell;
                                            FleckMaker.ThrowLightningGlow(thing.DrawPos, thing.Map, 0.5f);
                                        }
                                        else if (thing.stackCount > newJob.count)
                                        {
                                            var newThing = thing.SplitOff(newJob.count);
                                            GenSpawn.Spawn(newThing, cell, thing.Map);
                                            newThing.Position = cell;
                                            FleckMaker.ThrowLightningGlow(newThing.DrawPos, newThing.Map, 0.5f);
                                        }
                                        ___pawn.ClearReservationsForJob(newJob);
                                        return false;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        Log.Message("allThings: " + String.Join(", ", allThings));
                    }
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "RenderPawnInternal")]
    public static class PawnRenderer_RenderPawnInternal_Patch
    {
        [HarmonyBefore(new string[] { "rimworld.Nals.FacialAnimation" })]
        static bool Prefix(PawnRenderer __instance, ref Vector3 rootLoc, PawnRenderFlags flags)
        {
            if (!flags.FlagSet(PawnRenderFlags.Portrait))
            {
                Pawn ___pawn = __instance.graphics.pawn;
                if (___pawn.Dead && (___pawn.Corpse?.Spawned ?? false) && ___pawn.Corpse.Position.GetFirstThing<Building_ItemTeleporter>(___pawn.Corpse.Map) != null)
                {
                    return false;
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(SteadyEnvironmentEffects), "TryDoDeteriorate")]
    public static class TryDoDeteriorate_Patch
    {
        public static bool Prefix(Thing t, bool roofed, bool roomUsesOutdoorTemperature, bool protectedByEdifice, TerrainDef terrain)
        {
            if (t?.Map != null)
            {
                var itemTeleporter = t.Position.GetFirstThing<Building_ItemTeleporter>(t.Map);
                if (itemTeleporter != null && itemTeleporter.compPower.PowerOn)
                {
                    return false;
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(CompRottable), "Active", MethodType.Getter)]
    public static class Active_Patch
    {
        public static bool Prefix(CompRottable __instance, ref bool __result)
        {
            if (__instance.parent?.Map != null)
            {
                var itemTeleporter = __instance.parent.Position.GetFirstThing<Building_ItemTeleporter>(__instance.parent.Map);
                if (itemTeleporter != null && itemTeleporter.compPower.PowerOn)
                {
                    __result = false;
                    return false;
                }
            }
            return true;
        }
    }

    public class Building_ItemTeleporter : Building_Storage
    {

        public static Dictionary<Map, HashSet<Building_ItemTeleporter>> buildings = new Dictionary<Map, HashSet<Building_ItemTeleporter>>();
        public CompPowerTrader compPower;
        public bool haulingInterceptionEnabled = true;
        public bool billInterceptionEnabled = true;
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            compPower = base.GetComp<CompPowerTrader>();
            if (!buildings.TryGetValue(map, out var list))
            {
                buildings[map] = list = new HashSet<Building_ItemTeleporter>();
            }
            list.Add(this);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            if (buildings.TryGetValue(this.Map, out var list))
            {
                list.Remove(this);
            }
            base.DeSpawn(mode);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (buildings.TryGetValue(this.Map, out var list))
            {
                list.Remove(this);
            }
            base.Destroy(mode);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var g in base.GetGizmos())
            {
                yield return g;
            }
            if (this.Faction == Faction.OfPlayer)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "IT.EnableBillInterception".Translate(),
                    defaultDesc = "IT.EnableBillInterceptionDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Icons/EnableBillInterception"),
                    isActive = () => billInterceptionEnabled,
                    toggleAction = () => billInterceptionEnabled = !billInterceptionEnabled,
                };

                yield return new Command_Toggle
                {
                    defaultLabel = "IT.EnableHaulingInterception".Translate(),
                    defaultDesc = "IT.EnableHaulingInterceptionDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Icons/EnableHaulingInterception"),
                    isActive = () => haulingInterceptionEnabled,
                    toggleAction = () => haulingInterceptionEnabled = !haulingInterceptionEnabled,
                };
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref haulingInterceptionEnabled, "haulingInterceptionEnabled", true);
            Scribe_Values.Look(ref billInterceptionEnabled, "billInterceptionEnabled", true);
        }
    }
}
