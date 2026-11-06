using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    // A separate fixture deliberately has no reset/restore setup. Only the normal
    // New Game callback creates this audit profile, which is retained afterwards.
    public sealed class CampaignBossReviewNewProfileTests
    {
        [Serializable] private sealed class Record
        {
            public string stage, utc, company, product, persistentDataPath, gold, port, phase, ship, warehouseIncome;
            public int hull, maxHull, ordinaryCrew, specialCrew, crewCapacity, travelGold, water, food, powder, cannonballs, wood, medicine, fragments;
            public bool tutorialActive;
            public string[] components, keepsakes, displayed, seenSteps, ownedShips, recruitedCrew, deckCards, cardUpgrades;
        }
        private static readonly List<Record> records = new List<Record>();

        [UnityTest, Timeout(480000)]
        public IEnumerator NewProfile_RealMenuComicTutorialAndPort_PreservesAuditSave()
        {
            Assert.That(Application.companyName, Is.EqualTo("ShatteredSailCampaignBossReview"));
            Assert.That(Application.productName, Is.EqualTo("CampaignBossReview20261027NewGame"));
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
            yield return null; yield return null;
            var menu=Object.FindFirstObjectByType<MainMenuController>();
            Assert.That(menu, Is.Not.Null);
            Assert.That(menu.HasSaveData, Is.False,"Refuse to overwrite a prior audit save; use a new identity for a retry.");
            NavalBattleVisualCapture.Capture(null,"00_New_Profile_Menu.png","真实主菜单；隔离身份还没有存档","New Game");
            Click("New Game");
            yield return null; yield return null;
            var game=Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(game, Is.Not.Null);
            Snapshot(game,"new_game_before_comic",null);
            NavalBattleVisualCapture.Capture(game,"01_Opening_Comic.png","真实新游戏入口启动开场漫画","Opening Comic Overlay");
            yield return MainMenuSceneTests.ClickThroughOpeningComic(game);
            Assert.That(game.IsTutorialActive,Is.True);
            Assert.That(game.OwnedComponents.Count,Is.Zero,"No preloaded equipment is allowed.");
            var seen=new HashSet<string>();var captured=new HashSet<string>();
            MethodInfo drive=typeof(CoreGameplayDemoSceneTests).GetMethod("DriveGuidedTutorial",BindingFlags.Static|BindingFlags.NonPublic);
            Assert.That(drive,Is.Not.Null);
            // This existing driver reads visible state and clicks the actual legal
            // controls. It does not seed money, cargo, hands, completion or rewards.
            yield return (IEnumerator)drive.Invoke(null,new object[]{game,"Complete",seen,true,captured});
            Assert.That(game.IsTutorialActive,Is.False);
            Assert.That(game.Session.Phase,Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialCompletedKey),Is.EqualTo(1));
            Assert.That(game.WarehouseCollection.IsDisplayed(CoreWarehouseCollection.TutorialTreasureId),Is.True);
            Assert.That(game.OwnedComponents.Any(c=>c.ComponentId==CoreGameplayDemoController.GuidedTutorialRewardComponentId&&c.ShipIndex>=0),Is.True);
            Snapshot(game,"tutorial_completed",seen);
            NavalBattleVisualCapture.Capture(game,"90_Actual_Tutorial_Completion.png","真实装备奖励后完成引导；没有设置完成标志","Shipyard Equipment Overlay");
            for(int attempts=0;GameObject.Find("Port Hub")==null&&attempts<3;attempts++){Click("Return To Port Hub");yield return null;}
            Assert.That(GameObject.Find("Port Hub"),Is.Not.Null);
            NavalBattleVisualCapture.Capture(game,"91_Free_Port.png","完成教程后真实港口与已赚取的账户余额","Port Hub");
            Snapshot(game,"free_port",seen);
            Click("Port Hub Settings");yield return null;
            Click("Return To Main Menu");yield return null;
            Click("Confirm Return To Main Menu");yield return null;yield return null;
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("MainMenu"));
            menu=Object.FindFirstObjectByType<MainMenuController>();
            Assert.That(menu.HasSaveData,Is.True);Assert.That(menu.ContinueButton.IsInteractable(),Is.True);
            NavalBattleVisualCapture.Capture(null,"92_Saved_Menu.png","正式保存并返回主菜单；继续游戏可用","Continue Game");
            Click("Continue Game");yield return null;yield return null;
            game=Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(game,Is.Not.Null);Assert.That(game.IsTutorialActive,Is.False);
            Snapshot(game,"continued_saved_profile",seen);
            Assert.That(records[records.Count-1].gold,Is.EqualTo(records[records.Count-2].gold),"In-session reload must not pay a second login income.");
            LogAssert.NoUnexpectedReceived();
        }

        private static void Click(string name)
        {
            Button button=Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b=>b.name==name&&b.gameObject.activeInHierarchy);
            Assert.That(button,Is.Not.Null,"Missing real control: "+name);Assert.That(button.IsInteractable(),Is.True,name);
            button.onClick.Invoke();
        }
        private static void Snapshot(CoreGameplayDemoController controller,string stage,HashSet<string> seen)
        {
            var session=controller.Session;var cargo=session.Cargo;
            records.Add(new Record{stage=stage,utc=DateTime.UtcNow.ToString("o"),company=Application.companyName,product=Application.productName,persistentDataPath=Application.persistentDataPath,
                gold=session.ProfileGold.ToString(CultureInfo.InvariantCulture),port=session.CurrentPort,phase=session.Phase.ToString(),ship=session.SelectedShip.Id,
                hull=session.Hull,maxHull=session.MaxHull,ordinaryCrew=session.SelectedOrdinaryCrew,specialCrew=session.SelectedSpecialCrewCount,crewCapacity=session.CrewCountValue,
                travelGold=cargo.Gold,water=cargo.Water,food=cargo.Food,powder=cargo.Powder,cannonballs=cargo.Cannonballs,wood=cargo.Wood,medicine=cargo.Medicine,fragments=session.TreasureFragments,
                tutorialActive=controller.IsTutorialActive,components=controller.OwnedComponents.Select(c=>c.InstanceId+"|"+c.ComponentId+"|ship="+c.ShipIndex).ToArray(),
                ownedShips=Enumerable.Range(0,CoreVoyageSession.Ships.Count).Where(i=>i==0||PlayerPrefs.GetInt(CoreGameplayDemoController.ShipOwnedKey(i),0)==1).Select(i=>CoreVoyageSession.Ships[i].Id).ToArray(),
                recruitedCrew=CrewNarrativeCatalog.AllCrew.Where(c=>PlayerPrefs.GetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(c.Id),0)==1).Select(c=>c.Id).ToArray(),
                deckCards=Enum.GetValues(typeof(NavalBattleStage)).Cast<NavalBattleStage>().SelectMany(s=>session.GetBattleDeck(s).Select(i=>s+"|base|"+session.GetBattleCard(i,s).Id+"|rank="+session.GetCardUpgradeLevel(session.GetBattleCard(i,s).Id)).Concat(session.GetBonusBattleCards(s).Select(i=>s+"|bonus|"+session.GetBattleCard(i,s).Id+"|rank="+session.GetCardUpgradeLevel(session.GetBattleCard(i,s).Id)))).ToArray(),
                cardUpgrades=session.CardUpgradeLevels.Select(p=>p.Key+"|rank="+p.Value).ToArray(),
                keepsakes=controller.WarehouseCollection.OwnedIds.ToArray(),displayed=Enumerable.Range(0,CoreWarehouseCollection.DisplaySlotCount).Select(i=>controller.WarehouseCollection.DisplayedAt(i)).ToArray(),
                seenSteps=seen==null?new string[0]:seen.OrderBy(x=>x).ToArray(),warehouseIncome=PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseIncomeKey,string.Empty)});
            string[] args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-auditActionLogPath");
            Assert.That(at,Is.GreaterThanOrEqualTo(0));string path=args[at+1];Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path,"campaign-ui-states.json"),"["+string.Join(",\n",records.Select(r=>JsonUtility.ToJson(r,true)))+"]");
        }
    }
}
