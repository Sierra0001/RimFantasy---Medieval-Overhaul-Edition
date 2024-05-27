using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using System.Reflection;
using Verse;
using UnityEngine;
using HarmonyLib;
using Verse.AI.Group;

namespace RimFantasy
{
    public class CompProperties_WornWeapon : CompProperties
    {
        static GraphicData nullData;
        public static Graphic NullData
        {
            get
            {
                if(nullData==null)
                {
                    nullData = new GraphicData();
                    nullData.graphicClass = typeof(Graphic_Single);
                    nullData.texPath = "Things/NullDraw";
                }
                return nullData.Graphic;
            }
        }
        public GraphicData sheathOnlyGraphicData = null;
        public GraphicData fullGraphicData = null;
        public DrawPosition drawPosition;
        public Offset northOffset;
        public Offset eastOffset;
        public Offset southOffset;
        public Offset westOffset;
        public CompProperties_WornWeapon()
        {
            compClass = typeof(CompWornWeapon);
        }
    } 
    public class CompWornWeapon : ThingComp
    {
        private Graphic fullGraphicInt;
        private Graphic sheathOnlyGraphicInt;
        public CompProperties_WornWeapon Props;

        public virtual Graphic FullGraphic
        {
            get
            {
                if (fullGraphicInt == null)
                {
                    if (Props.fullGraphicData == null)
                    {
                        return parent.Graphic;
                    }
                    fullGraphicInt = Props.fullGraphicData.GraphicColoredFor(parent);
                }
                return fullGraphicInt;
            }
        }
        public virtual Graphic SheathOnlyGraphic
        {
            get
            {
                if (sheathOnlyGraphicInt == null)
                {
                    if (Props.sheathOnlyGraphicData == null)
                    {
                        sheathOnlyGraphicInt = CompProperties_WornWeapon.NullData;
                        return sheathOnlyGraphicInt;
                    }
                    sheathOnlyGraphicInt = Props.sheathOnlyGraphicData.GraphicColoredFor(parent);
                }
                return sheathOnlyGraphicInt;
            }
        }
        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            Props = (CompProperties_WornWeapon)this.props;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            Props = (CompProperties_WornWeapon)props;
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Props = (CompProperties_WornWeapon)props;
        }

        public bool ShouldShowWeapon(Pawn pawn, PawnRenderFlags flags = PawnRenderFlags.None)
        {
            Stance_Busy stance_Busy = pawn.stances.curStance as Stance_Busy;
            if (stance_Busy != null && !stance_Busy.neverAimWeapon && stance_Busy.focusTarg.IsValid && (flags & PawnRenderFlags.NeverAimWeapon) == 0)
            {

            }
            else if (!pawn.IsCarryingWeaponOpenly() && !(pawn.InBed()) && pawn.GetPosture() == PawnPosture.Standing)
            {
                return true;
            }
            return false;
        }

    }
    public enum DrawPosition
    {
        Back,
        Side
    }
    public struct Offset
    {
        public Vector3 position;
        public float angle;
    }
    public struct DrawOffsetSet
    {
        public Offset northOffset;
        public Offset eastOffset;
        public Offset southOffset;
        public Offset westOffset;
    }
}
