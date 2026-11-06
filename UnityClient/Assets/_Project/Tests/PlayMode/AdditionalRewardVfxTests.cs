using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator AdditionalVfx_UpgradeCommitsOnceCompletesAndDoesNotReplayOnRender()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            game.Session.GrantProfileGold(10000, "强化动效隔离验证");
            ActiveButton("Port Hotspot CardWorkshop").onClick.Invoke(); yield return null;
            ActiveButton("Workshop Entrance Upgrade").onClick.Invoke(); yield return null;
            ActiveButton("Workshop Preview Stage Naval").onClick.Invoke(); yield return null;
            while (ActiveButton("Select Upgrade Card 0") == null)
            {
                Assert.That(ActiveButton("Next Upgrade Cards").interactable, Is.True);
                ActiveButton("Next Upgrade Cards").onClick.Invoke(); yield return null;
            }
            ActiveButton("Select Upgrade Card 0").onClick.Invoke(); yield return null;
            decimal gold = game.Session.ProfileGold;
            int before = game.Session.GetCardUpgradeLevel("card_chain_shot"), cost = game.Session.GetCardUpgradeCost(0);
            Button stale = ActiveButton("Confirm Battle Card Upgrade");
            Assert.That(stale.interactable, Is.True);
            stale.onClick.Invoke();
            var motion = Object.FindObjectsByType<RewardSuccessMotion>(FindObjectsSortMode.None)
                .Single(view => view.name == "Card Upgrade Success VFX");
            Assert.That(motion.IsComplete, Is.False);
            Assert.That(game.Session.GetCardUpgradeLevel("card_chain_shot"), Is.EqualTo(before + 1));
            Assert.That(game.Session.ProfileGold, Is.EqualTo(gold - cost));
            string saved = PlayerPrefs.GetString(CoreGameplayDemoController.BattleCardUpgradeKey);
            Assert.That(saved, Does.Contain("card_chain_shot=" + (before + 1)));
            Assert.That(motion.GetComponentsInChildren<Selectable>(), Is.Empty, "No skip or modal input is added.");
            Assert.That(motion.GetComponentsInChildren<Graphic>().All(g => !g.raycastTarget), Is.True);
            yield return new WaitForSecondsRealtime(.35f);
            NavalBattleVisualCapture.Capture(game, "AdditionalVfx_01_CardUpgrade.png", "强化提交后当前卡面扫光、等级提示与变化数值短亮", "Battle Card Upgrade Panel");
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.That(motion.IsComplete, Is.True);
            Assert.That(GameObject.Find("Upgrade Comparison Body Current").GetComponent<Text>().text, Does.Not.Contain("#B76B12"));
            RenderResponsiveBattle(game); yield return null;
            Assert.That(Object.FindObjectsByType<RewardSuccessMotion>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(game.Session.ProfileGold, Is.EqualTo(gold - cost));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.BattleCardUpgradeKey), Is.EqualTo(saved));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AdditionalVfx_PackRevealUsesCommittedThreeCardsAndRestoresAfterCancellation()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(game.Session.GrantCardPacks("fire", 2), Is.True);
            typeof(CoreGameplayDemoController).GetMethod("SaveProgress", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
            ActiveButton("Port Hotspot CardWorkshop").onClick.Invoke(); yield return null;
            ActiveButton("Workshop Entrance Packs").onClick.Invoke(); yield return null;
            ActiveButton("Workshop Pack Select fire").onClick.Invoke(); yield return null;
            int copies = game.Session.CaptureCardPackProgress().cards.Sum(card => card.count);
            int packs = game.Session.GetCardPackCount("fire");
            Button.ButtonClickedEvent stale = ActiveButton("Workshop Packs Open Selected").onClick;
            stale.Invoke();
            var sequence = Object.FindFirstObjectByType<WorkshopPackRevealSequence>();
            Assert.That(sequence, Is.Not.Null);
            Assert.That(sequence.IsComplete, Is.False);
            Assert.That(ActiveButton("Workshop Packs Open Again").interactable, Is.False);
            Assert.That(game.Session.CaptureCardPackProgress().cards.Sum(card => card.count), Is.EqualTo(copies + 3));
            Assert.That(game.Session.GetCardPackCount("fire"), Is.EqualTo(packs - 1));
            string committed = PlayerPrefs.GetString(CoreGameplayDemoController.CardPackProgressKey);
            Assert.That(committed, Is.EqualTo(JsonUtility.ToJson(game.Session.CaptureCardPackProgress())));
            stale.Invoke(); ActiveButton("Workshop Packs Open Again").onClick.Invoke();
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.CardPackProgressKey), Is.EqualTo(committed));
            Assert.That(sequence.GetComponentsInChildren<Button>().Any(button => button.name.Contains("Skip")), Is.False);
            yield return new WaitForSecondsRealtime(.68f);
            NavalBattleVisualCapture.Capture(game, "AdditionalVfx_02_PackReveal.png", "已保存的三张奖励错峰翻面揭晓，不重复抽取或发放", "Workshop Packs Opening Table");
            yield return new WaitForSecondsRealtime(.85f);
            Assert.That(sequence.IsComplete, Is.True);
            Assert.That(ActiveButton("Workshop Packs Open Again").interactable, Is.True);
            RenderResponsiveBattle(game); yield return null;
            Assert.That(Object.FindObjectsByType<WorkshopPackRevealSequence>(FindObjectsSortMode.None).All(s => s.IsComplete), Is.True);
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.CardPackProgressKey), Is.EqualTo(committed));
            ActiveButton("Workshop Packs Open Again").onClick.Invoke();
            sequence = Object.FindObjectsByType<WorkshopPackRevealSequence>(FindObjectsSortMode.None).Single(s => !s.IsComplete);
            string second = PlayerPrefs.GetString(CoreGameplayDemoController.CardPackProgressKey);
            RectTransform[] holders = sequence.GetComponentsInChildren<RectTransform>()
                .Where(rect => rect.name.StartsWith("Workshop Pack Result ") && rect.name.EndsWith(" Slot")).ToArray();
            Assert.That(holders.Length, Is.EqualTo(3));
            game.enabled = false; yield return null;
            Assert.That(sequence.IsComplete, Is.True);
            foreach (RectTransform holder in holders)
            {
                Assert.That(holder.localScale, Is.EqualTo(Vector3.one));
                Assert.That(holder.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
                Assert.That(holder.GetComponentsInChildren<CanvasGroup>().Where(g => g.name.EndsWith(" Face")).All(g => g.alpha == 1f), Is.True);
            }
            game.enabled = true; yield return null;
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.CardPackProgressKey), Is.EqualTo(second));
            Assert.That(game.Session.CaptureCardPackProgress().cards.Sum(card => card.count), Is.EqualTo(copies + 6));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AdditionalVfx_RecruitmentAndCommittedAchievementAreLocalAndCancelWithOwner()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            game.Session.GrantProfileGold(10000, "招募动效隔离验证");
            CrewVariantDefinition crew = CrewNarrativeCatalog.ForPortAndProfession(game.Session.CurrentPort, 0);
            ActiveButton("Port Hotspot Tavern").onClick.Invoke(); yield return null;
            ActiveButton("Tavern Crew 0").onClick.Invoke(); yield return null;
            decimal gold = game.Session.ProfileGold;
            ActiveButton("Recruit Tavern Crew 0").onClick.Invoke();
            var contract = Object.FindObjectsByType<RewardSuccessMotion>(FindObjectsSortMode.None)
                .Single(view => view.name == "Recruitment Success VFX");
            var toast = Object.FindFirstObjectByType<AchievementToastMotion>();
            Assert.That(contract.IsComplete, Is.False);
            Assert.That(toast, Is.Not.Null, "The same committed recruitment saves the newly reached wealth milestone.");
            Assert.That(toast.IsComplete, Is.False);
            Assert.That(game.Session.ProfileGold, Is.EqualTo(gold - crew.ContractCost));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(crew.Id)), Is.EqualTo(1));
            Assert.That(contract.GetComponentsInChildren<Selectable>(), Is.Empty);
            Assert.That(toast.GetComponentsInChildren<Selectable>(), Is.Empty);
            Assert.That(toast.GetComponentsInChildren<Graphic>().All(g => !g.raycastTarget), Is.True);
            yield return new WaitForSecondsRealtime(.40f);
            NavalBattleVisualCapture.Capture(game, "AdditionalVfx_09_RecruitmentAndAchievement.png", "招募已提交后局部签约章与成就徽记扫光，档案继续可读", "Tavern Crew Detail");
            toast.enabled = false; yield return null;
            Assert.That(toast.IsComplete, Is.True);
            Assert.That(toast.GetComponent<CanvasGroup>().alpha, Is.Zero, "Disabling the animation itself must not leave a frozen notification.");
            toast.enabled = true; toast.Play(); yield return null;
            game.enabled = false; yield return null;
            Assert.That(contract.IsComplete, Is.True);
            Assert.That(toast.IsComplete, Is.True);
            Assert.That(toast.gameObject.activeSelf, Is.False);
            game.enabled = true; yield return null;
            typeof(CoreGameplayDemoController).GetMethod("SaveProgress", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
            Assert.That(toast.gameObject.activeSelf, Is.False, "Re-saving an earned milestone must not replay its toast.");
            Assert.That(game.Session.ProfileGold, Is.EqualTo(gold - crew.ContractCost));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
