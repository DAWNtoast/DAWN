using System.Reflection;
using NUnit.Framework;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class ReleaseSmokeArgumentsTests
    {
        [TestCase("ordinary", false)]
        [TestCase("-shatteredSailSmokeTest", true)]
        [TestCase("-shatteredSailSmokeTest=output", false)]
        [TestCase("-ShatteredSailSmokeTest", false)]
        public void OnlyExactExplicitSwitchEnablesReadOnlyStartup_EvenWithoutValidOutput(string argument, bool requested)
        {
            FieldInfo field = typeof(ReleasePlayerSmokeCheck).GetField("commandLineArguments", BindingFlags.Static | BindingFlags.NonPublic);
            object previous = field.GetValue(null);
            try
            {
                field.SetValue(null, new[] { "game.exe", argument });
                var property = typeof(ReleasePlayerSmokeCheck).GetProperty("IsRequested", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(property.GetValue(null), Is.EqualTo(requested));
            }
            finally { field.SetValue(null, previous); }
        }
    }
}
