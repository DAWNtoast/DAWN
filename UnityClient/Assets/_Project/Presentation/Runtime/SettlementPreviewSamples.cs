using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Presentation
{
    /// <summary>UI review data only. These rewards are not gameplay balance, earned cargo or save data.</summary>
    public static class SettlementPreviewSamples
    {
        public static VoyageSettlement Create(int scenario, string port = "灰鸥港")
        {
            var result = new VoyageSettlement
            {
                Outcome = VoyageOutcome.ArrivedAtPort, Title = "抵达新港", Destination = port,
                ProtectionRatio = 0.95f, DistanceNm = 1860, Hours = 94.5, Coverage = 83,
                CompletedEvents = 9, TotalEvents = 12, BankedGold = 1268, BankedWood = 32, BankedIron = 18, BankedFame = 47, ChartSaved = true
            };
            if (scenario == 1)
            {
                result.Outcome = VoyageOutcome.SafeReturn; result.Title = "安全返航"; result.ProtectionRatio = 0.85f;
                result.BankedGold = 374; result.BankedWood = 16; result.BankedIron = 8; result.BankedFame = 12;
                result.DistanceNm = 760; result.Hours = 38; result.Coverage = 46; result.CompletedEvents = 4;
            }
            if (scenario == 2)
            {
                result.Outcome = VoyageOutcome.Failure; result.Title = "航行失败"; result.ProtectionRatio = 0.35f;
                result.BankedGold = 67; result.BankedWood = 6; result.BankedIron = 2; result.BankedFame = 3; result.ChartSaved = false;
            }
            if (scenario == 3)
            {
                result.Outcome = VoyageOutcome.SafeReturn; result.Title = "安全返航"; result.ProtectionRatio = 0.85f;
                result.BankedGold = result.BankedWood = result.BankedIron = result.BankedFame = 0;
                result.DistanceNm = 0; result.Hours = 0; result.CompletedEvents = 0; result.Coverage = 7;
            }
            if (scenario == 4) { result.BankedGold = 0; result.BankedWood = 38; result.BankedIron = 0; result.BankedFame = 2; }
            if (scenario == 5) result.BankedGold = result.BankedWood = result.BankedIron = result.BankedFame = int.MaxValue;
            return result;
        }
    }
}
