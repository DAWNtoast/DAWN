using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

public sealed class ExplorationJsonTests
{
    [Test]
    public void UnityJson_RetainsExplorationWindowsAndCluesAcrossCaptureAndRestore()
    {
        var session = new CoreVoyageSession();
        session.SetSeed("EXPLORATION-JSON-20261015");
        session.StartVoyage();
        CoreVoyageCheckpoint checkpoint = session.CaptureVoyageCheckpoint();
        checkpoint.sailingBoostHoursRemaining = 9.5d;
        checkpoint.surveyBoostHoursRemaining = 12.25d;
        checkpoint.explorationDiscoveries = new[] { "reef_dock_chart", "moon_star_chart" };
        var restored = new CoreVoyageSession();
        Assert.That(restored.TryRestoreVoyageCheckpoint(
            JsonUtility.FromJson<CoreVoyageCheckpoint>(JsonUtility.ToJson(checkpoint)), out string reason), Is.True, reason);
        var savedAgain = JsonUtility.FromJson<CoreVoyageCheckpoint>(JsonUtility.ToJson(restored.CaptureVoyageCheckpoint()));
        Assert.That(savedAgain.sailingBoostHoursRemaining, Is.EqualTo(9.5d));
        Assert.That(savedAgain.surveyBoostHoursRemaining, Is.EqualTo(12.25d));
        CollectionAssert.AreEquivalent(checkpoint.explorationDiscoveries, savedAgain.explorationDiscoveries);
        Assert.That(restored.HasDiscovery("reef_dock_chart"), Is.True);
        Assert.That(restored.HasDiscovery("moon_star_chart"), Is.True);
    }

    [Test]
    public void UnityJson_MissingExplorationFieldsDefaultsToEmptyState()
    {
        var session = new CoreVoyageSession();
        session.StartVoyage();
        string json = JsonUtility.ToJson(session.CaptureVoyageCheckpoint());
        json = System.Text.RegularExpressions.Regex.Replace(json,
            "\"(?:sailingBoostHoursRemaining|surveyBoostHoursRemaining)\":0(?:\\.0+)?,", "");
        json = json.Replace("\"explorationDiscoveries\":[],", "");
        Assert.That(json, Does.Not.Contain("sailingBoostHoursRemaining"));
        Assert.That(json, Does.Not.Contain("surveyBoostHoursRemaining"));
        Assert.That(json, Does.Not.Contain("explorationDiscoveries"));
        var restored = new CoreVoyageSession();
        Assert.That(restored.TryRestoreVoyageCheckpoint(JsonUtility.FromJson<CoreVoyageCheckpoint>(json), out string reason), Is.True, reason);
        Assert.That(restored.SailingBoostHoursRemaining, Is.Zero);
        Assert.That(restored.SurveyBoostHoursRemaining, Is.Zero);
        Assert.That(restored.HasDiscovery("reef_dock_chart"), Is.False);
    }
}
