using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private BoardingSchoolCastGraphic activeBoardingSchoolCast;

        private void CancelBoardingSchoolCastPresentation()
        {
            if (activeBoardingSchoolCast != null) activeBoardingSchoolCast.Cancel();
            activeBoardingSchoolCast = null;
        }

        // Runs only after PlayCard succeeds, alongside the committed attack/guard/heal feedback.
        private IEnumerator AnimateBoardingSchoolCast(RectTransform stage, RectTransform player, RectTransform enemy,
            BattleSchoolAttackKind school, BoardingSchoolCastRole role)
        {
            if (!isActiveAndEnabled || stage == null || !stage.gameObject.activeInHierarchy || player == null || enemy == null) yield break;
            CancelBoardingSchoolCastPresentation();
            // The committed card already selected its one actor action. School FX do not replace it.
            Vector2 destination = BattleEffectPosition(stage, BattleSchoolAttackPresentation.TargetsEnemy(role) ? enemy : player);
            if (role == BoardingSchoolCastRole.Field)
                destination = (BattleEffectPosition(stage, player) + BattleEffectPosition(stage, enemy)) * .5f;
            RectTransform root = CreateRect("Boarding School Cast " + school + " " + role, stage, stage.rect.size, Vector2.zero);
            var graphic = root.gameObject.AddComponent<BoardingSchoolCastGraphic>();
            activeBoardingSchoolCast = graphic;
            graphic.Configure(school, role, BoardingActionOrigin(stage, player), destination);
            Vector2 ground = BoardingSchoolGround(stage, BattleSchoolAttackPresentation.TargetsEnemy(role) ? enemy : player);
            if (role == BoardingSchoolCastRole.Field) ground = (BoardingSchoolGround(stage, player) + BoardingSchoolGround(stage, enemy)) * .5f;
            graphic.ConfigureEnvironment(ground, stage.rect.yMax - 12f);
            Text label = CreateText("Boarding School Cast Label", root, new Vector2(360f, 40f), new Vector2(0f, -42f),
                22, FontStyle.Bold, TextAnchor.MiddleCenter, Color.Lerp(BattleSchoolAttackGraphic.Tint(school), Color.white, .4f));
            label.raycastTarget = false;
            label.text = SchoolCastName(school) + " · " + SchoolCastVerb(role);
            AddSceneLabelOutline(label);
            graphic.Play(this);
            try
            {
                while (graphic != null && graphic.IsPlaying)
                {
                    if (label != null)
                    {
                        float p = graphic.Progress;
                        Color c = label.color; c.a = Mathf.Clamp01((1f - p) * 3f); label.color = c;
                        label.rectTransform.anchoredPosition = new Vector2(0f, -42f + 12f * p);
                    }
                    yield return null;
                }
            }
            finally
            {
                if (graphic != null) graphic.Cancel();
                if (activeBoardingSchoolCast == graphic) activeBoardingSchoolCast = null;
            }
        }

        private static Vector2 BoardingSchoolGround(RectTransform stage, RectTransform party) =>
            stage.InverseTransformPoint(party.TransformPoint(new Vector3(0f, party.rect.yMin, 0f)));

        private static string SchoolCastName(BattleSchoolAttackKind school)
        {
            switch (school)
            {
                case BattleSchoolAttackKind.Fire: return "火焰";
                case BattleSchoolAttackKind.Arcane: return "秘术";
                case BattleSchoolAttackKind.Curse: return "诅咒";
                case BattleSchoolAttackKind.Tide: return "潮汐";
                case BattleSchoolAttackKind.Thunder: return "雷霆";
                default: return "血契";
            }
        }

        private static string SchoolCastVerb(BoardingSchoolCastRole role)
        {
            switch (role)
            {
                case BoardingSchoolCastRole.Attack: return "释放";
                case BoardingSchoolCastRole.Affliction: return "施咒";
                case BoardingSchoolCastRole.Guard: return "护持";
                case BoardingSchoolCastRole.Heal: return "复苏";
                case BoardingSchoolCastRole.Charge: return "蓄势";
                case BoardingSchoolCastRole.Field: return "展开";
                default: return "凝聚";
            }
        }
    }
}
