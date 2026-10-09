using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Tutorial;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private const float MusicCrossfadeSeconds = 1.8f;
        private const float MusicFadeInSeconds = 0.3f;
        private const float AmbienceCrossfadeSeconds = 0.7f;
        private const string MusicResourcesPrefix = "Audio/Music/";
        private const string BattleSfxResourcesPrefix = "Audio/BattleSfx/";
        private const string WorldSfxResourcesPrefix = "Audio/WorldSfx/";
        private const string InterfaceSfxResourcesPrefix = "Audio/InterfaceSfx/";
        private const string AmbienceResourcesPrefix = "Audio/Ambience/";
        private static readonly IReadOnlyList<string> battleSoundEffectKeys = new[]
        {
            "battle_start", "battle_card_play", "battle_enemy_intent", "battle_maneuver",
            "battle_cannon_fire", "battle_impact", "battle_melee", "battle_melee_impact",
            "battle_ram_charge", "battle_ram_impact", "battle_guard", "battle_repair",
            "battle_victory", "battle_sunk",
        };
        private static readonly IReadOnlyList<string> worldSoundEffectKeys = new[]
        {
            "sail_depart", "sail_step", "harbor_arrive", "encounter_enter",
            "settlement_reward", "settlement_treasure", "settlement_failure", "dispatch_complete",
            "investment_build", "curse_purify", "milestone_unlock",
        };
        private static readonly IReadOnlyList<string> interfaceSoundEffectKeys = new[]
        {
            "course_plot", "encounter_choice", "trade_confirm", "shipyard_equip", "tavern_recruit",
            "dispatch_start", "dispatch_recall", "treasure_craft", "treasure_equip", "chart_draw", "shop_refresh",
        };
        private static readonly IReadOnlyList<string> ambienceSoundEffectKeys = new[]
        {
            "harbor_ambience", "sailing_ambience", "shipyard_ambience", "tavern_ambience",
            "market_ambience", "treasure_workbench_ambience",
        };
        private readonly Dictionary<string, AudioClip> musicClipCache = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioClip> battleSfxClipCache = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioClip> worldSfxClipCache = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioClip> interfaceSfxClipCache = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioClip> ambienceClipCache = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private AudioSource activeMusicSource;
        private AudioSource incomingMusicSource;
        private AudioSource battleSfxSource;
        private AudioSource worldSfxSource;
        private AudioSource interfaceSfxSource;
        private AudioSource ambienceSource;
        private AudioSource incomingAmbienceSource;
        private AudioListener cachedAudioListener;
        private string activeMusicKey = string.Empty;
        private string activeAmbienceKey = string.Empty;
        private float musicCrossfade;
        private float musicFadeIn;
        private float musicOutgoingWeight = 1f;
        private float ambienceCrossfade;
        private float ambienceOutgoingWeight;
        private float activeAmbienceWeight;
        private float incomingAmbienceWeight;
        private float effectMixGain = GameplayAudioMix.EffectsGain;
        private float effectMixTargetGain = GameplayAudioMix.EffectsGain;
        private float effectMixRampStart = GameplayAudioMix.EffectsGain;
        private float effectMixRampElapsed;
        private float effectMixRampDuration;
        private readonly List<EffectVoice> effectVoices = new List<EffectVoice>();
        private readonly Dictionary<string, double> lastEffectStart = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Queue<PendingEffect> pendingClosingEffects = new Queue<PendingEffect>();

        private sealed class EffectVoice
        {
            public AudioSource Source;
            public string Key;
            public float Gain;
            public double EndsAt;
        }

        private sealed class PendingEffect
        {
            public string Key;
            public AudioClip Clip;
            public AudioSource PreferredSource;
            public float Gain;
            public double RequestedAt;
        }
        private int lastBattleAudioPhase = int.MinValue;
        private int lastWorldAudioPhase = int.MinValue;

        public string ActiveMusicKey => activeMusicKey;
        public static bool SoundEffectsEnabled => true;
        public static IReadOnlyList<string> BattleSoundEffectKeys => battleSoundEffectKeys;
        public static IReadOnlyList<string> WorldSoundEffectKeys => worldSoundEffectKeys;
        public static IReadOnlyList<string> InterfaceSoundEffectKeys => interfaceSoundEffectKeys;
        public static IReadOnlyList<string> AmbienceSoundEffectKeys => ambienceSoundEffectKeys;

        public static string PortMusicKey(string portName)
        {
            switch (portName)
            {
                case "白贝港": return "port_white_shell";
                case "铜钟港": return "port_bronze_bell";
                case "盐冠港": return "port_salt_crown";
                case "暮潮港": return "port_twilight_tide";
                case "灰鸥港": return "port_gray_gull";
                default: return "port_broken_anchor";
            }
        }

        public static string RouteMusicKey(int routeIndex)
        {
            switch (Mathf.Clamp(routeIndex, 0, 2))
            {
                case 1: return "route_golden_tide_trade";
                case 2: return "route_moon_tide_ruins";
                default: return "route_shattered_sail_reef";
            }
        }

        public static string TreasureMusicKey(string rankId)
        {
            switch (rankId)
            {
                case "gilded": return "treasure_gilded";
                case "moonlit": return "treasure_moonlit";
                default: return "treasure_weathered";
            }
        }

        public static string RouteBossMusicKey(int routeIndex)
        {
            switch (Mathf.Clamp(routeIndex, 0, 2))
            {
                case 1: return "boss_route_golden_tide_trade";
                case 2: return "boss_route_moon_tide_ruins";
                default: return "boss_route_shattered_sail_reef";
            }
        }

        public static string LegendaryBattleMusicKey(string encounterId)
        {
            switch (encounterId)
            {
                case "legendary_pirate_thunder_crown": return "battle_legendary_thunder_crown";
                case "legendary_pirate_red_widow": return "battle_legendary_red_widow";
                case "legendary_pirate_ember_ark": return "battle_legendary_ember_ark";
                case "legendary_pirate_tide_requiem": return "battle_legendary_tide_requiem";
                case "legendary_pirate_iron_oath": return "battle_legendary_iron_oath";
                default: return "battle_nonboss";
            }
        }

        private void InitializeAudio()
        {
            Transform existingRoot = transform.Find("Shattered Sail Audio");
            GameObject audioRoot = existingRoot == null ? null : existingRoot.gameObject;
            if (audioRoot == null)
            {
                audioRoot = new GameObject("Shattered Sail Audio");
                audioRoot.transform.SetParent(transform, false);
            }

            InitializeAudioSources(audioRoot.transform);
        }

        private void InitializeAudioSources(Transform audioRoot)
        {
            activeMusicSource = EnsureMusicSource(audioRoot, "Active Music Source");
            incomingMusicSource = EnsureMusicSource(audioRoot, "Incoming Music Source");
            battleSfxSource = EnsureEffectsSource(audioRoot, "Battle Sound Effects Source");
            worldSfxSource = EnsureEffectsSource(audioRoot, "World Sound Effects Source");
            interfaceSfxSource = EnsureEffectsSource(audioRoot, "Interface Sound Effects Source");
            ambienceSource = EnsureMusicSource(audioRoot, "Ambient Sound Source");
            incomingAmbienceSource = EnsureMusicSource(audioRoot, "Incoming Ambient Sound Source");
            ambienceSource.priority = incomingAmbienceSource.priority = 128;
            ambienceSource.volume = 0f;
            incomingAmbienceSource.volume = 0f;
            activeMusicSource.volume = 0f;
            incomingMusicSource.volume = 0f;
            effectVoices.Clear();
            lastEffectStart.Clear();
            pendingClosingEffects.Clear();
            effectMixGain = effectMixTargetGain = effectMixRampStart = GameplayAudioMix.EffectsGain;
            effectMixRampElapsed = effectMixRampDuration = 0f;
            AddEffectVoice(battleSfxSource);
            AddEffectVoice(worldSfxSource);
            AddEffectVoice(interfaceSfxSource);
            for (int index = effectVoices.Count; index < GameplayAudioMix.EffectVoiceCount; index++)
                AddEffectVoice(EnsureEffectsSource(audioRoot, "Sound Effects Voice " + (index + 1)));
            EnsureAudioListener();
            ApplyMusicVolume();
            SetMusic(ResolveDesiredMusicKey(), true);
        }

        private void AddEffectVoice(AudioSource source)
        {
            source.Stop();
            source.clip = null;
            source.volume = 0f;
            effectVoices.Add(new EffectVoice { Source = source });
        }

        private void ResumeAudioPresentation()
        {
            // Deactivating the hierarchy stops AudioSources, while their clip
            // and our crossfade keys survive. Resume both sides of a fade so a
            // retained music key cannot leave the reactivated scene silent.
            ResumeLoopingSource(activeMusicSource);
            ResumeLoopingSource(incomingMusicSource);
            ResumeLoopingSource(ambienceSource);
            ResumeLoopingSource(incomingAmbienceSource);
        }

        private static void ResumeLoopingSource(AudioSource source)
        {
            if (source != null && source.isActiveAndEnabled && source.clip != null && !source.isPlaying) source.Play();
        }

        private static AudioSource EnsureMusicSource(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            GameObject host = existing == null ? new GameObject(name) : existing.gameObject;
            if (existing == null) host.transform.SetParent(parent, false);
            AudioSource source = host.GetComponent<AudioSource>();
            if (source == null) source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.priority = 0;
            return source;
        }

        private static AudioSource EnsureEffectsSource(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            GameObject host = existing == null ? new GameObject(name) : existing.gameObject;
            if (existing == null) host.transform.SetParent(parent, false);
            AudioSource source = host.GetComponent<AudioSource>();
            if (source == null) source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = 32;
            return source;
        }

        private void EnsureAudioListener()
        {
            // The normal frame path only checks one Unity object. Reacquire on
            // destruction or deactivation so changing cameras still recovers.
            if (cachedAudioListener != null && cachedAudioListener.isActiveAndEnabled) return;
            AudioListener[] listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            for (int index = 0; index < listeners.Length; index++)
            {
                if (!listeners[index].isActiveAndEnabled) continue;
                cachedAudioListener = listeners[index];
                return;
            }
            Camera camera = Camera.main;
            if (camera == null) camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (camera == null) return;
            cachedAudioListener = camera.GetComponent<AudioListener>();
            if (cachedAudioListener == null) cachedAudioListener = camera.gameObject.AddComponent<AudioListener>();
            else cachedAudioListener.enabled = true;
        }

        private void Update()
        {
            UpdatePortHeaderReadouts();
            if (!Application.isPlaying)
            {
                UpdateEditorPreviewIfNeeded();
                return;
            }

            RefreshMarketMonthIfNeeded();
            EnsureAudioListener();
            UpdateBattleEntryCue();
            UpdateWorldPhaseCue();
            string desiredKey = ResolveDesiredMusicKey();
            if (desiredKey != activeMusicKey && musicCrossfade <= 0f) SetMusic(desiredKey, false);
            UpdateMusicCrossfade();
            UpdateAmbience();
            UpdateEffectVoices();
        }

        private string ResolveDesiredMusicKey()
        {
            if (session == null) return string.Empty;
            if (session.Phase == CoreDemoPhase.Port) return PortMusicKey(session.CurrentPort);
            if (session.Phase == CoreDemoPhase.Battle) return ResolveBattleMusicKey();
            if (session.IsTreasureVoyage)
                return TreasureMusicKey(session.ActiveTreasureMap == null ? string.Empty : session.ActiveTreasureMap.RankId);
            return RouteMusicKey(session.SelectedRouteIndex);
        }

        private string ResolveBattleMusicKey()
        {
            string encounterId = session.ActiveEncounter == null ? string.Empty : session.ActiveEncounter.Id;
            if (CoreBattleBalance.Classify(encounterId) == CoreBossKind.Legendary)
                return LegendaryBattleMusicKey(encounterId);
            if (session.Battle != null && session.Battle.Profile != null)
            {
                if (session.Battle.Profile.BossKind == CoreBossKind.Route)
                    return RouteBossMusicKey(session.SelectedRouteIndex);
                if (session.Battle.Profile.BossKind == CoreBossKind.Treasure)
                    return TreasureMusicKey(session.ActiveTreasureMap == null ? "moonlit" : session.ActiveTreasureMap.RankId);
            }
            if (session.IsTreasureVoyage)
                return TreasureMusicKey(session.ActiveTreasureMap == null ? string.Empty : session.ActiveTreasureMap.RankId);
            return "battle_nonboss";
        }

        private void SetMusic(string key, bool immediate)
        {
            if (string.IsNullOrEmpty(key) || activeMusicSource == null || incomingMusicSource == null) return;
            AudioClip clip = LoadMusic(key);
            if (clip == null) return;
            if (activeMusicKey == key && activeMusicSource.clip == clip)
            {
                if (!activeMusicSource.isPlaying)
                {
                    activeMusicSource.volume = 0f;
                    musicFadeIn = MusicFadeInSeconds;
                    activeMusicSource.Play();
                }
                return;
            }
            if (immediate)
            {
                activeMusicSource.Stop();
                activeMusicSource.clip = clip;
                activeMusicSource.volume = 0f;
                activeMusicSource.Play();
                incomingMusicSource.Stop();
                incomingMusicSource.clip = null;
                incomingMusicSource.volume = 0f;
                activeMusicKey = key;
                musicCrossfade = 0f;
                musicFadeIn = MusicFadeInSeconds;
                musicOutgoingWeight = 1f;
                return;
            }

            incomingMusicSource.volume = 0f;
            incomingMusicSource.clip = clip;
            incomingMusicSource.time = 0f;
            incomingMusicSource.Play();
            musicCrossfade = MusicCrossfadeSeconds;
            musicOutgoingWeight = MusicVolume > 0f ? Mathf.Clamp01(activeMusicSource.volume / MusicVolume) : 1f;
            musicFadeIn = 0f;
            activeMusicKey = key;
        }

        private void UpdateMusicCrossfade()
        {
            if (activeMusicSource == null || incomingMusicSource == null) return;
            if (musicCrossfade <= 0f)
            {
                musicFadeIn = Mathf.Max(0f, musicFadeIn - Time.unscaledDeltaTime);
                ApplyMusicSourceVolumes();
                return;
            }
            musicCrossfade -= Time.unscaledDeltaTime;
            ApplyMusicSourceVolumes();
            if (musicCrossfade > 0f) return;

            activeMusicSource.Stop();
            AudioSource swap = activeMusicSource;
            activeMusicSource = incomingMusicSource;
            incomingMusicSource = swap;
            incomingMusicSource.volume = 0f;
            incomingMusicSource.clip = null;
            musicOutgoingWeight = 1f;
            activeMusicSource.volume = MusicVolume;
        }

        private AudioClip LoadMusic(string key)
        {
            if (musicClipCache.TryGetValue(key, out AudioClip cached)) return cached;
            AudioClip clip = Resources.Load<AudioClip>(MusicResourcesPrefix + key);
            musicClipCache[key] = clip;
            if (clip == null) Debug.LogWarning("Background music was not found: " + MusicResourcesPrefix + key);
            return clip;
        }

        private float MusicVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(MainMenuController.MusicVolumeKey, 0.7f)) *
            GameplayAudioMix.MusicGain;

        private float EffectsVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(MainMenuController.EffectsVolumeKey, 0.8f));

        private void ApplyMusicSourceVolumes()
        {
            float incomingWeight = musicCrossfade > 0f
                ? Mathf.SmoothStep(0f, 1f, 1f - musicCrossfade / MusicCrossfadeSeconds) : 0f;
            float startupWeight = Mathf.SmoothStep(0f, 1f, 1f - musicFadeIn / MusicFadeInSeconds);
            if (activeMusicSource != null) activeMusicSource.volume = MusicVolume * (1f - incomingWeight) * startupWeight * musicOutgoingWeight;
            if (incomingMusicSource != null) incomingMusicSource.volume = MusicVolume * incomingWeight;
        }

        private void ApplyMusicVolume()
        {
            ApplyMusicSourceVolumes();
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(MainMenuController.MasterVolumeKey, 0.8f));
            ApplyAmbienceSourceVolumes();
            ApplyEffectSourceVolumes();
        }

        private void RefreshMusicVolume() { ApplyMusicVolume(); }

        private string ResolveDesiredAmbienceKey()
        {
            if (session == null) return string.Empty;
            if (session.Phase == CoreDemoPhase.Battle) return string.Empty;
            if (session.Phase == CoreDemoPhase.Sailing || session.Phase == CoreDemoPhase.Encounter)
                return "sailing_ambience";
            if (session.Phase == CoreDemoPhase.Settlement) return "harbor_ambience";
            switch (activePortFacility)
            {
                case 1: return "shipyard_ambience";
                case 2: return "tavern_ambience";
                case 3:
                case 4:
                case 5:
                case 7:
                case 9: return "market_ambience";
                case 6: return "treasure_workbench_ambience";
                default: return "harbor_ambience";
            }
        }

        private void UpdateAmbience()
        {
            if (ambienceSource == null || incomingAmbienceSource == null) return;
            string desired = ResolveDesiredAmbienceKey();
            if (ambienceCrossfade <= 0f && (desired != activeAmbienceKey ||
                !string.IsNullOrEmpty(desired) && !ambienceSource.isPlaying))
                SetAmbience(desired);
            if (ambienceCrossfade <= 0f) return;

            ambienceCrossfade = Mathf.Max(0f, ambienceCrossfade - Time.unscaledDeltaTime);
            float progress = Mathf.SmoothStep(0f, 1f, 1f - ambienceCrossfade / AmbienceCrossfadeSeconds);
            activeAmbienceWeight = ambienceOutgoingWeight * (1f - progress);
            incomingAmbienceWeight = incomingAmbienceSource.clip == null ? 0f : progress;
            ApplyAmbienceSourceVolumes();
            if (ambienceCrossfade > 0f) return;

            ambienceSource.Stop();
            ambienceSource.clip = null;
            AudioSource swap = ambienceSource;
            ambienceSource = incomingAmbienceSource;
            incomingAmbienceSource = swap;
            activeAmbienceWeight = incomingAmbienceWeight;
            incomingAmbienceWeight = 0f;
            ApplyAmbienceSourceVolumes();
        }

        private void SetAmbience(string desired)
        {
            if (ambienceSource == null || incomingAmbienceSource == null || ambienceCrossfade > 0f) return;
            AudioClip clip = string.IsNullOrEmpty(desired) ? null : LoadAmbience(desired);
            if (!string.IsNullOrEmpty(desired) && clip == null) return;
            ambienceOutgoingWeight = activeAmbienceWeight;
            incomingAmbienceSource.Stop();
            incomingAmbienceSource.clip = clip;
            incomingAmbienceSource.volume = 0f;
            incomingAmbienceWeight = 0f;
            if (clip != null)
            {
                incomingAmbienceSource.time = 0f;
                incomingAmbienceSource.Play();
            }
            activeAmbienceKey = desired;
            ambienceCrossfade = AmbienceCrossfadeSeconds;
        }

        private void ApplyAmbienceSourceVolumes()
        {
            if (ambienceSource != null) ambienceSource.volume = AmbienceVolume * activeAmbienceWeight;
            if (incomingAmbienceSource != null) incomingAmbienceSource.volume = AmbienceVolume * incomingAmbienceWeight;
        }

        private AudioClip LoadAmbience(string key)
        {
            if (ambienceClipCache.TryGetValue(key, out AudioClip cached)) return cached;
            AudioClip clip = Resources.Load<AudioClip>(AmbienceResourcesPrefix + key);
            ambienceClipCache[key] = clip;
            if (clip == null) Debug.LogWarning("Ambient sound was not found: " + AmbienceResourcesPrefix + key);
            return clip;
        }

        private float AmbienceVolume => EffectsVolume * GameplayAudioMix.AmbienceGain;

        private void UpdateBattleEntryCue()
        {
            int phase = int.MinValue;
            bool tutorialBattle = tutorialActive && tutorial != null && tutorial.Phase == TutorialVoyagePhase.Battle;
            if (tutorialActive && tutorial != null)
                phase = 1000 + (int)tutorial.Phase;
            else if (session != null)
                phase = (int)session.Phase;
            if (phase == lastBattleAudioPhase) return;
            lastBattleAudioPhase = phase;
            if (tutorialBattle || session != null && session.Phase == CoreDemoPhase.Battle)
                PlaySfx("battle_start", 0.86f);
        }

        private void UpdateWorldPhaseCue()
        {
            if (session == null) return;
            int phase = (int)session.Phase;
            if (phase == lastWorldAudioPhase) return;
            int previous = lastWorldAudioPhase;
            lastWorldAudioPhase = phase;
            if (session.Phase == CoreDemoPhase.Encounter && previous != int.MinValue)
                PlaySfx("encounter_enter", 0.58f);
            // Navigation completion and the outcome reveal own settlement cues.
            // A settlement may be a defeat at sea rather than an arrival at a harbor.
        }

        private bool IsBattleAudioActive()
        {
            return session != null && session.Phase == CoreDemoPhase.Battle ||
                tutorialActive && tutorial != null && tutorial.Phase == TutorialVoyagePhase.Battle ||
                battleAnimationPlaying;
        }

        private AudioClip LoadBattleSfx(string key)
        {
            if (battleSfxClipCache.TryGetValue(key, out AudioClip cached)) return cached;
            AudioClip clip = Resources.Load<AudioClip>(BattleSfxResourcesPrefix + key);
            battleSfxClipCache[key] = clip;
            if (clip == null) Debug.LogWarning("Battle sound effect was not found: " + BattleSfxResourcesPrefix + key);
            return clip;
        }

        private AudioClip LoadWorldSfx(string key)
        {
            if (worldSfxClipCache.TryGetValue(key, out AudioClip cached)) return cached;
            AudioClip clip = Resources.Load<AudioClip>(WorldSfxResourcesPrefix + key);
            worldSfxClipCache[key] = clip;
            if (clip == null) Debug.LogWarning("World sound effect was not found: " + WorldSfxResourcesPrefix + key);
            return clip;
        }

        private AudioClip LoadInterfaceSfx(string key)
        {
            if (interfaceSfxClipCache.TryGetValue(key, out AudioClip cached)) return cached;
            AudioClip clip = Resources.Load<AudioClip>(InterfaceSfxResourcesPrefix + key);
            interfaceSfxClipCache[key] = clip;
            if (clip == null) Debug.LogWarning("Interface sound effect was not found: " + InterfaceSfxResourcesPrefix + key);
            return clip;
        }

        private static bool IsWorldSoundEffectKey(string key)
        {
            for (int index = 0; index < worldSoundEffectKeys.Count; index++)
                if (string.Equals(worldSoundEffectKeys[index], key, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool IsInterfaceSoundEffectKey(string key)
        {
            for (int index = 0; index < interfaceSoundEffectKeys.Count; index++)
                if (string.Equals(interfaceSoundEffectKeys[index], key, StringComparison.Ordinal)) return true;
            return false;
        }

        private bool IsWorldAudioActive()
        {
            bool tutorialBattle = tutorialActive && tutorial != null && tutorial.Phase == TutorialVoyagePhase.Battle;
            return !battleAnimationPlaying && !tutorialBattle && (session == null || session.Phase != CoreDemoPhase.Battle);
        }

        private void PlaySfx(string key, float volume = 1f)
        {
            if (!SoundEffectsEnabled || string.IsNullOrEmpty(key)) return;
            AudioClip clip;
            AudioSource preferredSource;
            if (key.StartsWith("battle_", StringComparison.Ordinal))
            {
                if (battleSfxSource == null || !IsBattleAudioActive()) return;
                clip = LoadBattleSfx(key);
                preferredSource = battleSfxSource;
            }
            else if (IsWorldSoundEffectKey(key))
            {
                if (worldSfxSource == null || !IsWorldAudioActive()) return;
                clip = LoadWorldSfx(key);
                preferredSource = worldSfxSource;
            }
            else
            {
                if (!IsInterfaceSoundEffectKey(key) || interfaceSfxSource == null || !IsWorldAudioActive()) return;
                clip = LoadInterfaceSfx(key);
                preferredSource = interfaceSfxSource;
            }
            if (clip == null || volume <= 0f) return;
            double now = AudioSettings.dspTime;
            if (lastEffectStart.TryGetValue(key, out double previous) && now - previous < GameplayAudioMix.RetriggerSeconds(key)) return;
            int sameCueVoices = 0;
            foreach (EffectVoice voice in effectVoices)
                if (voice.Key == key && IsEffectVoiceBusy(voice, now)) sameCueVoices++;
            if (sameCueVoices >= GameplayAudioMix.SimultaneousCueLimit(key)) return;
            foreach (PendingEffect pending in pendingClosingEffects)
                if (pending.Key == key) return;

            bool closingCue = GameplayAudioMix.IsClosingCue(key);
            EffectVoice available = FindEffectVoice(preferredSource, closingCue, now);
            if (available == null)
            {
                // Never steal a playing tail. Two slots are reserved for outcomes;
                // an exceptional outcome burst waits briefly for an existing tail.
                if (closingCue && pendingClosingEffects.Count < 2)
                {
                    pendingClosingEffects.Enqueue(new PendingEffect
                    {
                        Key = key, Clip = clip, PreferredSource = preferredSource,
                        Gain = Mathf.Clamp01(volume), RequestedAt = now,
                    });
                }
                return;
            }
            StartEffectVoice(available, key, clip, volume, now);
        }

        private static bool IsEffectVoiceBusy(EffectVoice voice, double now) =>
            voice.Key != null && (voice.Source.isPlaying || now < voice.EndsAt);

        private EffectVoice FindEffectVoice(AudioSource preferredSource, bool closingCue, double now)
        {
            if (closingCue)
            {
                for (int index = GameplayAudioMix.RegularEffectVoiceCount; index < effectVoices.Count; index++)
                    if (!IsEffectVoiceBusy(effectVoices[index], now)) return effectVoices[index];
            }
            for (int index = 0; index < Mathf.Min(GameplayAudioMix.RegularEffectVoiceCount, effectVoices.Count); index++)
                if (effectVoices[index].Source == preferredSource && !IsEffectVoiceBusy(effectVoices[index], now)) return effectVoices[index];
            for (int index = 0; index < Mathf.Min(GameplayAudioMix.RegularEffectVoiceCount, effectVoices.Count); index++)
                if (!IsEffectVoiceBusy(effectVoices[index], now)) return effectVoices[index];
            return null;
        }

        private void StartEffectVoice(EffectVoice voice, string key, AudioClip clip, float gain, double now)
        {
            voice.Key = key;
            voice.Gain = Mathf.Clamp01(gain);
            voice.EndsAt = now + clip.length;
            voice.Source.clip = clip;
            voice.Source.priority = GameplayAudioMix.IsClosingCue(key) ? 16 : 32;
            lastEffectStart[key] = now;
            // Apply the shared gain before Play, including this new voice.
            ApplyEffectSourceVolumes();
            voice.Source.Play();
        }

        private void UpdateEffectVoices()
        {
            double now = AudioSettings.dspTime;
            foreach (EffectVoice voice in effectVoices)
            {
                if (voice.Key == null || IsEffectVoiceBusy(voice, now)) continue;
                voice.Key = null;
                voice.Source.clip = null;
                voice.Source.volume = 0f;
            }
            while (pendingClosingEffects.Count > 0)
            {
                PendingEffect pending = pendingClosingEffects.Peek();
                if (now - pending.RequestedAt > 2d)
                {
                    pendingClosingEffects.Dequeue();
                    continue;
                }
                EffectVoice available = FindEffectVoice(pending.PreferredSource, true, now);
                if (available == null) break;
                pendingClosingEffects.Dequeue();
                StartEffectVoice(available, pending.Key, pending.Clip, pending.Gain, now);
            }
            AdvanceEffectMixEnvelope(Time.unscaledDeltaTime);
            ApplyEffectSourceVolumes();
        }

        private void AdvanceEffectMixEnvelope(float deltaTime)
        {
            double now = AudioSettings.dspTime;
            int activeCount = 0;
            foreach (EffectVoice voice in effectVoices)
                if (IsEffectVoiceBusy(voice, now)) activeCount++;
            // Recover during quiet gaps too, so the next single cue does not inherit
            // stale attenuation. Only Update advances this shared envelope; a burst
            // of Play calls must not advance it multiple times within one frame.
            float target = activeCount == 0 ? GameplayAudioMix.EffectsGain : GameplayAudioMix.EffectMixScale(activeCount);
            if (!Mathf.Approximately(target, effectMixTargetGain))
            {
                effectMixRampStart = effectMixGain;
                effectMixTargetGain = target;
                effectMixRampElapsed = 0f;
                effectMixRampDuration = target < effectMixGain
                    ? GameplayAudioMix.EffectsAttenuationSeconds : GameplayAudioMix.EffectsRecoverySeconds;
            }
            if (effectMixRampDuration <= 0f) return;
            effectMixRampElapsed = Mathf.Min(effectMixRampDuration, effectMixRampElapsed + Mathf.Max(0f, deltaTime));
            effectMixGain = Mathf.Lerp(effectMixRampStart, effectMixTargetGain,
                Mathf.SmoothStep(0f, 1f, effectMixRampElapsed / effectMixRampDuration));
        }

        private void ApplyEffectSourceVolumes()
        {
            double now = AudioSettings.dspTime;
            float scale = EffectsVolume * effectMixGain;
            foreach (EffectVoice voice in effectVoices)
                voice.Source.volume = IsEffectVoiceBusy(voice, now) ? voice.Gain * scale : 0f;
        }
    }
}

