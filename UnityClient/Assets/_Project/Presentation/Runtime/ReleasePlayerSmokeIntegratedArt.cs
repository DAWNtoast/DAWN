using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public sealed partial class ReleasePlayerSmokeCheck
    {
        private void CheckPackagedComponentTuning()
        {
            var expected = new (string id, float speed, float vision)[]
            {
                ("sail_coast_runner", 0.9f, 4f),
                ("sail_tarred_square", 0.8f, 0f),
                ("sail_gullwing_cloth", 1.3f, 2f),
                ("sail_dawnstripe_canvas", 0.9f, 8f),
                ("sail_storm_canvas", 1.4f, 12f),
                ("sail_crosswind_lattice", 1.7f, 8f),
                ("sail_rainheron_wing", 1.3f, 16f),
                ("sail_saltfox_rig", 1.8f, 0f),
                ("sail_bluebell_canvas", 1.1f, 20f),
                ("sail_guild_silk", 2f, 18f),
                ("sail_emerald_trade_wing", 1.8f, 22f),
                ("sail_sunthread_spinnaker", 2.5f, 8f),
                ("sail_redroute_broadsail", 1.9f, 18f),
                ("sail_whisperveil", 1.7f, 26f),
                ("sail_moonweave", 2.7f, 26f),
                ("sail_eclipse_shroud", 2.4f, 30f),
                ("sail_starshear_rig", 3.2f, 18f),
                ("sail_ghostcurrent_canvas", 2.6f, 24f),
                ("sail_aurora_fan", 2.5f, 36f),
                ("sail_tempest_wing", 3.5f, 42f),
                ("sail_skybreaker_array", 3.7f, 34f),
                ("sail_crown_of_gales", 3.1f, 52f),
                ("sail_thunderphoenix", 4f, 30f),
                ("sail_last_free_wind", 3f, 60f),
                ("instrument_worldcompass", 1.6f, 105f),
                ("instrument_last_horizon_lens", 0.6f, 145f),
                ("sail_dawnkite", 1.1f, 6f),
                ("sail_strait_lugger", 1.6f, 10f),
                ("sail_evening_silk", 2.1f, 16f),
                ("sail_fogshroud_wing", 2.2f, 42f),
                ("sail_thunderreef_wing", 3.4f, 40f),
            };
            var catalog = new ComponentArtCatalog();
            Require(catalog.Entries.Count == 125 && catalog.ExpandedEntries.Count == 150,
                "Packaged component catalog must include 125 base and 25 expanded components.");
            var lines = new List<string> { "componentId,speed,vision" };
            foreach (var value in expected)
            {
                ComponentArtEntry entry = catalog.GetById(value.id);
                Require(entry != null && Mathf.Abs(entry.SpeedBonus - value.speed) < .0001f &&
                    Mathf.Abs(entry.VisionBonus - value.vision) < .0001f, "Packaged component tuning is stale: " + value.id);
                lines.Add(value.id + "," + entry.SpeedBonus.ToString("R", CultureInfo.InvariantCulture) +
                    "," + entry.VisionBonus.ToString("R", CultureInfo.InvariantCulture));
            }
            File.WriteAllLines(Path.Combine(destination, "packaged-component-tuning.csv"), lines);
            completedChecks.Add(expected.Length + " updated component speed/vision entries");
        }

        private void CheckPackagedIntegratedArt()
        {
            Texture2D atlas = BattleIconArtCatalog.Texture;
            Require(atlas != null && atlas.width == 2048 && atlas.height == 1536 &&
                BattleIconArtCatalog.AssetIds.Count == 41, "Packaged authored combat icon atlas missing.");
            var lines = new List<string> { "kind,id,resourcePath,width,height" };
            foreach (string id in BattleIconArtCatalog.AssetIds)
            {
                Require(BattleIconArtCatalog.TryGetUv(id, out Rect uv) && uv.width > 0f &&
                    uv.height > 0f && uv.xMin >= 0f && uv.xMax <= 1f && uv.yMin >= 0f && uv.yMax <= 1f,
                    "Packaged icon atlas mapping missing: " + id);
                lines.Add("icon," + id + "," + BattleIconArtCatalog.ResourcePath + "," + atlas.width + "," + atlas.height);
            }
            GameObject host = new GameObject("Packaged authored graphic bindings", typeof(RectTransform));
            try
            {
                var command = host.AddComponent<BattleCommandIconGraphic>();
                command.Configure(BattleCommandIconKind.ActionPoint);
                Require(command.mainTexture == atlas, "HUD command graphic is not using authored icons.");
                var statusHost = new GameObject("Status", typeof(RectTransform));
                statusHost.transform.SetParent(host.transform, false);
                var status = statusHost.AddComponent<BattleStatusIconGraphic>();
                status.Configure(CoreBattleStatusKind.Attack, "attack", false);
                Require(status.mainTexture == atlas, "Status graphic is not using authored icons.");
                var headerHost = new GameObject("Header", typeof(RectTransform));
                headerHost.transform.SetParent(host.transform, false);
                var header = headerHost.AddComponent<BattleHeaderIconGraphic>();
                header.Configure(true, Color.white);
                Require(header.mainTexture == atlas, "Header graphic is not using authored icons.");
            }
            finally { DestroyImmediate(host); }
            foreach (CoreNavalFieldKind kind in Enum.GetValues(typeof(CoreNavalFieldKind)))
            {
                Texture2D field = NavalFieldArtCatalog.ForField(kind);
                Require(field != null && field.width == 512 && field.height == 512,
                    "Packaged field painting missing or resized: " + kind);
                lines.Add("field," + kind + "," + NavalFieldArtCatalog.ResourcePath(kind) + ",512,512");
            }
            foreach (string path in new[] { "PortArt/ShipDossiers/ship_clipper_dossier_v1", "ComponentShopV3/shipyard_hall" })
            {
                Texture2D painting = Resources.Load<Texture2D>(path);
                Require(painting != null, "Packaged replacement painting missing: " + path);
                if (path.Contains("clipper")) Require(painting.width == 1024 && painting.height == 1536,
                    "Four-masted clipper dossier lost authored dimensions.");
                lines.Add("painting," + painting.name + "," + path + "," + painting.width + "," + painting.height);
            }
            File.WriteAllLines(Path.Combine(destination, "packaged-integrated-art.csv"), lines);
            completedChecks.Add("49 authored art assets: 41 icons, 6 fields, clipper dossier and shop background");
        }
    }
}
