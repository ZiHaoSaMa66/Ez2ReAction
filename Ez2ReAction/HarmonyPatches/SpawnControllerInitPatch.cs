using HarmonyLib;
using System.Reflection;

namespace Ez2ReAction.HarmonyPatches
{
    [HarmonyPatch(typeof(BeatmapObjectSpawnController))]
    [HarmonyPatch("Start")]
    internal class SpawnControllerInitPatch
    {
        private static FieldInfo _initDataField;
        private static FieldInfo _njsField;
        private static FieldInfo _offsetField;
        private static FieldInfo _offsetTypeField;

        internal static void Prefix(BeatmapObjectSpawnController __instance)
        {
            if (__instance == null) return;

            var config = PluginConfig.Instance;
            if (!config.Enabled && !config.OverrideOffset) return;

            if (_initDataField == null)
            {
                _initDataField = typeof(BeatmapObjectSpawnController)
                    .GetField("_initData", BindingFlags.NonPublic | BindingFlags.Instance);
                if (_initDataField == null)
                {
                    Plugin.Log?.Warn("SpawnControllerInitPatch: _initData field not found");
                    return;
                }
            }

            var initData = _initDataField.GetValue(__instance);
            if (initData == null) return;

            var initDataType = initData.GetType();

            if (config.Enabled && config.NjsOverride > 0f)
            {
                if (_njsField == null)
                {
                    _njsField = initDataType.GetField("noteJumpMovementSpeed",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (_njsField == null)
                    {
                        Plugin.Log?.Warn("SpawnControllerInitPatch: noteJumpMovementSpeed field not found");
                        return;
                    }
                }

                var originalNjs = (float)_njsField.GetValue(initData);
                config.LastMapNjs = originalNjs;

                if (PluginConfig.Instance.OriginalNjs <= 0f)
                {
                    PluginConfig.Instance.OriginalNjs = originalNjs;
                    NJSSettingsUI.Instance.UpdateOriginalNjs(originalNjs);
                }

                _njsField.SetValue(initData, config.NjsOverride);
            }

            if (_offsetField == null)
            {
                _offsetField = initDataType.GetField("noteJumpStartBeatOffset",
                    BindingFlags.Public | BindingFlags.Instance);
                if (_offsetField == null)
                {
                    _offsetField = initDataType.GetField("_noteJumpStartBeatOffset",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                }
                if (_offsetField == null)
                {
                    _offsetField = initDataType.GetField("noteJumpValue",
                        BindingFlags.Public | BindingFlags.Instance);
                }
                if (_offsetField == null)
                {
                    Plugin.Log?.Warn("SpawnControllerInitPatch: no offset field found");
                }
            }

            if (_offsetField != null)
            {
                var originalOffset = (float)_offsetField.GetValue(initData);
                config.LastMapOffset = originalOffset;

                if (PluginConfig.Instance.OriginalOffset <= 0f)
                {
                    PluginConfig.Instance.OriginalOffset = originalOffset;
                }

                if (config.OverrideOffset)
                {
                    _offsetField.SetValue(initData, config.OffsetOverride);

                    if (_offsetTypeField == null)
                    {
                        _offsetTypeField = initDataType.GetField("noteJumpValueType",
                            BindingFlags.Public | BindingFlags.Instance);
                    }

                    if (_offsetTypeField != null)
                    {
                        var fixedDuration = 1;
                        _offsetTypeField.SetValue(initData, fixedDuration);
                    }
                }
            }

            if (config.Enabled || config.OverrideOffset)
            {
                _initDataField.SetValue(__instance, initData);
            }

            // Notify the UI that gameplay capture is complete,
            // now that both NJS and offset values have been stored
            NJSSettingsUI.Instance.NotifyAfterGameplayCapture();

            Plugin.Log?.Debug($"SpawnControllerInitPatch applied — NJS: {config.Enabled}, Offset: {config.OverrideOffset}");
        }
    }
}
