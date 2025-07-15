using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using Verse.AI.Group;
using Verse.Noise;
using static UnityEngine.GraphicsBuffer;
using HarmonyLib;
using UnityEngine;
using Verse.AI;
using RimWorld.Planet;
using System.Collections;
using static System.Net.Mime.MediaTypeNames;
using RimWorld.QuestGen;

namespace DungeonPack
{
    public class ZeusCannon_Bullet : Bullet
    {
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            base.Impact(hitThing, blockedByShield);
            if (hitThing != null)
            {
                Map map = hitThing.Map;
                IntVec3 pos = hitThing.Position;
                map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningStrike(map, pos));
            }

        }
    }


    public class MechSignallerEffect : CompTargetEffect
    {
        public override void DoEffectOn(Pawn user, Thing target)
        {
            var possibleMech = DefDatabase<PawnKindDef>.AllDefsListForReading.Where(MechClusterGenerator.MechKindSuitableForCluster).ToList();

            // To prevent this from being resource broken, spawn "Ancients"
            // Generate a random group of mechanoids, 2-5 base mechanoids, with the potential for more
            var numSpawn = Rand.Range(2, 5);
            List<Pawn> mechs = new List<Pawn>();
            for (int i = 0; i < numSpawn || (Rand.Range(0, 2) == 0); i++)
            {
                var mechToSpawn = PawnGenerator.GeneratePawn(possibleMech.RandomElement(), Faction.OfAncients);
                var refPoint = user.DutyLocation();
                if (target != null)
                {
                    refPoint = target.Position;
                }

                IntVec3 res = refPoint;
                if (DropCellFinder.TryFindDropSpotNear(refPoint, user.Map, out res, true, true))
                {
                    ActiveDropPodInfo activeDropPodInfo = new ActiveDropPodInfo();
                    activeDropPodInfo.innerContainer.TryAdd(mechToSpawn, 1);
                    activeDropPodInfo.openDelay = 100;
                    activeDropPodInfo.leaveSlag = false;
                    activeDropPodInfo.despawnPodBeforeSpawningThing = true;
                    activeDropPodInfo.spawnWipeMode = WipeMode.Vanish;
                    DropPodUtility.MakeDropPodAt(res, user.Map, activeDropPodInfo, user.Faction);

                    mechs.Add(mechToSpawn);
                }
            }

            // Give the mech a job. Everyone needs jobs
            LordJob_AssistColony lord = new LordJob_AssistColony(user.Faction, user.DutyLocation());
            LordMaker.MakeNewLord(user.Faction, lord, user.Map, mechs);
        }
    }


    public class OrbOfMadnessEffect : CompTargetEffect
    {
        public override void DoEffectOn(Pawn user, Thing target)
        {
            Pawn pawn = (Pawn)target;
            if (!pawn.Dead)
            {
                pawn.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Berserk, null, forceWake: true);
                Find.BattleLog.Add(new BattleLogEntry_ItemUsed(user, pawn, parent.def, RulePackDefOf.Event_ItemUsed));

                // 1/10
                if (Rand.Range(0, 10) == 0)
                {
                    user.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Berserk, null, forceWake: true);
                    Find.BattleLog.Add(new BattleLogEntry_ItemUsed(pawn, user, parent.def, RulePackDefOf.Event_ItemUsed));
                }
            }
        }
    }

    public class HediffOnEquip : ThingComp
    {
        public List<HediffDef> realHediff = new List<HediffDef>();
        public HediffOnEquipComp data => (HediffOnEquipComp) props;


        public override void PostPostMake()
        {
            base.PostPostMake();
            foreach (var hediffN in data.hediffs)
            {
                // A hediff with foo_REQDLC tags will require the DLC to work, and will try a version, foo, if there isn't
                // otherwise: proceed as normal
                int loc = hediffN.IndexOf("_REQ");
                var require = (loc == -1) ? "" : hediffN.Substring(loc + 4);
                if (loc != -1 && !ModLister.HasActiveModWithName(require))
                {
                    var adjusted = DefDatabase<HediffDef>.GetNamed(hediffN.Substring(0, loc));
                    if (adjusted != null)
                    {
                        realHediff.Add(adjusted);
                    }
                    //Verse.Log.Message("Added adjusted: " + adjusted + ";requirement: " + require);
                }
                else
                {
                    realHediff.Add(DefDatabase<HediffDef>.GetNamed(hediffN));
                    //Verse.Log.Message("Added: " + hediffN);

                }
            }
        }

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            foreach (var def in realHediff)
            {
                Hediff hediff = HediffMaker.MakeHediff(def, pawn, null);
                pawn.health.AddHediff(hediff);
            }
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            foreach (var def in realHediff)
            {
                Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(def);
                if (firstHediffOfDef != null)
                {
                    pawn.health.RemoveHediff(firstHediffOfDef);
                }
            }
        }
    }
    public class HediffOnEquipComp : CompProperties
    {
        public List<string> hediffs;

        public HediffOnEquipComp()
        {
            compClass = typeof(HediffOnEquip);
        }
    }

    public class GaiaFuryEffect : CompUseEffect
    {
        public override void DoEffect(Pawn usedBy)
        {
            if (usedBy.Map.GameConditionManager.GetActiveCondition(GameConditionDefOf.PsychicDrone) != null)
            {
                return;
            }
            IncidentParms incidentParms = StorytellerUtility.DefaultParmsNow(incCat: IncidentCategoryDefOf.ThreatSmall, target: usedBy.Map);
            Find.Storyteller.incidentQueue.Add(DefDatabase<IncidentDef>.GetNamed("PsychicDrone"), Find.TickManager.TicksGame, incidentParms);

        }
    }


    public class RealitybreakerEffect : CompUseEffect
    {
        public override void DoEffect(Pawn usedBy)
        {
            foreach (Pawn item in usedBy.MapHeld.mapPawns.AllPawnsSpawned)
            {
                Hediff hediff = HediffMaker.MakeHediff(HediffDefOf.PsychicShock, item);
                hediff.Severity = 1.0f;
                item.health.AddHediff(hediff);
            }
        }
    }


    public class PandoraBoxEffect : CompUseEffect
    {

        public override void DoEffect(Pawn usedBy)
        {
            int numIncidents = Rand.Range(1, 5);
            IncidentParms incidentParms2 = StorytellerUtility.DefaultParmsNow(incCat: IncidentCategoryDefOf.ThreatSmall, target: usedBy.Map);
            for (int i = 0; (i < numIncidents) || (Rand.Range(0, 2) == 0); i++)
            {
                var randomIncident = DefDatabase<IncidentDef>.GetRandom();
                //Verse.Log.Message(randomIncident.defName + " " + randomIncident.baseChance + " " + randomIncident.TargetAllowed(usedBy.Map));

                if (randomIncident.baseChance == 0 || !randomIncident.TargetAllowed(usedBy.Map))
                {


                    continue;
                }
                Find.Storyteller.incidentQueue.Add(randomIncident, Find.TickManager.TicksGame + 500 + i * 10, incidentParms2);
            }
        }
    }

    public class ThorHammerEffect : DamageWorker_Blunt
    {
        protected override void ApplySpecialEffectsToPart(Pawn pawn, float totalDamage, DamageInfo dinfo, DamageResult result)
        {
            base.ApplySpecialEffectsToPart(pawn, totalDamage, dinfo, result);

            // Add lightning strike!
            Map map = pawn.Map;
            map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningStrike(map, pawn.Position));

        }

    }


    public class RandomPhaseEffect : HediffComp
    {
        private int ticksUntilNextPhase;
        private int baseTicksUntilNext = 2500;
        public override void CompPostMake()
        {
            base.CompPostMake();
            ticksUntilNextPhase = baseTicksUntilNext;
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (ticksUntilNextPhase-- <= 0) {
                ticksUntilNextPhase = baseTicksUntilNext;

                // Place pawn in a random walkable position
                //Verse.Log.Message("Random teleport!");
                for (int i = 0; i < 5; i++)
                {
                    // Select 10 random spots that are standable
                    var cell = CellFinder.RandomCell(parent.pawn.Map);
                    if (cell.Standable(parent.pawn.Map))
                    {
                        parent.pawn.SetPositionDirect(cell);
                        break;
                    }
                }
            }
        }
    }


    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            var inst = new Harmony("rimworld.dungeonpack");
            inst.Patch(AccessTools.Method(typeof(ResearchManager), nameof(ResearchManager.FinishProject)), postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(ResearchCatcher)));
        }

        public static void ResearchCatcher(ref ResearchManager __instance, ResearchProjectDef proj, bool doCompletionDialog, Pawn researcher, bool doCompletionLetter)
        {

            int iIncident = proj.defName.IndexOf("DP_RGive");
            if (iIncident != -1)
            {
                // Trigger quest
                // Schema, DP_RGive[QuestName] -> DP_IGive[QuestName]
                Map map = Find.CurrentMap;
                if (researcher != null)
                {
                    map = researcher.Map;
                }

                IncidentParms incidentParms = StorytellerUtility.DefaultParmsNow(incCat: IncidentCategoryDefOf.GiveQuest, target: map);
                var questName = "DP_IGive" + proj.defName.Substring(iIncident + ("DP_RGive").Length);
                Find.Storyteller.incidentQueue.Add(DefDatabase<IncidentDef>.GetNamed(questName), Find.TickManager.TicksGame, incidentParms);
            }

            if (proj.defName.Equals("DP_ResearchReset"))
            {
                //proj. = false;
                proj.baseCost += 500;
            }
        }
    }

    public class LostMercyEffect : HediffWithComps
    {
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            pawn.needs.mood.thoughts.memories.TryGainMemory(DefDatabase<ThoughtDef>.GetNamed("DP_LostMercy").producesMemoryThought);
        }

        public override void PostRemoved()
        {
            pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(DefDatabase<ThoughtDef>.GetNamed("DP_LostMercy").producesMemoryThought);
        }
    }

    public class DP_QuestRedoerEffect : CompUseEffect
    {
        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);
            DiaNode root = new DiaNode(new TaggedString(""));
            var researchRef = Find.ResearchManager;

            var DPProjs = DefDatabase<ResearchProjectDef>.AllDefsListForReading.FindAll((proj) => proj.defName.StartsWith("DP_RGive"));
            var DPIGivers = DefDatabase<IncidentDef>.AllDefsListForReading.FindAll((incid) => incid.defName.StartsWith("DP_IGive"));

            foreach (var proj in DPProjs)
            {
                if (proj.IsFinished)
                {
                    var questN = proj.defName.Substring("DP_RGive".Length);
                    DiaOption diaOption = new DiaOption("Start " + questN);
                    diaOption.action = delegate
                    {

                        /*
                        var active = Find.QuestManager.QuestsListForReading.Find((currq) => currq.root.defName.Equals("DP_" + questN));
                        if (active != null)
                        {
                            active.End(QuestEndOutcome.Unknown, false);
                        }
                        */

                        var quest = DPIGivers.Find((incid) => incid.defName.Equals("DP_IGive" + questN));
                        IncidentParms incidentParms = StorytellerUtility.DefaultParmsNow(incCat: IncidentCategoryDefOf.GiveQuest, target: usedBy.Map);
                        Find.Storyteller.incidentQueue.Add(quest, Find.TickManager.TicksGame, incidentParms);

                    };
                    diaOption.resolveTree = true;
                    root.options.Add(diaOption);
                }
            }


            root.options.Add(new DiaOption("(" + "Cancel".Translate() + ")") { resolveTree = true });
            Find.WindowStack.Add(new DP_GiveQuestDialog(usedBy, root, radioMode: true));
        }
    }


    public class DP_GiveQuestDialog : Dialog_NodeTree
    {
	    protected Pawn negotiator;

	    private const float TitleHeight = 70f;

	    private const float InfoHeight = 60f;

	    public override Vector2 InitialSize => new Vector2(720f, 600f);

	    public DP_GiveQuestDialog(Pawn negotiator, DiaNode startNode, bool radioMode)
		    : base(startNode, radioMode)
	    {
		    this.negotiator = negotiator;
	    }

        // Find.WindowStack.Add(dialog_Negotiation);
        public override void DoWindowContents(Rect inRect)
	    {
		    Widgets.BeginGroup(inRect);
		    Rect rect = new Rect(0f, 0f, inRect.width / 2f, 70f);
		    Rect rect2 = new Rect(0f, rect.yMax, rect.width, 60f);
		    Rect rect3 = new Rect(inRect.width / 2f, 0f, inRect.width / 2f, 70f);
		    Rect rect4 = new Rect(inRect.width / 2f, rect.yMax, rect.width, 60f);
		    Verse.Text.Font = GameFont.Medium;
		    Widgets.Label(rect, "Trigger a quest");
            Verse.Text.Anchor = TextAnchor.UpperRight;
		    // Widgets.Label(rect3, new GUIContent("test text 1"));
		    Verse.Text.Anchor = TextAnchor.UpperLeft;
		    Verse.Text.Font = GameFont.Small;
		    GUI.color = new Color(1f, 1f, 1f, 0.7f);
            // Widgets.Label(rect2, "SocialSkillIs".Translate(negotiator.skills.GetSkill(SkillDefOf.Social).Level));
            Widgets.Label(rect2, "Trigger an old Dungeon Pack quest. The quest must have first been researched");
            Verse.Text.Anchor = TextAnchor.UpperRight;
		    // Widgets.Label(rect4, new GUIContent("test text 2"));
		    Verse.Text.Anchor = TextAnchor.UpperLeft;
		    GUI.color = Color.white;
		    Widgets.EndGroup();
		    float num = 147f;
		    Rect rect5 = new Rect(0f, num, inRect.width, inRect.height - num);
		    DrawNode(rect5);
	    }
    }
}

