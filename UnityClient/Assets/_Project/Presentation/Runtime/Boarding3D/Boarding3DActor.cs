using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShatteredSail.Presentation
{
    public enum Boarding3DAction { Idle, Walk, Turn, Attack, Cast, Hit, Guard, Heal, Death }

    /// <summary>A replaceable, fully three-dimensional prototype rig. Its clock is owned by the trial.</summary>
    public sealed class Boarding3DActor : MonoBehaviour
    {
        private enum Bone { Hips, Spine, Head, ArmL, ForearmL, ArmR, ForearmR, ThighL, ShinL, ThighR, ShinR, TailL, TailR, Count }
        private readonly Transform[] bones = new Transform[(int)Bone.Count];
        private readonly Quaternion[] rest = new Quaternion[(int)Bone.Count];
        private readonly Quaternion[] blendFrom = new Quaternion[(int)Bone.Count];
        private readonly Vector3[] pose = new Vector3[(int)Bone.Count];
        private readonly List<Mesh> ownedMeshes = new List<Mesh>();
        private readonly List<Material> ownedMaterials = new List<Material>();
        private Vector3 hipsRest, blendPosition;
        private float elapsed, duration = .9f, clock, transition;
        private bool initialized;
        public Boarding3DAction Action { get; private set; }
        public float Progress => Action == Boarding3DAction.Idle ? 1f : Mathf.Clamp01(elapsed / duration);
        public bool Paused { get; set; }
        public Transform WeaponTip { get; private set; }

        public static Boarding3DActor Create(Transform parent, bool enemy)
        {
            var root = new GameObject(enemy ? "Crimson Corsair 3D" : "Teal Captain 3D");
            root.transform.SetParent(parent, false);
            var actor = root.AddComponent<Boarding3DActor>();
            actor.Build(enemy);
            actor.ResetPose();
            return actor;
        }

        public void Play(Boarding3DAction action, float duration = .9f)
        {
            if (!initialized || Paused) return;
            if (Action == Boarding3DAction.Death && action != Boarding3DAction.Idle) return;
            for (int i = 0; i < bones.Length; i++) blendFrom[i] = bones[i].localRotation;
            blendPosition = bones[0].localPosition;
            Action = action;
            this.duration = float.IsNaN(duration) || float.IsInfinity(duration) ? .9f : Mathf.Max(.25f, duration);
            elapsed = transition = 0f;
        }

        public void Tick(float delta)
        {
            if (!initialized || Paused || !isActiveAndEnabled || float.IsNaN(delta) || float.IsInfinity(delta) || delta <= 0f) return;
            clock += delta;
            elapsed += delta;
            transition += delta;
            if (Action != Boarding3DAction.Idle && Action != Boarding3DAction.Death && elapsed >= duration)
            {
                Play(Boarding3DAction.Idle);
                transition = 0f;
            }
            ApplyPose();
        }

        public void ResetPose()
        {
            if (!initialized) return;
            Paused = false;
            Action = Boarding3DAction.Idle;
            clock = elapsed = 0f;
            transition = 1f;
            for (int i = 0; i < bones.Length; i++) bones[i].localRotation = blendFrom[i] = rest[i];
            bones[0].localPosition = blendPosition = hipsRest;
            ApplyPose();
        }

        private void ApplyPose()
        {
            for (int i = 0; i < pose.Length; i++) pose[i] = Vector3.zero;
            float breathing = Mathf.Sin(clock * 2.3f);
            Vector3 offset = new Vector3(0f, breathing * .009f, 0f);
            Set(Bone.Spine, breathing * .65f, 0f, 0f);
            Set(Bone.ArmL, -12f + breathing, 0f, 8f);
            Set(Bone.ForearmL, -15f, 0f, 0f);
            Set(Bone.ArmR, -19f + breathing, 0f, -12f);
            Set(Bone.ForearmR, -24f, 0f, 0f);
            Set(Bone.ThighL, 0f, 0f, 3f);
            Set(Bone.ThighR, 0f, 0f, -3f);
            Set(Bone.Head, -1f, 0f, -1f);
            float t = Progress;
            switch (Action)
            {
                case Boarding3DAction.Walk:
                case Boarding3DAction.Turn:
                    float cycle = elapsed * 8.2f;
                    float stride = Mathf.Sin(cycle), opposite = -stride;
                    float entry = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / .12f));
                    float exit = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((duration - elapsed) / .16f));
                    float gait = entry * exit * (Action == Boarding3DAction.Turn ? .6f : 1f);
                    Set(Bone.ThighL, stride * 29f * gait, 0f, 3f);
                    Set(Bone.ThighR, opposite * 29f * gait, 0f, -3f);
                    Set(Bone.ShinL, Mathf.Max(0f, -stride) * 43f * gait, 0f, 0f);
                    Set(Bone.ShinR, Mathf.Max(0f, stride) * 43f * gait, 0f, 0f);
                    Set(Bone.ArmL, -12f - stride * 23f * gait, 0f, 8f);
                    Set(Bone.ArmR, -19f + stride * 17f * gait, 0f, -12f);
                    Set(Bone.Spine, 4f * gait, stride * 5f * gait, -stride * 2f * gait);
                    Set(Bone.Head, -4f * gait, -stride * 3f * gait, 0f);
                    Set(Bone.TailL, -Mathf.Max(0f, stride) * 22f * gait, 0f, 0f);
                    Set(Bone.TailR, -Mathf.Max(0f, opposite) * 22f * gait, 0f, 0f);
                    offset.y += Mathf.Abs(Mathf.Sin(cycle)) * .027f * gait;
                    break;
                case Boarding3DAction.Attack:
                    // The windup has finished before contact. In the contact pose the
                    // shoulder/elbow sum aims the blade along the actor's +Z axis;
                    // additive torso/shoulder yaw previously sent it out sideways.
                    float wind = Window(t, 0f, .24f, .24f, .43f);
                    float slash = Window(t, .24f, .45f, .54f, .96f);
                    Set(Bone.Spine, -9f * wind + 8f * slash, -18f * wind - 4f * slash, -4f * wind);
                    Set(Bone.Head, 6f * wind - 5f * slash, 10f * wind + 4f * slash, 0f);
                    Set(Bone.ArmR, -19f - 125f * wind - 17f * slash, -8f * wind, -12f - 28f * wind + 7f * slash);
                    Set(Bone.ForearmR, -24f - 45f * wind - 36f * slash, 0f, 0f);
                    Set(Bone.ArmL, -12f - 28f * wind + 22f * slash, 0f, 8f + 22f * slash);
                    Set(Bone.ForearmL, -15f - 34f * wind - 20f * slash, 0f, 0f);
                    // A lifted approach step and a separate recovery step accompany
                    // the stage's root motion, with both soles low during contact.
                    float advanceStep = Window(t, .11f, .24f, .24f, .40f);
                    float retreatStep = Window(t, .58f, .73f, .73f, .94f);
                    Set(Bone.ThighL, -32f * advanceStep + 18f * retreatStep - 9f * slash, 0f, 3f);
                    Set(Bone.ShinL, 49f * advanceStep + 8f * slash, 0f, 0f);
                    Set(Bone.ThighR, 17f * advanceStep - 29f * retreatStep + 5f * slash, 0f, -3f);
                    Set(Bone.ShinR, 43f * retreatStep, 0f, 0f);
                    offset += new Vector3(0f, -.018f * slash + .025f * (advanceStep + retreatStep), -.025f * wind);
                    Set(Bone.TailL, -18f * slash, 0f, 0f);
                    Set(Bone.TailR, -12f * slash, 0f, 0f);
                    break;
                case Boarding3DAction.Cast:
                case Boarding3DAction.Heal:
                    float gather = Window(t, 0f, .25f, .65f, 1f);
                    float release = Window(t, .38f, .58f, .68f, 1f);
                    Set(Bone.Spine, -6f * gather + 13f * release, 12f * gather, 0f);
                    Set(Bone.ArmL, -12f - 76f * gather - 17f * release, -17f * gather, 8f + 22f * gather);
                    Set(Bone.ForearmL, -15f - 65f * gather + 60f * release, 0f, 0f);
                    Set(Bone.ArmR, -19f - 32f * gather, 0f, -12f - 18f * gather);
                    Set(Bone.ForearmR, -24f - 36f * gather, 0f, 0f);
                    Set(Bone.Head, -9f * gather + 12f * release, 8f * gather, 0f);
                    offset.y += .025f * gather;
                    break;
                case Boarding3DAction.Guard:
                    float guard = Window(t, 0f, .20f, .69f, 1f);
                    Set(Bone.Spine, 8f * guard, -15f * guard, 0f);
                    Set(Bone.ArmR, -19f - 62f * guard, -25f * guard, -12f + 36f * guard);
                    Set(Bone.ForearmR, -24f - 54f * guard, 0f, 0f);
                    Set(Bone.ArmL, -12f - 54f * guard, 30f * guard, 8f - 24f * guard);
                    Set(Bone.ForearmL, -15f - 65f * guard, 0f, 0f);
                    Set(Bone.ThighL, -14f * guard, 0f, 8f * guard);
                    Set(Bone.ThighR, -14f * guard, 0f, -8f * guard);
                    Set(Bone.ShinL, 23f * guard, 0f, 0f);
                    Set(Bone.ShinR, 23f * guard, 0f, 0f);
                    offset.y -= .065f * guard;
                    break;
                case Boarding3DAction.Hit:
                    float recoil = Window(t, 0f, .13f, .2f, 1f);
                    Set(Bone.Spine, -24f * recoil, -12f * recoil, 9f * recoil);
                    Set(Bone.Head, -17f * recoil, 14f * recoil, -8f * recoil);
                    Set(Bone.ArmL, -12f + 24f * recoil, 0f, 8f + 18f * recoil);
                    Set(Bone.ArmR, -19f + 20f * recoil, 0f, -12f - 18f * recoil);
                    offset.z -= .12f * recoil;
                    offset.y -= .025f * recoil;
                    break;
                case Boarding3DAction.Death:
                    float fall = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.13f, .84f, t));
                    Set(Bone.Hips, -88f * fall, 0f, 8f * fall);
                    Set(Bone.Spine, -12f * Mathf.Sin(t * Mathf.PI), 0f, 0f);
                    Set(Bone.Head, -12f * fall, 20f * fall, 0f);
                    Set(Bone.ArmL, -12f + 16f * fall, 0f, 8f + 42f * fall);
                    Set(Bone.ArmR, -19f + 20f * fall, 0f, -12f - 50f * fall);
                    Set(Bone.ForearmL, -15f + 12f * fall, 0f, 0f);
                    Set(Bone.ForearmR, -24f + 18f * fall, 0f, 0f);
                    Set(Bone.ThighL, -12f * fall, 0f, 8f * fall);
                    Set(Bone.ThighR, -7f * fall, 0f, -10f * fall);
                    Set(Bone.ShinL, 12f * fall, 0f, 0f);
                    Set(Bone.ShinR, 6f * fall, 0f, 0f);
                    offset = new Vector3(0f, -.77f * fall, -.48f * fall);
                    break;
            }
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(transition / .14f));
            for (int i = 0; i < bones.Length; i++)
                bones[i].localRotation = Quaternion.Slerp(blendFrom[i], rest[i] * Quaternion.Euler(pose[i]), blend);
            bones[0].localPosition = Vector3.Lerp(blendPosition, hipsRest + offset, blend);
        }

        private void Set(Bone bone, float x, float y, float z) => pose[(int)bone] = new Vector3(x, y, z);
        private static float Window(float t, float start, float peak, float release, float end) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, peak, t)) *
            (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(release, end, t)));

        private void Build(bool enemy)
        {
            Material coat = Mat(enemy ? new Color(.34f, .055f, .07f) : new Color(.025f, .24f, .27f), .05f);
            Material coatLight = Mat(enemy ? new Color(.55f, .11f, .09f) : new Color(.065f, .36f, .38f), .05f);
            Material dark = Mat(new Color(.038f, .045f, .052f), .12f);
            Material leather = Mat(new Color(.13f, .068f, .038f), .05f);
            Material gold = Mat(new Color(.80f, .52f, .18f), .65f);
            Material skin = Mat(enemy ? new Color(.56f, .33f, .20f) : new Color(.76f, .52f, .32f), .05f);
            Material linen = Mat(new Color(.83f, .79f, .63f), .02f);
            Material blade = Mat(new Color(.63f, .73f, .78f), .85f);
            Material hair = Mat(enemy ? new Color(.12f, .075f, .04f) : new Color(.19f, .13f, .085f), .02f);

            Transform hips = Joint(Bone.Hips, transform, new Vector3(0f, .98f, 0f));
            hipsRest = hips.localPosition;
            Section("Pelvis", hips, new Vector3(0f, -.04f, 0f), .18f, .25f, .16f, .25f, .16f, 8, dark);
            Transform spine = Joint(Bone.Spine, hips, new Vector3(0f, .03f, 0f));
            Section("Tailored coat", spine, Vector3.zero, .58f, .245f, .145f, .31f, .17f, 8, coat);
            Section("Linen shirt", spine, new Vector3(0f, .08f, .145f), .46f, .115f, .028f, .12f, .028f, 4, linen);
            Box("Left lapel", spine, new Vector3(-.13f, .37f, .17f), new Vector3(.075f, .32f, .036f), coatLight, new Vector3(0f, 0f, -12f));
            Box("Right lapel", spine, new Vector3(.13f, .37f, .17f), new Vector3(.075f, .32f, .036f), coatLight, new Vector3(0f, 0f, 12f));
            Box("Cross belt", spine, new Vector3(.015f, .31f, .198f), new Vector3(.075f, .60f, .035f), leather, new Vector3(0f, 0f, -30f));
            Section("Waist belt", spine, new Vector3(0f, .025f, 0f), .095f, .255f, .17f, .255f, .17f, 8, leather);
            Box("Brass buckle", spine, new Vector3(0f, .073f, .181f), new Vector3(.135f, .10f, .025f), gold);
            Box("Buckle inset", spine, new Vector3(0f, .073f, .199f), new Vector3(.075f, .05f, .012f), dark);
            for (int i = 0; i < 3; i++)
                Ellipsoid("Coat button", spine, new Vector3(-.18f, .19f + i * .12f, .167f), new Vector3(.021f, .021f, .012f), gold, 6, 4);
            Section("Neck", spine, new Vector3(0f, .58f, 0f), .16f, .09f, .085f, .09f, .085f, 8, skin);
            Section("Standing collar", spine, new Vector3(0f, .56f, -.008f), .10f, .14f, .125f, .145f, .13f, 8, coatLight);

            Transform head = Joint(Bone.Head, spine, new Vector3(0f, .72f, 0f));
            Ellipsoid("Angular face", head, new Vector3(0f, .06f, .018f), new Vector3(.158f, .215f, .155f), skin, 10, 6);
            Ellipsoid("Hair", head, new Vector3(0f, .12f, -.045f), new Vector3(.164f, .185f, .132f), hair, 10, 5);
            Ellipsoid("Left ear", head, new Vector3(-.16f, .045f, .006f), new Vector3(.033f, .062f, .04f), skin, 6, 4);
            Ellipsoid("Right ear", head, new Vector3(.16f, .045f, .006f), new Vector3(.033f, .062f, .04f), skin, 6, 4);
            Ellipsoid("Nose", head, new Vector3(0f, .045f, .164f), new Vector3(.032f, .057f, .049f), skin, 5, 4);
            Box("Left brow", head, new Vector3(-.065f, .115f, .148f), new Vector3(.075f, .018f, .015f), hair, new Vector3(0f, 0f, -8f));
            Box("Right brow", head, new Vector3(.065f, .115f, .148f), new Vector3(.075f, .018f, .015f), hair, new Vector3(0f, 0f, 8f));
            Box("Left eye", head, new Vector3(-.063f, .082f, .158f), new Vector3(.041f, .022f, .011f), linen);
            Box("Right eye", head, new Vector3(.063f, .082f, .158f), new Vector3(.041f, .022f, .011f), linen);
            Box("Left pupil", head, new Vector3(-.061f, .082f, .166f), new Vector3(.013f, .018f, .007f), dark);
            Box("Right pupil", head, new Vector3(.065f, .082f, .166f), new Vector3(.013f, .018f, .007f), dark);
            Ellipsoid("Beard", head, new Vector3(0f, -.075f, .08f), new Vector3(.115f, .105f, .095f), hair, 7, 4);
            Box("Moustache", head, new Vector3(0f, -.012f, .159f), new Vector3(.14f, .026f, .025f), hair);
            Ellipsoid("Gold earring", head, new Vector3(-.185f, -.005f, .025f), new Vector3(.022f, .033f, .012f), gold, 7, 4);
            Section("Hat crown", head, new Vector3(0f, .20f, -.018f), .15f, .20f, .17f, .175f, .15f, 8, dark);
            HatBrim(head, dark, gold);
            Box("Hat badge", head, new Vector3(0f, .281f, .180f), new Vector3(.076f, .069f, .023f), gold, new Vector3(-12f, 0f, 0f));
            if (enemy) Box("Eye patch", head, new Vector3(.063f, .082f, .176f), new Vector3(.067f, .048f, .018f), dark, new Vector3(0f, 0f, 8f));

            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? -1f : 1f;
                Transform arm = Joint(side == 0 ? Bone.ArmL : Bone.ArmR, spine, new Vector3(sign * .30f, .51f, 0f));
                Ellipsoid("Shoulder", arm, Vector3.zero, new Vector3(.125f, .13f, .13f), coat, 8, 5);
                Section("Upper sleeve", arm, new Vector3(0f, -.30f, 0f), .30f, .092f, .095f, .115f, .115f, 8, coat);
                Transform forearm = Joint(side == 0 ? Bone.ForearmL : Bone.ForearmR, arm, new Vector3(0f, -.30f, 0f));
                Section("Lower sleeve", forearm, new Vector3(0f, -.27f, 0f), .27f, .072f, .073f, .09f, .095f, 8, coatLight);
                Section("Gold cuff", forearm, new Vector3(0f, -.28f, 0f), .063f, .085f, .085f, .085f, .085f, 8, gold);
                Ellipsoid("Hand", forearm, new Vector3(0f, -.34f, .01f), new Vector3(.069f, .09f, .07f), skin, 7, 5);
                if (side == 1) BuildSword(forearm, leather, gold, blade);
                else Ellipsoid("Thumb", forearm, new Vector3(.058f, -.31f, .038f), new Vector3(.029f, .042f, .028f), skin, 6, 4);
                Transform thigh = Joint(side == 0 ? Bone.ThighL : Bone.ThighR, hips, new Vector3(sign * .145f, -.055f, 0f));
                Section("Trouser thigh", thigh, new Vector3(0f, -.40f, 0f), .40f, .095f, .10f, .13f, .13f, 8, dark);
                Transform shin = Joint(side == 0 ? Bone.ShinL : Bone.ShinR, thigh, new Vector3(0f, -.40f, 0f));
                Section("Tall boot", shin, new Vector3(0f, -.40f, 0f), .40f, .085f, .09f, .105f, .115f, 8, leather);
                Section("Boot cuff", shin, new Vector3(0f, -.10f, 0f), .075f, .115f, .12f, .12f, .125f, 8, leather);
                Box("Boot foot", shin, new Vector3(0f, -.438f, .058f), new Vector3(.18f, .13f, .32f), leather);
                Box("Boot sole", shin, new Vector3(0f, -.493f, .058f), new Vector3(.19f, .025f, .33f), dark);
                Box("Boot buckle", shin, new Vector3(sign * .093f, -.18f, .031f), new Vector3(.025f, .055f, .07f), gold);
                Transform tail = Joint(side == 0 ? Bone.TailL : Bone.TailR, hips, new Vector3(sign * .145f, .06f, -.12f));
                Section("Coat tail", tail, new Vector3(0f, -.42f, -.015f), .42f, .14f, .045f, .105f, .04f, 4, coat);
                Box("Tail gold piping", tail, new Vector3(sign * .09f, -.22f, -.057f), new Vector3(.018f, .37f, .016f), gold, new Vector3(0f, 0f, -sign * 5f));
            }
            Ellipsoid("Captain epaulette", bones[(int)Bone.ArmL], new Vector3(0f, .045f, 0f), new Vector3(.16f, .068f, .145f), gold, 8, 4);
            for (int i = 0; i < bones.Length; i++) rest[i] = bones[i].localRotation;
            initialized = true;
        }

        private void BuildSword(Transform hand, Material leather, Material gold, Material steel)
        {
            Transform sword = new GameObject("Cutlass").transform;
            sword.SetParent(hand, false);
            sword.localPosition = new Vector3(0f, -.34f, .035f);
            sword.localRotation = Quaternion.Euler(0f, 0f, -8f);
            Section("Leather grip", sword, new Vector3(0f, -.105f, 0f), .21f, .035f, .035f, .035f, .035f, 7, leather);
            Ellipsoid("Pommel", sword, new Vector3(0f, .115f, 0f), new Vector3(.045f, .045f, .045f), gold, 7, 4);
            Box("Cutlass guard", sword, new Vector3(0f, -.13f, 0f), new Vector3(.22f, .032f, .07f), gold);
            Box("Knuckle bow", sword, new Vector3(.095f, -.01f, 0f), new Vector3(.026f, .25f, .032f), gold, new Vector3(0f, 0f, -8f));
            Vector2[] outline = { new Vector2(-.036f, -.15f), new Vector2(.035f, -.15f), new Vector2(.051f, -.39f),
                new Vector2(.09f, -.63f), new Vector2(.145f, -.87f), new Vector2(.12f, -1.02f),
                new Vector2(.034f, -.92f), new Vector2(-.004f, -.69f), new Vector2(-.024f, -.42f) };
            Mesh blade = Extrude(outline, .016f);
            MeshPart("Curved steel blade", sword, blade, steel);
            WeaponTip = new GameObject("Weapon Tip").transform;
            WeaponTip.SetParent(sword, false);
            WeaponTip.localPosition = new Vector3(.12f, -1.02f, 0f);
        }

        private void HatBrim(Transform head, Material felt, Material trim)
        {
            Vector2[] contour = { new Vector2(-.38f, -.12f), new Vector2(-.20f, -.24f), new Vector2(.20f, -.24f),
                new Vector2(.38f, -.12f), new Vector2(.20f, .18f), new Vector2(0f, .24f), new Vector2(-.20f, .18f) };
            Transform brim = MeshPart("Tricorn brim", head, Extrude(contour, .045f), felt);
            brim.localPosition = new Vector3(0f, .225f, 0f);
            brim.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Box("Front folded brim", head, new Vector3(0f, .27f, .155f), new Vector3(.43f, .11f, .047f), felt, new Vector3(-26f, 0f, 0f));
            Box("Gold hat band", head, new Vector3(0f, .315f, .176f), new Vector3(.43f, .017f, .018f), trim, new Vector3(-26f, 0f, 0f));
        }

        private Transform Joint(Bone bone, Transform parent, Vector3 position)
        {
            Transform joint = new GameObject(bone.ToString()).transform;
            joint.SetParent(parent, false);
            joint.localPosition = position;
            bones[(int)bone] = joint;
            return joint;
        }

        private Material Mat(Color color, float metal)
        {
            Shader shader = GraphicsSettings.currentRenderPipeline != null ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader) { name = "Owned pirate material", color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metal);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", metal > .5f ? .48f : .2f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", metal > .5f ? .48f : .2f);
            ownedMaterials.Add(material);
            return material;
        }

        private Transform MeshPart(string label, Transform parent, Mesh mesh, Material material)
        {
            var part = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(parent, false);
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            ownedMeshes.Add(mesh);
            return part.transform;
        }

        private void Box(string label, Transform parent, Vector3 position, Vector3 size, Material material, Vector3 rotation = default)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            Vector3 a = size * -.5f, b = size * .5f;
            Quad(vertices, triangles, new Vector3(a.x,a.y,b.z), new Vector3(b.x,a.y,b.z), new Vector3(b.x,b.y,b.z), new Vector3(a.x,b.y,b.z));
            Quad(vertices, triangles, new Vector3(b.x,a.y,a.z), new Vector3(a.x,a.y,a.z), new Vector3(a.x,b.y,a.z), new Vector3(b.x,b.y,a.z));
            Quad(vertices, triangles, new Vector3(a.x,a.y,a.z), new Vector3(a.x,a.y,b.z), new Vector3(a.x,b.y,b.z), new Vector3(a.x,b.y,a.z));
            Quad(vertices, triangles, new Vector3(b.x,a.y,b.z), new Vector3(b.x,a.y,a.z), new Vector3(b.x,b.y,a.z), new Vector3(b.x,b.y,b.z));
            Quad(vertices, triangles, new Vector3(a.x,b.y,b.z), new Vector3(b.x,b.y,b.z), new Vector3(b.x,b.y,a.z), new Vector3(a.x,b.y,a.z));
            Quad(vertices, triangles, new Vector3(a.x,a.y,a.z), new Vector3(b.x,a.y,a.z), new Vector3(b.x,a.y,b.z), new Vector3(a.x,a.y,b.z));
            Transform part = MeshPart(label, parent, MakeMesh(vertices, triangles), material);
            part.localPosition = position;
            part.localRotation = Quaternion.Euler(rotation);
        }

        private void Section(string label, Transform parent, Vector3 position, float height, float bottomX, float bottomZ, float topX, float topZ, int count, Material material)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int i = 0; i < count; i++)
            {
                float a = (i + .5f) * Mathf.PI * 2f / count, b = (i + 1.5f) * Mathf.PI * 2f / count;
                Vector3 p = new Vector3(Mathf.Cos(a) * bottomX, 0f, Mathf.Sin(a) * bottomZ);
                Vector3 q = new Vector3(Mathf.Cos(b) * bottomX, 0f, Mathf.Sin(b) * bottomZ);
                Vector3 r = new Vector3(Mathf.Cos(b) * topX, height, Mathf.Sin(b) * topZ);
                Vector3 s = new Vector3(Mathf.Cos(a) * topX, height, Mathf.Sin(a) * topZ);
                Quad(vertices, triangles, q, p, s, r);
                Tri(vertices, triangles, Vector3.zero, p, q);
                Tri(vertices, triangles, Vector3.up * height, r, s);
            }
            MeshPart(label, parent, MakeMesh(vertices, triangles), material).localPosition = position;
        }

        private void Ellipsoid(string label, Transform parent, Vector3 center, Vector3 radii, Material material, int longitude, int latitude)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int y = 0; y < latitude; y++)
                for (int x = 0; x < longitude; x++)
                {
                    Vector3 a = SpherePoint(x, y, longitude, latitude, radii);
                    Vector3 b = SpherePoint(x + 1, y, longitude, latitude, radii);
                    Vector3 c = SpherePoint(x + 1, y + 1, longitude, latitude, radii);
                    Vector3 d = SpherePoint(x, y + 1, longitude, latitude, radii);
                    Quad(vertices, triangles, a, b, c, d);
                }
            MeshPart(label, parent, MakeMesh(vertices, triangles), material).localPosition = center;
        }

        private static Vector3 SpherePoint(int x, int y, int longitude, int latitude, Vector3 radii)
        {
            float azimuth = x * 2f * Mathf.PI / longitude, polar = y * Mathf.PI / latitude;
            return Vector3.Scale(new Vector3(Mathf.Sin(polar) * Mathf.Cos(azimuth), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(azimuth)), radii);
        }

        private static Mesh Extrude(Vector2[] contour, float thickness)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            Vector2 center = Vector2.zero;
            foreach (Vector2 point in contour) center += point;
            center /= contour.Length;
            float signedArea = 0f;
            for (int i = 0; i < contour.Length; i++) { Vector2 p = contour[i], q = contour[(i + 1) % contour.Length]; signedArea += p.x * q.y - q.x * p.y; }
            for (int i = 0; i < contour.Length; i++)
            {
                Vector2 p = contour[i], q = contour[(i + 1) % contour.Length];
                if (signedArea < 0f) { Vector2 swap = p; p = q; q = swap; }
                Vector3 a = new Vector3(p.x, p.y, -thickness * .5f), b = new Vector3(q.x, q.y, -thickness * .5f);
                Vector3 c = new Vector3(q.x, q.y, thickness * .5f), d = new Vector3(p.x, p.y, thickness * .5f);
                Quad(vertices, triangles, a, b, c, d);
                Tri(vertices, triangles, new Vector3(center.x, center.y, thickness * .5f), d, c);
                Tri(vertices, triangles, new Vector3(center.x, center.y, -thickness * .5f), b, a);
            }
            return MakeMesh(vertices, triangles);
        }

        private static void Quad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int n = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2);
            triangles.Add(n); triangles.Add(n + 2); triangles.Add(n + 3);
        }

        private static void Tri(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            int n = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2);
        }

        private static Mesh MakeMesh(List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh { name = "Owned pirate geometry" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in ownedMeshes) if (mesh != null) Release(mesh);
            foreach (Material material in ownedMaterials) if (material != null) Release(material);
            ownedMeshes.Clear(); ownedMaterials.Clear();
        }

        private static void Release(Object value)
        {
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
