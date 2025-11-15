using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MendAndRecycle
{
    public abstract class JobDriver_DoBill : Verse.AI.JobDriver_DoBill
    {
        protected override IEnumerable<Toil> MakeNewToils ()
        {
            AddEndCondition(delegate
            {
                var thing = GetActor().jobs.curJob.GetTarget(BillGiverInd).Thing;
                if (thing is Building && !thing.Spawned)
                {
                    return JobCondition.Incompletable;
                }
                return JobCondition.Ongoing;
            });
            this.FailOnBurningImmobile(BillGiverInd);
            this.FailOn(delegate
            {
                if (job.GetTarget(BillGiverInd).Thing is IBillGiver billGiver)
                {
                    if (job.bill.DeletedOrDereferenced)
                    {
                        return true;
                    }
                    if (!billGiver.CurrentlyUsableForBills())
                    {
                        return true;
                    }
                }
                return false;
            });

            yield return Toils_Reserve.Reserve(BillGiverInd, 1);
            yield return Toils_Reserve.ReserveQueue(IngredientInd, 1);

            Toil gotoBillGiver = Toils_Goto.GotoThing(BillGiverInd, PathEndMode.InteractionCell, false);

            Toil toil = ToilMaker.MakeToil("MakeNewToils");
            toil.initAction = delegate ()
            {
                if (this.job.targetQueueB != null && this.job.targetQueueB.Count == 1)
                {
                    UnfinishedThing unfinishedThing = this.job.targetQueueB[0].Thing as UnfinishedThing;
                    if (unfinishedThing != null && !unfinishedThing.Destroyed)
                    {
                        unfinishedThing.BoundBill = (Bill_ProductionWithUft)this.job.bill;
                    }
                }
                this.job.bill.Notify_DoBillStarted(this.pawn);
            };
            yield return toil;

            yield return Toils_Jump.JumpIf(gotoBillGiver, () => this.job.GetTargetQueue(IngredientInd).NullOrEmpty<LocalTargetInfo>());
            foreach (Toil toil2 in JobDriver_DoBill.CollectIngredientsToils(IngredientInd, BillGiverInd, IngredientPlaceCellInd, false, true, this.BillGiver is Building_WorkTableAutonomous))
            {
                yield return toil2;
            }
            yield return gotoBillGiver;
            yield return DoBill().FailOnDespawnedNullOrForbiddenPlacedThings(BillGiverInd).FailOnCannotTouch(BillGiverInd, PathEndMode.InteractionCell);
            yield return Store();

            yield return Toils_Reserve.Reserve(IngredientPlaceCellInd, 1);
            yield return Toils_Haul.CarryHauledThingToCell(IngredientPlaceCellInd);
            yield return Toils_Haul.PlaceHauledThingInCell(IngredientPlaceCellInd, null, false);

            yield return Toils_Reserve.Release(IngredientInd);
            yield return Toils_Reserve.Release(IngredientPlaceCellInd);
            yield return Toils_Reserve.Release(BillGiverInd);
        }

        protected abstract Toil DoBill ();

        Toil Store ()
        {
            return new Toil ()
            {
                initAction = delegate
                {
                    var objectThing = job.GetTarget (IngredientInd).Thing;

                    if (job.bill.GetStoreMode () != BillStoreModeDefOf.DropOnFloor)
                    {
                        IntVec3 vec = IntVec3.Invalid;
                        if (job.bill.GetStoreMode() == BillStoreModeDefOf.BestStockpile)
                        {
                            StoreUtility.TryFindBestBetterStoreCellFor(objectThing, pawn, pawn.Map, StoragePriority.Unstored, pawn.Faction, out vec, true);
                        }
                        else if (job.bill.GetStoreMode() == BillStoreModeDefOf.SpecificStockpile)
                        {
                            StoreUtility.TryFindBestBetterStoreCellForIn(objectThing, pawn, pawn.Map, StoragePriority.Unstored, pawn.Faction, job.bill.GetSlotGroup(), out vec, true);
                        }
                        else
                        {
                            Log.ErrorOnce("Unknown store mode", 9158246);
                        }
                        if (vec.IsValid)
                        {
                            pawn.carryTracker.TryStartCarry(objectThing, objectThing.stackCount);
                            job.SetTarget(IngredientPlaceCellInd, vec);
                            job.count = 99999;
                            return;
                        }
                    }
                    pawn.carryTracker.TryStartCarry (objectThing, objectThing.stackCount);
                    pawn.carryTracker.TryDropCarriedThing (pawn.Position, ThingPlaceMode.Near, out objectThing);

                    pawn.jobs.EndCurrentJob (JobCondition.Succeeded);
                }
            };
        }
    }
}

