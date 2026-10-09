using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreVoyageDestinationTests
    {
        [Test]
        public void DestinationPicker_ExcludesCurrentPortAndChangesMapIdentity()
        {
            var session = new CoreVoyageSession();
            Assert.That(CoreVoyageSession.DestinationPorts, Does.Contain("白贝港"));
            Assert.That(session.SelectedDestinationPort, Is.Not.EqualTo(session.CurrentPort));
            Assert.That(session.SelectDestinationPort(session.CurrentPort), Is.False);
            Assert.That(session.SelectDestinationPort("不存在的港口"), Is.False);
            Assert.That(session.SelectDestinationPort("铜钟港"), Is.True);

            session.SetSeed("DESTINATION-IDENTITY");
            session.StartVoyage();
            Assert.That(session.Map.DestinationName, Is.EqualTo("铜钟港"));

            var alternate = new CoreVoyageSession();
            alternate.SetSeed("DESTINATION-IDENTITY");
            alternate.SelectDestinationPort("盐冠港");
            alternate.StartVoyage();
            Assert.That(alternate.Map.DestinationName, Is.EqualTo("盐冠港"));
            Assert.That(alternate.Map.Hash, Is.Not.EqualTo(session.Map.Hash));
        }

        [Test]
        public void Arrival_SelectsAValidNextDestinationForTheNewPort()
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(500, "暮潮港");
            Assert.That(session.SelectedDestinationPort, Is.Not.EqualTo("暮潮港"));
            Assert.That(CoreVoyageSession.DestinationPorts, Does.Contain(session.SelectedDestinationPort));
        }
    }
}
