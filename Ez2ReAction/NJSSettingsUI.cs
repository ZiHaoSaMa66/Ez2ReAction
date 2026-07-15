using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.GameplaySetup;
using BeatSaberMarkupLanguage.ViewControllers;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Ez2ReAction
{
    internal class NJSSettingsUI : INotifyPropertyChanged
    {
        public static NJSSettingsUI Instance { get; } = new NJSSettingsUI();

        public event PropertyChangedEventHandler PropertyChanged;
        private bool _registered;

        /// <summary>
        /// True after a successful GameplaySetup tab bind for the current menu container.
        /// Cleared on unregister / failed rebind so soft restarts can retry.
        /// </summary>
        public bool IsRegistered => _registered;

        [UIValue("enabled")]
        public bool Enabled
        {
            get => PluginConfig.Instance.Enabled;
            set
            {
                PluginConfig.Instance.Enabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Enabled)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReactionTimeLabel)));
            }
        }

        [UIValue("njs-value")]
        public float NjsValue
        {
            get => PluginConfig.Instance.NjsOverride;
            set
            {
                PluginConfig.Instance.NjsOverride = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NjsValue)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReactionTimeLabel)));
            }
        }

        [UIValue("original-njs-label")]
        public string OriginalNjsLabel => PluginConfig.Instance.OriginalNjsLabel;

        [UIValue("override-offset")]
        public bool OverrideOffset
        {
            get => PluginConfig.Instance.OverrideOffset;
            set
            {
                PluginConfig.Instance.OverrideOffset = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OverrideOffset)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReactionTimeLabel)));
            }
        }

        [UIValue("offset-value")]
        public float OffsetValue
        {
            get => PluginConfig.Instance.OffsetOverride;
            set
            {
                PluginConfig.Instance.OffsetOverride = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OffsetValue)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReactionTimeLabel)));
            }
        }

        [UIValue("map-njs-label")]
        public string MapNjsLabel
        {
            get
            {
                var njs = PluginConfig.Instance.OriginalNjs > 0f
                    ? PluginConfig.Instance.OriginalNjs
                    : PluginConfig.Instance.LastMapNjs;
                return $"Map NJS {njs:F1}";
            }
        }

        [UIValue("map-offset-label")]
        public string MapOffsetLabel
        {
            get
            {
                var config = PluginConfig.Instance;
                var hasOriginal = config.OriginalNjs > 0f;
                var offset = hasOriginal ? config.OriginalOffset : config.LastMapOffset;
                return $"Offset {offset:F3}";
            }
        }

        [UIValue("reaction-time-label")]
        public string ReactionTimeLabel
        {
            get
            {
                var config = PluginConfig.Instance;
                var njs = config.Enabled ? config.NjsOverride
                    : (config.OriginalNjs > 0f ? config.OriginalNjs : config.LastMapNjs);
                var offset = config.OverrideOffset ? config.OffsetOverride
                    : config.OriginalOffset;
                var bpm = config.OriginalBpm > 0f ? config.OriginalBpm : config.LastMapBpm;
                var ms = PluginConfig.CalculateReactionTime(njs, offset, bpm);
                return $"Reaction {ms}ms";
            }
        }

        public void UpdateOriginalNjs(float originalNjs)
        {
            var config = PluginConfig.Instance;
            config.OriginalNjs = originalNjs;
            config.LastMapNjs = originalNjs;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OriginalNjsLabel)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MapNjsLabel)));
        }

        public void NotifyAfterGameplayCapture()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MapNjsLabel)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MapOffsetLabel)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReactionTimeLabel)));
        }

        public void UpdateMapValues(float njs, float offset, float bpm)
        {
            var config = PluginConfig.Instance;
            config.OriginalNjs = njs;
            config.OriginalOffset = offset;
            config.OriginalBpm = bpm;
            config.LastMapNjs = njs;
            config.LastMapOffset = offset;
            config.LastMapBpm = bpm;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OriginalNjsLabel)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MapNjsLabel)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MapOffsetLabel)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReactionTimeLabel)));
        }

        public void Register()
        {
            // Graphics settings OK (and other soft restarts) rebuild BSML's Zenject
            // GameplaySetup with an empty menus list, while this static host survives.
            // Always re-bind the tab; AddTab alone is not enough if we early-return on
            // a stale _registered flag from the previous menu container.
            try
            {
                GameplaySetup.Instance.RemoveTab("Ez2ReAction");
                GameplaySetup.Instance.AddTab(
                    "Ez2ReAction",
                    "Ez2ReAction.Views.njs-settings.bsml",
                    this,
                    MenuType.Solo | MenuType.Custom | MenuType.Online
                );
                _registered = true;
            }
            catch (System.Exception ex)
            {
                _registered = false;
                Plugin.Log?.Warn($"Failed to register GameplaySetup tab: {ex.Message}");
            }
        }

        public void Unregister()
        {
            try
            {
                if (_registered)
                    GameplaySetup.Instance.RemoveTab("Ez2ReAction");
            }
            catch (System.Exception ex)
            {
                Plugin.Log?.Warn($"Failed to unregister GameplaySetup tab: {ex.Message}");
            }
            finally
            {
                _registered = false;
            }
        }
    }
}
