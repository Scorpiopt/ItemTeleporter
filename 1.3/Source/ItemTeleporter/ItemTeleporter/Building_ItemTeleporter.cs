using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

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

    [HarmonyPatch(typeof(Pawn_JobTracker), "StartJob")]
    public static class Pawn_JobTracker_StartJob_Patch
    {
        public static void Prefix(Pawn ___pawn, Job newJob, JobCondition lastJobEndCondition = JobCondition.None, ThinkNode jobGiver = null, bool resumeCurJobAfterwards = false, bool cancelBusyStances = true, ThinkTreeDef thinkTree = null, JobTag? tag = null, bool fromQueue = false, bool canReturnCurJobToPool = false)
        {
            if (newJob != null && newJob.def == JobDefOf.DoBill)
            {
                var storagesAround = GenRadial.RadialDistinctThingsAround(newJob.targetA.Thing.Position, newJob.targetA.Thing.Map, 5, true).OfType<Building_ItemTeleporter>().ToList();
                for (var i = 0; i < newJob.countQueue.Count; i++)
                {
                    if (!newJob.targetQueueB[i].Thing.Position.GetThingList(newJob.targetA.Thing.Map).Any(x => x is Building_ItemTeleporter))
                    {
                        foreach (var storage in storagesAround)
                        {
                            if (storage.GetStoreSettings().AllowedToAccept(newJob.targetQueueB[i].Thing))
                            {
                                var goodCells = storage.AllSlotCells().Where(x => StoreUtility.IsGoodStoreCell(x, newJob.targetA.Thing.Map, newJob.targetQueueB[i].Thing, ___pawn, ___pawn.Faction));
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
                                        Log.Message(newThing.stackCount + " - " + newJob.countQueue[i]);
                                        GenSpawn.Spawn(newThing, thing.Position, thing.Map);
                                        newThing.Position = cell;
                                        newJob.targetQueueB[i] = newThing;
                                        FleckMaker.ThrowLightningGlow(newThing.DrawPos, newThing.Map, 0.5f);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    public class Building_ItemTeleporter : Building_Storage
    {

    }
}
