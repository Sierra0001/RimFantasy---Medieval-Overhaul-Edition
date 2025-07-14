using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimFantasy
{
	public class CompProperties_GlowerStuffable : CompProperties
	{
		public float overlightRadius;
		public float glowRadius = 14f;
		public ColorInt glowColor = new ColorInt(255, 255, 255, 0) * 1.45f;
		public bool stuffGlow;
		public bool? glowWhileStockpiled;
        public bool? glowWhileOnGround;
        public float? glowRadiusStockpiled;
		public bool? glowWhileEquipped;
		public float? glowRadiusEquipped;
		public bool? glowWhileDrawn;
		public float? glowRadiusDrawn;
		public CompProperties_GlowerStuffable()
		{
			compClass = typeof(CompGlowerStuffable);
		}
	}
	public class CompGlowerStuffable : ThingComp
    {
        public CompGlower compGlower;
        private bool dirty;
        public CompProperties_GlowerStuffable Props => (CompProperties_GlowerStuffable)props;
        private CompPowerTrader compPower;
        private Map map;
        private IntVec3 prevPosition;
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            this.dirty = true;
            this.compPower = this.parent.GetComp<CompPowerTrader>();
            this.map = this.parent.MapHeld;
            RimFantasyManager.Instance.compGlowerToTick.Add(this);
        }
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            if (RimFantasyManager.Instance.compGlowerToTick.Contains(this))
            {
                RimFantasyManager.Instance.compGlowerToTick.Remove(this);
            }
            this.RemoveGlower(previousMap);
            base.PostDestroy(mode, previousMap);
        }

        public override void PostDeSpawn(Map map)
        {
            this.RemoveGlower(map);
            base.PostDeSpawn(map);
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            RimFantasyManager.Instance.compGlowerToTick.Add(this);
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            RimFantasyManager.Instance.compGlowerToTick.Add(this);
        }
        public void Tick()
        {
            if (this.parent.MapHeld != null)
            {
                if (map is null)
                {
                    map = this.parent.MapHeld;
                }
                if (prevPosition != this.parent.PositionHeld)
                {
                    prevPosition = this.parent.PositionHeld;
                    dirty = true;
                }
                var shouldGlow = ShouldGlow();
                if (compGlower is null && shouldGlow || !shouldGlow)
                {
                    dirty = true;
                }
                else if (compGlower != null && curRadius != GetRadius())
                {
                    dirty = true;
                }
                if (dirty)
                {
                    if (compPower == null || compPower.PowerOn)
                    {
                        this.UpdateGlower();
                    }
                    dirty = false;
                }
                if (compPower != null)
                {
                    if (compPower.PowerOn && this.compGlower == null)
                    {
                        this.UpdateGlower();
                    }
                    else if (!compPower.PowerOn && this.compGlower != null)
                    {
                        this.RemoveGlower(this.parent.MapHeld);
                    }
                }
            }

        }
        public void RemoveGlower(Map prevMap)
        {
            if (prevMap != null && this.compGlower != null)
            {
                try
                {
                    prevMap.glowGrid.DeRegisterGlower(this.compGlower);
                }
                catch { }
                this.compGlower = null;
            }
        }

        private float curRadius;
        public void UpdateGlower()
        {
            var position = GetPosition();
            var map = this.parent.MapHeld;
            if (map != null && position.IsValid)
            {
                RemoveGlower(this.parent.MapHeld);
                if (ShouldGlow())
                {
                    this.compGlower = new CompGlower();
                    var parent = GetParent();
                    var glow = GetGlowColor();
                    curRadius = GetRadius();
                    this.compGlower.Initialize(new CompProperties_Glower()
                    {
                        glowColor = glow,
                        glowRadius = curRadius,
                        overlightRadius = Props.overlightRadius
                    });
                    this.compGlower.parent = parent;
                    base.parent.MapHeld.mapDrawer.MapMeshDirty(position, MapMeshFlagDefOf.Things);
                    base.parent.MapHeld.glowGrid.RegisterGlower(this.compGlower);
                }
            }

        }
        public Pawn Wearer
        {
            get
            {
                if (this.parent.ParentHolder is Pawn_EquipmentTracker tracker)
                {
                    return tracker.pawn;
                }
                else if (this.parent.ParentHolder is Pawn_ApparelTracker apparelTracker)
                {
                    return apparelTracker.pawn;
                }
                else if (this.parent.ParentHolder is Pawn_InventoryTracker inventoryTracker)
                {
                    return inventoryTracker.pawn;
                }
                else if (this.parent.ParentHolder is Pawn_CarryTracker carryTracker)
                {
                    return carryTracker.pawn;
                }
                return null;
            }
        }
        private bool ShouldGlow()
        {
            bool shouldGlow = false;
            var pos = this.parent.PositionHeld;
            if (pos.InBounds(this.parent.MapHeld))
            {
                if (Props.glowWhileStockpiled.HasValue && InStockpile)
                {
                    shouldGlow = Props.glowWhileStockpiled.Value;
                }
                if (Props.glowWhileOnGround.HasValue && this.parent.Spawned && Wearer is null && this.parent.ParentHolder is Map && !InStockpile)
                {
                    shouldGlow = Props.glowWhileOnGround.Value;
                }
                if (Wearer != null && Props.glowWhileDrawn.HasValue && Wearer.IsCarryingWeaponOpenly() && Wearer.equipment?.Primary == this.parent)
                {
                    shouldGlow = Props.glowWhileDrawn.Value;
                }
                if (Wearer != null && Props.glowWhileEquipped.HasValue && Wearer.equipment?.Primary == this.parent)
                {
                    shouldGlow = Props.glowWhileEquipped.Value;
                }
            }
            return shouldGlow;
        }

        private ColorInt GetGlowColor()
        {
            if (this.Props.stuffGlow)
            {
                return this.parent.Stuff != null ? new ColorInt(this.parent.DrawColor) : this.Props.glowColor;
            }
            return this.Props.glowColor;
        }
        private float GetRadius()
        {
            var radius = 0f;
            if (Props.glowRadiusStockpiled.HasValue && InStockpile)
            {
                radius = Props.glowRadiusStockpiled.Value;
            }
            if (Wearer != null && Props.glowWhileEquipped.HasValue && Wearer.equipment?.Primary == this.parent)
            {
                radius = Props.glowRadiusEquipped.Value;
            }
            if (Wearer != null && Props.glowRadiusDrawn.HasValue && Wearer.IsCarryingWeaponOpenly() && Wearer.equipment?.Primary == this.parent)
            {
                radius = Props.glowRadiusDrawn.Value;
            }
            if (radius == 0f)
            {
                return Props.glowRadius;
            }
            return radius;
        }

        private IntVec3 GetPosition()
        {
            if (Wearer != null)
            {
                return Wearer.Position;
            }
            return this.parent.PositionHeld;
        }

        private ThingWithComps GetParent()
        {
            if (Wearer != null)
            {
                return Wearer;
            }
            return this.parent;
        }
        private bool InStockpile => (this.parent.PositionHeld.GetZone(this.parent.MapHeld) is Zone_Stockpile || this.parent.IsInAnyStorage());
    }
}
