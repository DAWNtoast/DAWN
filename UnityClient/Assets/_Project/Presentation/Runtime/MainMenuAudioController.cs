using UnityEngine;

namespace ShatteredSail.Presentation
{
    public sealed partial class MainMenuController
    {
        private const float MenuMusicFadeSeconds = 0.3f;
        private AudioSource menuMusicSource;
        private AudioSource outgoingMenuSource;
        private float outgoingMenuWeight;
        private AudioListener menuAudioListener;
        private float menuMusicFade;

        private void InitializeMenuAudio(Transform parent)
        {
            if (menuMusicSource == null)
            {
                var theme = Resources.Load<AudioClip>(MenuPortMusic.ResourcePath);
                outgoingMenuSource = MenuPortMusic.TakeOutgoing(parent);
                if (theme != null && outgoingMenuSource != null && outgoingMenuSource.clip == theme && outgoingMenuSource.isPlaying)
                {
                    menuMusicSource = outgoingMenuSource;
                    menuMusicSource.name = "Main Menu Music Source";
                    outgoingMenuSource = null;
                    EnsureMenuAudioListener();
                    return;
                }
                if (outgoingMenuSource != null) outgoingMenuSource.name = "Outgoing Menu Music Source";
                Transform existing = parent.Find("Main Menu Music Source");
                GameObject host = existing == null ? new GameObject("Main Menu Music Source") : existing.gameObject;
                if (existing == null) host.transform.SetParent(parent, false);
                menuMusicSource = host.GetComponent<AudioSource>();
                if (menuMusicSource == null) menuMusicSource = host.AddComponent<AudioSource>();
                menuMusicSource.playOnAwake = false;
                menuMusicSource.loop = true;
                menuMusicSource.spatialBlend = 0f;
                menuMusicSource.priority = 0;
                menuMusicSource.clip = theme;
            }
            EnsureMenuAudioListener();
            if (menuMusicSource.clip == null)
            {
                Debug.LogWarning("Main menu music was not found: " + MenuPortMusic.ResourcePath);
                return;
            }
            if (menuMusicSource.isPlaying) return;
            menuMusicFade = outgoingMenuSource != null ? MenuPortMusic.CrossfadeSeconds : MenuMusicFadeSeconds;
            if (outgoingMenuSource != null)
            {
                MenuPortMusic.RememberPlayback(outgoingMenuSource);
                outgoingMenuWeight = MenuMusicVolume > 0f ? Mathf.Clamp01(outgoingMenuSource.volume / MenuMusicVolume) : 1f;
            }
            menuMusicSource.volume = 0f;
            menuMusicSource.Play();
            MenuPortMusic.RestorePlayback(menuMusicSource);
        }

        private void UpdateMenuAudio()
        {
            if (menuMusicSource == null) return;
            EnsureMenuAudioListener();
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, 0.8f));
            menuMusicFade = Mathf.Max(0f, menuMusicFade - Time.unscaledDeltaTime);
            float duration = outgoingMenuSource != null ? MenuPortMusic.CrossfadeSeconds : MenuMusicFadeSeconds;
            float progress = Mathf.SmoothStep(0f, 1f, 1f - menuMusicFade / duration);
            menuMusicSource.volume = MenuMusicVolume * progress;
            if (outgoingMenuSource != null)
            {
                outgoingMenuSource.volume = MenuMusicVolume * outgoingMenuWeight * (1f - progress);
                if (menuMusicFade <= 0f) ReleaseOutgoingMenuSource();
            }
            MenuPortMusic.RememberPlayback(MenuPortMusic.LouderPlayingSource(menuMusicSource, outgoingMenuSource));
        }

        private static float MenuMusicVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, 0.7f)) * GameplayAudioMix.MusicGain;

        private void ReleaseOutgoingMenuSource()
        {
            if (outgoingMenuSource == null) return;
            outgoingMenuSource.Stop();
            Destroy(outgoingMenuSource.gameObject);
            outgoingMenuSource = null;
        }

        private void StopMenuAudio()
        {
            MenuPortMusic.RememberPlayback(MenuPortMusic.LouderPlayingSource(menuMusicSource, outgoingMenuSource));
            if (menuMusicSource != null) menuMusicSource.Stop();
            ReleaseOutgoingMenuSource();
        }

        private void CarryMusicToGame()
        {
            AudioSource outgoing = MenuPortMusic.LouderPlayingSource(menuMusicSource, outgoingMenuSource);
            if (outgoing == null) return;
            MenuPortMusic.CarryAcrossScene(outgoing);
            if (outgoing == menuMusicSource) menuMusicSource = null;
            else outgoingMenuSource = null;
        }

        private static void LoadGameplayWithMusic()
        {
            var menu = FindFirstObjectByType<MainMenuController>();
            UnityEngine.SceneManagement.SceneManager.LoadScene(MainSceneName);
            if (menu != null) menu.CarryMusicToGame();
        }

        private void EnsureMenuAudioListener()
        {
            if (menuAudioListener != null && menuAudioListener.isActiveAndEnabled) return;
            foreach (AudioListener listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (listener.isActiveAndEnabled) { menuAudioListener = listener; return; }
            Camera camera = Camera.main;
            if (camera == null) camera = Object.FindFirstObjectByType<Camera>();
            if (camera == null) return;
            menuAudioListener = camera.GetComponent<AudioListener>();
            if (menuAudioListener == null) menuAudioListener = camera.gameObject.AddComponent<AudioListener>();
            else menuAudioListener.enabled = true;
        }
    }
}
