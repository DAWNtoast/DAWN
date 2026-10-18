using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Small, outlined combat symbols drawn without font or atlas dependencies.</summary>
    [RequireComponent(typeof(CanvasRenderer)), DisallowMultipleComponent]
    public sealed class BattleStatusIconGraphic : MaskableGraphic
    {
        [SerializeField] private CoreBattleStatusKind kind;
        [SerializeField] private string statusId;
        [SerializeField] private bool isDebuff;
        private Rect bounds;
        private float scale;
        private float glyphScale = 1f;
        private Vector2 glyphOrigin;

        private enum VariantMark
        {
            None, Plus, Flame, Clock, Boarding, Projectile, Sword, UpArrow, Oil, Self, Incoming,
            Scroll, Pot, FreePowder, Skull, Wave, Chain, Arrow, Repeat, Ground, Pulse, Return,
            Heartbeat, Slash, Lock, Star, Shield, Bolt
        }

        public CoreBattleStatusKind Kind => kind;
        public string StatusId => statusId;
        public bool IsDebuff => isDebuff;

        private Color Ink => isDebuff ? Rgb(55, 20, 29) : Rgb(16, 28, 35);
        private static Color Ivory => Rgb(255, 241, 198);
        private static Color Steel => Rgb(168, 218, 231);
        private static Color Gold => Rgb(255, 197, 64);
        private static Color Aqua => Rgb(57, 218, 212);
        private static Color Rose => Rgb(250, 83, 101);
        private static Color Violet => Rgb(194, 129, 255);

        public void Configure(CoreBattleStatusKind kind, string statusId, bool isDebuff)
        {
            this.kind = kind;
            this.statusId = statusId ?? string.Empty;
            this.isDebuff = isDebuff;
            color = Color.white;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            bounds = rectTransform.rect;
            scale = Mathf.Min(bounds.width, bounds.height) * .47f;
            if (scale <= 0f) return;
            VariantMark mark = MarkForStatus(statusId);
            glyphScale = mark == VariantMark.None ? 1f : .81f;
            glyphOrigin = mark == VariantMark.None ? Vector2.zero : new Vector2(-.12f, -.12f);

            switch (kind)
            {
                case CoreBattleStatusKind.Guard:
                    Shield(mesh, statusId == "prepare_fire_guard" || statusId == "natural_fire_guard" ? Gold : Aqua);
                    Line(mesh, new Vector2(0f, .52f), new Vector2(0f, -.55f), .08f, Ivory);
                    break;
                case CoreBattleStatusKind.Armor:
                    Armor(mesh);
                    break;
                case CoreBattleStatusKind.Attack:
                    Sword(mesh, false, false);
                    break;
                case CoreBattleStatusKind.Fire:
                    Fire(mesh);
                    break;
                case CoreBattleStatusKind.Retaliation:
                    Sword(mesh, true, false);
                    Sword(mesh, false, false);
                    break;
                case CoreBattleStatusKind.Healing:
                    Polygon(mesh, Aqua, new Vector2(-.31f, .78f), new Vector2(.31f, .78f),
                        new Vector2(.31f, .3f), new Vector2(.77f, .3f), new Vector2(.77f, -.3f),
                        new Vector2(.31f, -.3f), new Vector2(.31f, -.78f), new Vector2(-.31f, -.78f),
                        new Vector2(-.31f, -.3f), new Vector2(-.77f, -.3f), new Vector2(-.77f, .3f), new Vector2(-.31f, .3f));
                    Line(mesh, new Vector2(0f, .53f), new Vector2(0f, -.53f), .12f, Ivory);
                    Line(mesh, new Vector2(-.51f, 0f), new Vector2(.51f, 0f), .12f, Ivory);
                    break;
                case CoreBattleStatusKind.Curse:
                    Skull(mesh);
                    break;
                case CoreBattleStatusKind.Weakness:
                    Sword(mesh, false, true);
                    break;
                case CoreBattleStatusKind.Tide:
                    Waves(mesh);
                    break;
                case CoreBattleStatusKind.Arcane:
                    Star(mesh);
                    break;
                case CoreBattleStatusKind.Thunder:
                    Polygon(mesh, Gold, new Vector2(.04f, .87f), new Vector2(-.62f, -.08f),
                        new Vector2(-.07f, -.08f), new Vector2(-.24f, -.88f),
                        new Vector2(.69f, .26f), new Vector2(.14f, .26f));
                    break;
                case CoreBattleStatusKind.Blood:
                    Drop(mesh, Rose);
                    break;
                case CoreBattleStatusKind.Delayed:
                    Hourglass(mesh);
                    break;
                case CoreBattleStatusKind.Anchor:
                    Anchor(mesh);
                    break;
                case CoreBattleStatusKind.Slow:
                    Web(mesh);
                    break;
                case CoreBattleStatusKind.Immunity:
                    Shield(mesh, Rgb(102, 218, 136));
                    Stroke(mesh, Ivory, .16f, new Vector2(-.39f, .02f), new Vector2(-.08f, -.29f), new Vector2(.43f, .36f));
                    break;
                case CoreBattleStatusKind.Seal:
                    Lock(mesh);
                    break;
                case CoreBattleStatusKind.Draw:
                    Cards(mesh);
                    break;
            }
            if (mark != VariantMark.None) DrawVariantMark(mesh, mark);
        }

        private static VariantMark MarkForStatus(string id)
        {
            // These marks communicate the effect's source or trigger. They are
            // deliberately mapped by meaning, never by an arbitrary ID hash.
            switch (id)
            {
                case "deck_buff": case "curse_tick_bonus": return VariantMark.Plus;
                case "prepare_fire_guard": case "fire_retaliation": case "fire_immunity": return VariantMark.Flame;
                case "natural_fire_guard": case "retained_guard": case "healing": return VariantMark.Clock;
                case "boarding_cover": case "boarding_burn": return VariantMark.Boarding;
                case "loaded_shot": case "fire_rounds": case "load_seal": return VariantMark.Projectile;
                case "scorch": case "blood_attack": case "delayed_damage": return VariantMark.Sword;
                case "overflow": case "thunder_ready": return VariantMark.UpArrow;
                case "oil": return VariantMark.Oil;
                case "self_burn": case "crew_shortage": return VariantMark.Self;
                case "enemy_burn": case "voyage_curse": return VariantMark.Incoming;
                case "fire_pact": case "curse_contract": case "blood_oath": return VariantMark.Scroll;
                case "fire_pots": return VariantMark.Pot;
                case "free_powder": return VariantMark.FreePowder;
                case "curse_counter": case "curse_weakness": case "curse_armor": return VariantMark.Skull;
                case "tide_ambush": case "tide_weakness": return VariantMark.Wave;
                case "curse_link": case "precharge": return VariantMark.Chain;
                case "arcane_piercing": return VariantMark.Arrow;
                case "arcane_echo": case "thunder_residual": return VariantMark.Repeat;
                case "grounding": return VariantMark.Ground;
                case "pulse_reduction": return VariantMark.Pulse;
                case "thunder_weakness": return VariantMark.Bolt;
                case "blood_debt": return VariantMark.Return;
                case "desperate": return VariantMark.Heartbeat;
                case "healing_halved": return VariantMark.Slash;
                case "retained_cards": return VariantMark.Lock;
                case "rune_clamp": return VariantMark.Star;
                case "curse_guard": case "tide_guard": return VariantMark.Shield;
                default: return VariantMark.None;
            }
        }

        private void DrawVariantMark(VertexHelper mesh, VariantMark mark)
        {
            // Upper-right leaves the lower-right amount legible in the row.
            glyphScale = 1f;
            glyphOrigin = Vector2.zero;
            Circle(mesh, new Vector2(.54f, .54f), .38f, Ink, false);
            glyphScale = .34f;
            glyphOrigin = new Vector2(.54f, .54f);
            switch (mark)
            {
                case VariantMark.Plus:
                    Line(mesh, new Vector2(-.7f, 0f), new Vector2(.7f, 0f), .29f, Ivory);
                    Line(mesh, new Vector2(0f, -.7f), new Vector2(0f, .7f), .29f, Ivory);
                    break;
                case VariantMark.Flame: Fire(mesh); break;
                case VariantMark.Clock:
                    Circle(mesh, Vector2.zero, .79f, Gold, false);
                    Circle(mesh, Vector2.zero, .58f, Ink, false);
                    Line(mesh, Vector2.zero, new Vector2(0f, .42f), .17f, Ivory);
                    Line(mesh, Vector2.zero, new Vector2(.38f, -.16f), .17f, Ivory);
                    break;
                case VariantMark.Boarding:
                    Line(mesh, new Vector2(-.75f, -.5f), new Vector2(.75f, .5f), .2f, Gold);
                    Line(mesh, new Vector2(-.75f, -.1f), new Vector2(.75f, .9f), .2f, Gold);
                    Line(mesh, new Vector2(-.45f, -.32f), new Vector2(-.45f, .08f), .17f, Ivory);
                    Line(mesh, new Vector2(0f, -.02f), new Vector2(0f, .38f), .17f, Ivory);
                    Line(mesh, new Vector2(.45f, .28f), new Vector2(.45f, .68f), .17f, Ivory);
                    break;
                case VariantMark.Projectile:
                    Circle(mesh, new Vector2(-.08f, -.1f), .6f, Steel, false);
                    Circle(mesh, new Vector2(-.24f, .12f), .18f, Ivory, false);
                    Line(mesh, new Vector2(.33f, .29f), new Vector2(.69f, .67f), .19f, Gold);
                    break;
                case VariantMark.Sword: Sword(mesh, false, false); break;
                case VariantMark.UpArrow:
                    Line(mesh, new Vector2(0f, -.73f), new Vector2(0f, .33f), .3f, Gold);
                    Fill(mesh, Gold, new Vector2(-.7f, .16f), new Vector2(0f, .85f), new Vector2(.7f, .16f));
                    break;
                case VariantMark.Oil: Drop(mesh, Gold); break;
                case VariantMark.Self:
                    Circle(mesh, new Vector2(0f, .43f), .3f, Ivory, false);
                    Fill(mesh, Ivory, new Vector2(-.34f, .01f), new Vector2(.34f, .01f),
                        new Vector2(.54f, -.72f), new Vector2(-.54f, -.72f));
                    break;
                case VariantMark.Incoming:
                    Line(mesh, new Vector2(.77f, 0f), new Vector2(-.28f, 0f), .28f, Rose);
                    Fill(mesh, Rose, new Vector2(-.2f, .66f), new Vector2(-.86f, 0f), new Vector2(-.2f, -.66f));
                    break;
                case VariantMark.Scroll:
                    Fill(mesh, Ivory, new Vector2(-.6f, .78f), new Vector2(.57f, .78f), new Vector2(.57f, -.72f), new Vector2(-.6f, -.72f));
                    Line(mesh, new Vector2(-.33f, .3f), new Vector2(.31f, .3f), .13f, Ink);
                    Line(mesh, new Vector2(-.33f, -.07f), new Vector2(.31f, -.07f), .13f, Ink);
                    Circle(mesh, new Vector2(.34f, -.49f), .24f, Rose, false);
                    break;
                case VariantMark.Pot:
                    Fill(mesh, Gold, new Vector2(-.48f, .63f), new Vector2(.48f, .63f), new Vector2(.32f, .28f),
                        new Vector2(.68f, -.31f), new Vector2(.43f, -.75f), new Vector2(-.43f, -.75f),
                        new Vector2(-.68f, -.31f), new Vector2(-.32f, .28f));
                    Line(mesh, new Vector2(-.34f, .65f), new Vector2(.34f, .65f), .18f, Ivory);
                    break;
                case VariantMark.FreePowder:
                    Fill(mesh, Gold, new Vector2(-.56f, .68f), new Vector2(.56f, .68f),
                        new Vector2(.56f, -.68f), new Vector2(-.56f, -.68f));
                    Line(mesh, new Vector2(-.56f, .28f), new Vector2(.56f, .28f), .16f, Ink);
                    Line(mesh, new Vector2(-.56f, -.28f), new Vector2(.56f, -.28f), .16f, Ink);
                    Line(mesh, new Vector2(-.74f, -.78f), new Vector2(.74f, .78f), .24f, Ivory);
                    break;
                case VariantMark.Skull: Skull(mesh); break;
                case VariantMark.Wave: Waves(mesh); break;
                case VariantMark.Chain:
                    Ring(mesh, new Vector2(-.31f, -.26f), .46f, Gold);
                    Ring(mesh, new Vector2(.31f, .26f), .46f, Gold);
                    Line(mesh, new Vector2(-.25f, -.21f), new Vector2(.25f, .21f), .18f, Ivory);
                    break;
                case VariantMark.Arrow:
                    Line(mesh, new Vector2(-.74f, -.74f), new Vector2(.39f, .39f), .21f, Ivory);
                    Fill(mesh, Violet, new Vector2(-.01f, .73f), new Vector2(.79f, .79f), new Vector2(.73f, -.01f));
                    break;
                case VariantMark.Repeat:
                    Stroke(mesh, Ivory, .2f, new Vector2(-.7f, .6f), new Vector2(-.1f, 0f), new Vector2(-.7f, -.6f));
                    Stroke(mesh, Gold, .2f, new Vector2(.04f, .6f), new Vector2(.64f, 0f), new Vector2(.04f, -.6f));
                    break;
                case VariantMark.Ground:
                    Line(mesh, new Vector2(0f, .78f), new Vector2(0f, -.1f), .2f, Gold);
                    Line(mesh, new Vector2(-.77f, -.13f), new Vector2(.77f, -.13f), .2f, Gold);
                    Line(mesh, new Vector2(-.52f, -.45f), new Vector2(.52f, -.45f), .2f, Gold);
                    Line(mesh, new Vector2(-.25f, -.77f), new Vector2(.25f, -.77f), .2f, Gold);
                    break;
                case VariantMark.Pulse:
                    Stroke(mesh, Aqua, .2f, new Vector2(-.82f, 0f), new Vector2(-.48f, 0f),
                        new Vector2(-.15f, .66f), new Vector2(.22f, -.66f), new Vector2(.5f, 0f), new Vector2(.82f, 0f));
                    break;
                case VariantMark.Return:
                    Stroke(mesh, Ivory, .21f, new Vector2(.58f, -.6f), new Vector2(.58f, .25f), new Vector2(.3f, .5f), new Vector2(-.41f, .5f));
                    Fill(mesh, Rose, new Vector2(-.28f, .86f), new Vector2(-.86f, .5f), new Vector2(-.28f, .14f));
                    break;
                case VariantMark.Heartbeat:
                    Fill(mesh, Rose, new Vector2(0f, .38f), new Vector2(.4f, .73f), new Vector2(.79f, .41f),
                        new Vector2(.73f, -.05f), new Vector2(0f, -.83f), new Vector2(-.73f, -.05f),
                        new Vector2(-.79f, .41f), new Vector2(-.4f, .73f));
                    Line(mesh, new Vector2(-.56f, .03f), new Vector2(.56f, .03f), .17f, Ivory);
                    break;
                case VariantMark.Slash:
                    Circle(mesh, Vector2.zero, .78f, Rose, false);
                    Circle(mesh, Vector2.zero, .54f, Ink, false);
                    Line(mesh, new Vector2(-.53f, -.53f), new Vector2(.53f, .53f), .24f, Rose);
                    break;
                case VariantMark.Lock: Lock(mesh); break;
                case VariantMark.Star: Star(mesh); break;
                case VariantMark.Shield: Shield(mesh, Aqua); break;
                case VariantMark.Bolt:
                    Fill(mesh, Gold, new Vector2(.04f, .87f), new Vector2(-.62f, -.08f),
                        new Vector2(-.07f, -.08f), new Vector2(-.24f, -.88f),
                        new Vector2(.69f, .26f), new Vector2(.14f, .26f));
                    break;
            }
        }

        private void Ring(VertexHelper mesh, Vector2 center, float radius, Color tint)
        {
            Circle(mesh, center, radius, tint, false);
            Circle(mesh, center, radius * .57f, Ink, false);
        }

        private void Shield(VertexHelper mesh, Color tint)
        {
            Polygon(mesh, tint, new Vector2(-.7f, .68f), new Vector2(0f, .86f), new Vector2(.7f, .68f),
                new Vector2(.61f, -.17f), new Vector2(.37f, -.55f), new Vector2(0f, -.84f),
                new Vector2(-.37f, -.55f), new Vector2(-.61f, -.17f));
        }

        private void Armor(VertexHelper mesh)
        {
            Polygon(mesh, Steel, new Vector2(-.36f, .79f), new Vector2(-.14f, .53f), new Vector2(.14f, .53f),
                new Vector2(.36f, .79f), new Vector2(.77f, .51f), new Vector2(.57f, .05f),
                new Vector2(.47f, -.71f), new Vector2(0f, -.85f), new Vector2(-.47f, -.71f),
                new Vector2(-.57f, .05f), new Vector2(-.77f, .51f));
            Line(mesh, new Vector2(0f, .41f), new Vector2(0f, -.65f), .07f, Rgb(78, 129, 149));
            Stroke(mesh, Gold, .09f, new Vector2(-.44f, -.49f), new Vector2(0f, -.61f), new Vector2(.44f, -.49f));
        }

        private void Sword(VertexHelper mesh, bool mirrored, bool broken)
        {
            float flip = mirrored ? -1f : 1f;
            Vector2 root = new Vector2(-.5f * flip, -.5f);
            Vector2 tip = new Vector2(.76f * flip, .8f);
            Vector2 shoulder = new Vector2(.49f * flip, .72f);
            Vector2 bladeRoot = new Vector2(-.38f * flip, -.22f);
            Vector2 bladeBottom = new Vector2(-.19f * flip, -.41f);
            Color blade = broken ? Rgb(162, 157, 166) : Steel;
            if (broken)
            {
                Polygon(mesh, blade, shoulder, tip, new Vector2(.7f * flip, .5f),
                    new Vector2(.22f * flip, -.01f), new Vector2(.12f * flip, .22f), new Vector2(-.01f * flip, .17f));
                Polygon(mesh, blade, bladeRoot, new Vector2(-.04f * flip, .1f),
                    new Vector2(.07f * flip, -.14f), new Vector2(.18f * flip, -.08f), bladeBottom);
            }
            else
            {
                Polygon(mesh, blade, shoulder, tip, new Vector2(.7f * flip, .5f), bladeBottom, bladeRoot);
                Line(mesh, new Vector2(-.14f * flip, -.14f), new Vector2(.58f * flip, .58f), .055f, Ivory);
            }
            Stroke(mesh, Gold, .15f, new Vector2(-.66f * flip, -.14f), new Vector2(-.13f * flip, -.68f));
            Stroke(mesh, Rgb(165, 91, 61), .16f, root, new Vector2(-.72f * flip, -.75f));
            Circle(mesh, new Vector2(-.76f * flip, -.78f), .13f, Gold, true);
        }

        private void Fire(VertexHelper mesh)
        {
            Polygon(mesh, Rgb(255, 136, 47), new Vector2(-.72f, -.23f), new Vector2(-.58f, .4f),
                new Vector2(-.26f, .1f), new Vector2(-.02f, .88f), new Vector2(.35f, .44f),
                new Vector2(.37f, .08f), new Vector2(.66f, .4f), new Vector2(.73f, -.3f),
                new Vector2(.4f, -.72f), new Vector2(-.18f, -.81f), new Vector2(-.57f, -.6f));
            Polygon(mesh, Gold, new Vector2(-.36f, -.4f), new Vector2(-.04f, .3f),
                new Vector2(.17f, -.17f), new Vector2(.34f, -.38f), new Vector2(.2f, -.66f), new Vector2(-.19f, -.67f));
        }

        private void Skull(VertexHelper mesh)
        {
            Circle(mesh, new Vector2(0f, .18f), .66f, Violet, true);
            Polygon(mesh, Violet, new Vector2(-.44f, -.13f), new Vector2(.44f, -.13f),
                new Vector2(.33f, -.73f), new Vector2(-.33f, -.73f));
            Circle(mesh, new Vector2(-.26f, .12f), .17f, Ink, false);
            Circle(mesh, new Vector2(.26f, .12f), .17f, Ink, false);
            Polygon(mesh, Ink, new Vector2(0f, -.12f), new Vector2(-.13f, -.33f), new Vector2(.13f, -.33f));
            Line(mesh, new Vector2(-.13f, -.48f), new Vector2(-.13f, -.71f), .07f, Ink);
            Line(mesh, new Vector2(.13f, -.48f), new Vector2(.13f, -.71f), .07f, Ink);
        }

        private void Waves(VertexHelper mesh)
        {
            for (int row = 0; row < 3; row++)
            {
                float y = .48f - row * .47f;
                Stroke(mesh, row == 1 ? Steel : Aqua, .15f,
                    new Vector2(-.79f, y - .07f), new Vector2(-.55f, y + .1f), new Vector2(-.3f, y + .1f),
                    new Vector2(.03f, y - .1f), new Vector2(.3f, y - .1f), new Vector2(.58f, y + .09f), new Vector2(.79f, y + .09f));
            }
        }

        private void Star(VertexHelper mesh)
        {
            Polygon(mesh, Violet, new Vector2(0f, .87f), new Vector2(.22f, .23f), new Vector2(.81f, 0f),
                new Vector2(.22f, -.23f), new Vector2(0f, -.87f), new Vector2(-.22f, -.23f),
                new Vector2(-.81f, 0f), new Vector2(-.22f, .23f));
            Circle(mesh, Vector2.zero, .16f, Ivory, false);
            Circle(mesh, new Vector2(-.57f, .58f), .1f, Gold, true);
            Circle(mesh, new Vector2(.57f, -.58f), .1f, Gold, true);
        }

        private void Drop(VertexHelper mesh, Color tint)
        {
            Polygon(mesh, tint, new Vector2(0f, .89f), new Vector2(.29f, .43f), new Vector2(.61f, -.08f),
                new Vector2(.66f, -.38f), new Vector2(.46f, -.69f), new Vector2(0f, -.84f),
                new Vector2(-.46f, -.69f), new Vector2(-.66f, -.38f), new Vector2(-.61f, -.08f), new Vector2(-.29f, .43f));
            Stroke(mesh, Rgb(255, 180, 176), .09f, new Vector2(-.29f, -.15f), new Vector2(-.35f, -.39f), new Vector2(-.22f, -.53f));
        }

        private void Hourglass(VertexHelper mesh)
        {
            Polygon(mesh, Steel, new Vector2(-.51f, .66f), new Vector2(.51f, .66f),
                new Vector2(.37f, .27f), new Vector2(.11f, 0f), new Vector2(.37f, -.27f),
                new Vector2(.51f, -.66f), new Vector2(-.51f, -.66f), new Vector2(-.37f, -.27f),
                new Vector2(-.11f, 0f), new Vector2(-.37f, .27f));
            Polygon(mesh, Gold, new Vector2(-.35f, .47f), new Vector2(.35f, .47f), new Vector2(0f, .08f));
            Polygon(mesh, Gold, new Vector2(0f, -.23f), new Vector2(.36f, -.55f), new Vector2(-.36f, -.55f));
            Stroke(mesh, Gold, .17f, new Vector2(-.67f, .75f), new Vector2(.67f, .75f));
            Stroke(mesh, Gold, .17f, new Vector2(-.67f, -.75f), new Vector2(.67f, -.75f));
        }

        private void Anchor(VertexHelper mesh)
        {
            Circle(mesh, new Vector2(0f, .61f), .23f, Steel, true);
            Circle(mesh, new Vector2(0f, .61f), .1f, Ink, false);
            Stroke(mesh, Steel, .15f, new Vector2(0f, .41f), new Vector2(0f, -.72f));
            Stroke(mesh, Steel, .15f, new Vector2(-.39f, .19f), new Vector2(.39f, .19f));
            Stroke(mesh, Steel, .16f, new Vector2(-.68f, -.25f), new Vector2(-.53f, -.57f),
                new Vector2(0f, -.79f), new Vector2(.53f, -.57f), new Vector2(.68f, -.25f));
            Polygon(mesh, Steel, new Vector2(-.84f, -.39f), new Vector2(-.7f, -.12f), new Vector2(-.44f, -.32f));
            Polygon(mesh, Steel, new Vector2(.84f, -.39f), new Vector2(.7f, -.12f), new Vector2(.44f, -.32f));
        }

        private void Web(VertexHelper mesh)
        {
            for (int i = 0; i < 3; i++)
            {
                float angle = i * Mathf.PI / 3f;
                Vector2 end = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * .84f;
                Stroke(mesh, Steel, .08f, -end, end);
            }
            for (int ring = 1; ring <= 2; ring++)
            {
                float radius = ring * .33f;
                Vector2[] points = new Vector2[7];
                for (int i = 0; i < points.Length; i++)
                {
                    float angle = i * Mathf.PI / 3f;
                    points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                }
                Stroke(mesh, Aqua, .065f, points);
            }
        }

        private void Lock(VertexHelper mesh)
        {
            Stroke(mesh, Steel, .16f, new Vector2(-.36f, .08f), new Vector2(-.36f, .49f),
                new Vector2(-.2f, .73f), new Vector2(.2f, .73f), new Vector2(.36f, .49f), new Vector2(.36f, .08f));
            Polygon(mesh, Gold, new Vector2(-.65f, .2f), new Vector2(.65f, .2f),
                new Vector2(.65f, -.71f), new Vector2(-.65f, -.71f));
            Circle(mesh, new Vector2(0f, -.18f), .14f, Ink, false);
            Line(mesh, new Vector2(0f, -.2f), new Vector2(0f, -.48f), .13f, Ink);
        }

        private void Cards(VertexHelper mesh)
        {
            Polygon(mesh, Aqua, new Vector2(-.73f, .68f), new Vector2(.27f, .84f),
                new Vector2(.49f, -.62f), new Vector2(-.51f, -.78f));
            Polygon(mesh, Ivory, new Vector2(-.34f, .71f), new Vector2(.68f, .58f),
                new Vector2(.5f, -.84f), new Vector2(-.52f, -.71f));
            Polygon(mesh, Rose, new Vector2(.13f, .29f), new Vector2(.36f, -.08f),
                new Vector2(.06f, -.37f), new Vector2(-.17f, 0f));
        }

        private void Stroke(VertexHelper mesh, Color tint, float width, params Vector2[] points)
        {
            for (int i = 1; i < points.Length; i++) Line(mesh, points[i - 1], points[i], width + .11f, Ink);
            for (int i = 1; i < points.Length; i++) Line(mesh, points[i - 1], points[i], width, tint);
        }

        private void Line(VertexHelper mesh, Vector2 start, Vector2 end, float width, Color tint)
        {
            Vector2 delta = end - start;
            if (delta.sqrMagnitude < .000001f) return;
            Vector2 side = new Vector2(-delta.y, delta.x).normalized * width * .5f;
            Fill(mesh, tint, start - side, start + side, end + side, end - side);
        }

        private void Circle(VertexHelper mesh, Vector2 center, float radius, Color tint, bool outlined)
        {
            Vector2[] points = new Vector2[16];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / points.Length;
                points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            if (outlined) Polygon(mesh, tint, points); else Fill(mesh, tint, points);
        }

        private void Polygon(VertexHelper mesh, Color tint, params Vector2[] points)
        {
            // Fill first, then cover each boundary with a dark edge. Mesh-based
            // strokes stay crisp when the HUD scales and do not need a font.
            Fill(mesh, tint, points);
            for (int i = 0; i < points.Length; i++) Line(mesh, points[i], points[(i + 1) % points.Length], .075f, Ink);
        }

        private void Fill(VertexHelper mesh, Color tint, params Vector2[] points)
        {
            if (points.Length < 3) return;
            // Ear clipping supports concave symbols such as crosses, flames,
            // and lightning without filling across the notches in their outline.
            int first = mesh.currentVertCount;
            for (int i = 0; i < points.Length; i++)
                mesh.AddVert((Vector3)(bounds.center + (points[i] * glyphScale + glyphOrigin) * scale), tint * color, Vector2.zero);
            int[] remaining = new int[points.Length];
            float signedArea = 0f;
            for (int i = 0; i < points.Length; i++)
            {
                remaining[i] = i;
                Vector2 next = points[(i + 1) % points.Length];
                signedArea += points[i].x * next.y - next.x * points[i].y;
            }
            float winding = signedArea >= 0f ? 1f : -1f;
            int count = points.Length;
            int budget = count * count;
            while (count > 2 && budget-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < count; i++)
                {
                    int previous = remaining[(i + count - 1) % count];
                    int current = remaining[i];
                    int next = remaining[(i + 1) % count];
                    Vector2 a = points[previous], b = points[current], c = points[next];
                    if (Cross(b - a, c - b) * winding <= .000001f) continue;
                    bool occupied = false;
                    for (int j = 0; j < count; j++)
                    {
                        int other = remaining[j];
                        if (other == previous || other == current || other == next) continue;
                        Vector2 p = points[other];
                        if (Cross(b - a, p - a) * winding >= -.000001f &&
                            Cross(c - b, p - b) * winding >= -.000001f &&
                            Cross(a - c, p - c) * winding >= -.000001f)
                        { occupied = true; break; }
                    }
                    if (occupied) continue;
                    mesh.AddTriangle(first + previous, first + current, first + next);
                    for (int j = i; j < count - 1; j++) remaining[j] = remaining[j + 1];
                    count--;
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        private static Color Rgb(byte red, byte green, byte blue) => new Color32(red, green, blue, 255);
    }
}
