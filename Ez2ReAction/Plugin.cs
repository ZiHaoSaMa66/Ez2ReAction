using HarmonyLib;
using IPA;
using IPA.Config;
using IPA.Config.Stores;
using Ez2ReAction.HarmonyPatches;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ez2ReAction
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    internal class Plugin
    {
        internal static IPA.Logging.Logger Log { get; private set; }
        internal static Harmony Harmony { get; private set; }

        [Init]
        public void Init(IPA.Logging.Logger logger, Config config)
        {
            Log = logger;
            PluginConfig.Instance = config.Generated<PluginConfig>();
        }

        [OnStart]
        public void OnStart()
        {
            Harmony = new Harmony("com.yourname.Ez2ReAction");
            Harmony.PatchAll(Assembly.GetExecutingAssembly());

            SceneManager.activeSceneChanged += OnActiveSceneChanged;

            Log.Info("Ez2ReAction initialized");
        }

        [OnExit]
        public void OnExit()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            NJSSettingsUI.Instance.Unregister();
            UnsubscribeFromAllEvents();
            Harmony?.UnpatchSelf();
            Log.Info("Ez2ReAction stopped");
        }

        private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            if (newScene.name != "MainMenu")
                return;

            // Soft restart (e.g. graphics settings OK) re-enters MainMenu and rebuilds
            // menu view controllers / BSML GameplaySetup. Reuse one DDOL registrar and
            // always re-run setup so the tab and event subscriptions rebind.
            if (RegistrarBehaviour.Instance != null)
            {
                RegistrarBehaviour.Instance.RequestSetup();
                return;
            }

            var go = new GameObject("Ez2ReActionRegistrar");
            go.AddComponent<RegistrarBehaviour>();
            GameObject.DontDestroyOnLoad(go);
        }

        // ── Type helpers ──────────────────────────────────────────

        private static System.Type FindType(string typeName)
        {
            string[] candidates = new string[] { typeName, "HMUI." + typeName };
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var name in candidates)
                {
                    var t = asm.GetType(name, false);
                    if (t != null) return t;
                }
            }
            return null;
        }

        private static object FindFirstInstance(System.Type type)
        {
            if (type == null) return null;

            var getInstance = type.GetMethod("get_instance",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (getInstance != null)
            {
                var instance = getInstance.Invoke(null, null);
                if (instance != null) return instance;
            }

            var resources = Resources.FindObjectsOfTypeAll(type);
            if (resources != null && resources.Length > 0)
                return resources[0];

            return null;
        }

        // ── StandardLevelDetailViewController events ──────────────

        private static System.Type _levelDetailType;
        private static object _levelDetailInstance;
        private static System.Collections.Generic.List<System.Reflection.EventInfo> _subscribedEvents
            = new System.Collections.Generic.List<System.Reflection.EventInfo>();
        private static System.Collections.Generic.List<System.Delegate> _eventHandlers
            = new System.Collections.Generic.List<System.Delegate>();

        private static void SubscribeToMenuEvents()
        {
            if (_levelDetailType == null)
            {
                _levelDetailType = FindType("StandardLevelDetailViewController");
                if (_levelDetailType == null)
                {
                    Plugin.Log?.Warn("StandardLevelDetailViewController not found");
                    return;
                }
            }

            var instance = FindFirstInstance(_levelDetailType);
            if (instance == null)
            {
                Plugin.Log?.Warn("StandardLevelDetailViewController instance not found");
                return;
            }

            // Unsubscribe from any previous instance first, then bind the live one.
            UnsubscribeFromMenuEvents();
            _levelDetailInstance = instance;

            string[] eventNames = new string[]
            {
                "didChangeDifficultyBeatmapEvent",
                "didSelectLevelEvent",
                "selectBeatmapEvent"
            };

            var targetMethod = typeof(Plugin).GetMethod("OnMenuDifficultyChanged",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (targetMethod == null) return;

            foreach (var eventName in eventNames)
            {
                try
                {
                    var evt = _levelDetailType.GetEvent(eventName,
                        BindingFlags.Public | BindingFlags.Instance);
                    if (evt == null) continue;

                    var handlerType = evt.EventHandlerType;
                    var invokeMethod = handlerType.GetMethod("Invoke");
                    if (invokeMethod == null) continue;

                    var paramTypes = invokeMethod.GetParameters()
                        .Select(p => p.ParameterType).ToArray();

                    System.Delegate handler;
                    if (paramTypes.Length == 0)
                    {
                        handler = System.Delegate.CreateDelegate(handlerType, targetMethod);
                    }
                    else
                    {
                        var paramExprs = paramTypes
                            .Select((t, i) => Expression.Parameter(t, $"p{i}")).ToArray();
                        var body = Expression.Call(targetMethod);
                        var lambda = Expression.Lambda(handlerType, body, paramExprs);
                        handler = lambda.Compile();
                    }

                    evt.GetAddMethod().Invoke(instance, new object[] { handler });
                    _subscribedEvents.Add(evt);
                    _eventHandlers.Add(handler);
                    Plugin.Log?.Info($"Subscribed to StandardLevelDetailViewController.{eventName}");
                }
                catch (System.Exception ex)
                {
                    Plugin.Log?.Warn($"Failed to subscribe to {eventName}: {ex.Message}");
                }
            }
        }

        private static void UnsubscribeFromMenuEvents()
        {
            try
            {
                for (int i = 0; i < _subscribedEvents.Count; i++)
                {
                    if (_levelDetailInstance != null)
                    {
                        _subscribedEvents[i].GetRemoveMethod().Invoke(
                            _levelDetailInstance, new object[] { _eventHandlers[i] });
                    }
                }
            }
            catch { }
            _subscribedEvents.Clear();
            _eventHandlers.Clear();
            _levelDetailInstance = null;
        }

        // ── LevelCollectionViewController events ──────────────────

        private static System.Type _levelCollectionType;
        private static object _levelCollectionInstance;
        private static System.Collections.Generic.List<System.Reflection.EventInfo> _subscribedLevelEvents
            = new System.Collections.Generic.List<System.Reflection.EventInfo>();
        private static System.Collections.Generic.List<System.Delegate> _levelEventHandlers
            = new System.Collections.Generic.List<System.Delegate>();

        private static void OnLevelSelected()
        {
            // didSelectLevelEvent fires before StandardLevelDetailViewController
            // has the new beatmap data ready. Defer read by one frame.
            if (RegistrarBehaviour.Instance != null)
                RegistrarBehaviour.Instance.StartCoroutine(DelayedReadCoroutine());
        }

        private static IEnumerator DelayedReadCoroutine()
        {
            yield return null;
            OnMenuDifficultyChanged();
        }

        private static void SubscribeToLevelCollectionEvents()
        {
            if (_levelCollectionType == null)
            {
                _levelCollectionType = FindType("LevelCollectionViewController");
                if (_levelCollectionType == null)
                {
                    Plugin.Log?.Warn("LevelCollectionViewController not found");
                    return;
                }
            }

            var instance = FindFirstInstance(_levelCollectionType);
            if (instance == null) return;

            // Unsubscribe from any previous instance first, then bind the live one.
            UnsubscribeFromLevelCollectionEvents();
            _levelCollectionInstance = instance;

            var didSelectEvent = _levelCollectionType.GetEvent("didSelectLevelEvent",
                BindingFlags.Public | BindingFlags.Instance);
            if (didSelectEvent == null) return;

            var targetMethod = typeof(Plugin).GetMethod("OnLevelSelected",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (targetMethod == null) return;

            var handlerType = didSelectEvent.EventHandlerType;
            var invokeMethod = handlerType.GetMethod("Invoke");
            if (invokeMethod == null) return;

            var paramTypes = invokeMethod.GetParameters()
                .Select(p => p.ParameterType).ToArray();

            System.Delegate handler;
            if (paramTypes.Length == 0)
            {
                handler = System.Delegate.CreateDelegate(handlerType, targetMethod);
            }
            else
            {
                var paramExprs = paramTypes
                    .Select((t, i) => Expression.Parameter(t, $"p{i}")).ToArray();
                var body = Expression.Call(targetMethod);
                var lambda = Expression.Lambda(handlerType, body, paramExprs);
                handler = lambda.Compile();
            }

            didSelectEvent.GetAddMethod().Invoke(instance, new object[] { handler });
            _subscribedLevelEvents.Add(didSelectEvent);
            _levelEventHandlers.Add(handler);
            Plugin.Log?.Info("Subscribed to LevelCollectionViewController.didSelectLevelEvent");
        }

        private static void UnsubscribeFromLevelCollectionEvents()
        {
            try
            {
                for (int i = 0; i < _subscribedLevelEvents.Count; i++)
                {
                    if (_levelCollectionInstance != null)
                    {
                        _subscribedLevelEvents[i].GetRemoveMethod().Invoke(
                            _levelCollectionInstance, new object[] { _levelEventHandlers[i] });
                    }
                }
            }
            catch { }
            _subscribedLevelEvents.Clear();
            _levelEventHandlers.Clear();
            _levelCollectionInstance = null;
        }

        private static void UnsubscribeFromAllEvents()
        {
            UnsubscribeFromMenuEvents();
            UnsubscribeFromLevelCollectionEvents();
        }

        // ── Beatmap data reader ───────────────────────────────────

        internal static void OnMenuDifficultyChanged()
        {
            try
            {
                if (_levelDetailType == null) return;

                if (_levelDetailInstance == null)
                {
                    _levelDetailInstance = FindFirstInstance(_levelDetailType);
                }
                if (_levelDetailInstance == null) return;

                object beatmap = TryGetSelectedBeatmapFromBeatmapKey();

                if (beatmap == null)
                {
                    var selectedDiffBeatmapProp = _levelDetailType.GetProperty("selectedDifficultyBeatmap",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (selectedDiffBeatmapProp == null)
                    {
                        selectedDiffBeatmapProp = _levelDetailType.GetProperty("selectedDifficultyBeatmap",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                    }

                    if (selectedDiffBeatmapProp != null)
                    {
                        beatmap = selectedDiffBeatmapProp.GetValue(_levelDetailInstance, null);
                    }
                    else
                    {
                        var selectedDiffBeatmapField = _levelDetailType.GetField("_selectedDifficultyBeatmap",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        if (selectedDiffBeatmapField == null)
                        {
                            selectedDiffBeatmapField = _levelDetailType.GetField("selectedDifficultyBeatmap",
                                BindingFlags.Public | BindingFlags.Instance);
                        }
                        if (selectedDiffBeatmapField != null)
                        {
                            beatmap = selectedDiffBeatmapField.GetValue(_levelDetailInstance);
                        }
                    }
                }

                if (beatmap == null)
                {
                    Plugin.Log?.Debug("No selected difficulty beatmap");
                    return;
                }

                var beatmapType = beatmap.GetType();

                var bpm = TryGetBpmFromBeatmap(beatmap) ?? TryGetSelectedLevelBpm() ?? 120f;

                var njsProp = beatmapType.GetProperty("noteJumpMovementSpeed",
                    BindingFlags.Public | BindingFlags.Instance);
                var offsetProp = beatmapType.GetProperty("noteJumpStartBeatOffset",
                    BindingFlags.Public | BindingFlags.Instance);

                if (njsProp == null)
                {
                    var njsField = beatmapType.GetField("noteJumpMovementSpeed",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (njsField != null)
                    {
                        var njs = (float)njsField.GetValue(beatmap);
                        var offset = 0f;
                        var offsetField = beatmapType.GetField("noteJumpStartBeatOffset",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (offsetField != null)
                        {
                            offset = (float)offsetField.GetValue(beatmap);
                        }
                        NJSSettingsUI.Instance.UpdateMapValues(njs, offset, bpm);
                        Plugin.Log?.Debug($"Pre-filled from menu: NJS={njs}, Offset={offset}, BPM={bpm}");
                        return;
                    }
                }

                if (njsProp != null && offsetProp != null)
                {
                    var njs = (float)njsProp.GetValue(beatmap, null);
                    var offset = (float)offsetProp.GetValue(beatmap, null);
                    NJSSettingsUI.Instance.UpdateMapValues(njs, offset, bpm);
                    Plugin.Log?.Debug($"Pre-filled from menu: NJS={njs}, Offset={offset}, BPM={bpm}");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log?.Warn($"Error reading selected beatmap data: {ex.Message}");
            }
        }

        private static object TryGetSelectedBeatmapFromBeatmapKey()
        {
            var beatmapKeyProp = _levelDetailType.GetProperty("beatmapKey",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var beatmapLevelProp = _levelDetailType.GetProperty("beatmapLevel",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (beatmapKeyProp == null || beatmapLevelProp == null) return null;

            var beatmapKey = beatmapKeyProp.GetValue(_levelDetailInstance, null);
            var beatmapLevel = beatmapLevelProp.GetValue(_levelDetailInstance, null);
            if (beatmapKey == null || beatmapLevel == null) return null;

            var beatmapKeyType = beatmapKey.GetType();
            var characteristicField = beatmapKeyType.GetField("beatmapCharacteristic",
                BindingFlags.Public | BindingFlags.Instance);
            var difficultyField = beatmapKeyType.GetField("difficulty",
                BindingFlags.Public | BindingFlags.Instance);
            if (characteristicField == null || difficultyField == null) return null;

            var characteristic = characteristicField.GetValue(beatmapKey);
            var difficulty = difficultyField.GetValue(beatmapKey);
            if (characteristic == null || difficulty == null) return null;

            var getDifficultyBeatmapDataMethod = beatmapLevel.GetType().GetMethod("GetDifficultyBeatmapData",
                BindingFlags.Public | BindingFlags.Instance);
            if (getDifficultyBeatmapDataMethod == null) return null;

            return getDifficultyBeatmapDataMethod.Invoke(beatmapLevel, new[] { characteristic, difficulty });
        }

        private static float? TryGetBpmFromBeatmap(object beatmap)
        {
            if (beatmap == null) return null;

            var bpm = TryReadFloatMember(beatmap, "beatsPerMinute");
            if (bpm.HasValue) return bpm;

            foreach (var levelMemberName in new[] { "level", "beatmapLevel", "previewBeatmapLevel" })
            {
                var level = TryReadMember(beatmap, levelMemberName);
                bpm = TryReadFloatMember(level, "beatsPerMinute");
                if (bpm.HasValue) return bpm;
            }

            return null;
        }

        private static float? TryGetSelectedLevelBpm()
        {
            if (_levelDetailInstance == null) return null;

            foreach (var levelMemberName in new[] { "beatmapLevel", "previewBeatmapLevel", "level" })
            {
                var level = TryReadMember(_levelDetailInstance, levelMemberName);
                var bpm = TryReadFloatMember(level, "beatsPerMinute");
                if (bpm.HasValue) return bpm;
            }

            return null;
        }

        private static object TryReadMember(object instance, string memberName)
        {
            if (instance == null || string.IsNullOrEmpty(memberName)) return null;

            var type = instance.GetType();
            var property = type.GetProperty(memberName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                return property.GetValue(instance, null);
            }

            var field = type.GetField(memberName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(instance);
        }

        private static float? TryReadFloatMember(object instance, string memberName)
        {
            var value = TryReadMember(instance, memberName);
            if (value is float floatValue) return floatValue;
            if (value is double doubleValue) return (float)doubleValue;
            if (value is int intValue) return intValue;
            return null;
        }

        // ── Registrar ─────────────────────────────────────────────

        private class RegistrarBehaviour : MonoBehaviour
        {
            internal static RegistrarBehaviour Instance { get; private set; }

            private Coroutine _setupRoutine;

            private void Awake()
            {
                Instance = this;
            }

            private void OnDestroy()
            {
                if (Instance == this)
                    Instance = null;
            }

            private void Start()
            {
                RequestSetup();
            }

            internal void RequestSetup()
            {
                if (_setupRoutine != null)
                    StopCoroutine(_setupRoutine);
                _setupRoutine = StartCoroutine(SetupCoroutine());
            }

            private IEnumerator SetupCoroutine()
            {
                // Give Zenject/BSML a frame to finish installing the new menu container
                // after soft restart before touching GameplaySetup.Instance.
                yield return new WaitForEndOfFrame();

                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        NJSSettingsUI.Instance.Register();
                        if (NJSSettingsUI.Instance.IsRegistered)
                            break;
                    }
                    catch (System.Exception ex)
                    {
                        Plugin.Log?.Warn($"GameplaySetup register attempt {i + 1} failed: {ex.Message}");
                    }
                    yield return new WaitForSeconds(0.5f);
                }

                // Menu VCs are recreated on soft restart; drop stale handlers first.
                UnsubscribeFromAllEvents();
                _levelDetailType = null;
                _levelDetailInstance = null;
                _levelCollectionType = null;
                _levelCollectionInstance = null;

                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        SubscribeToMenuEvents();
                        SubscribeToLevelCollectionEvents();
                        if (_subscribedEvents.Count > 0 || _subscribedLevelEvents.Count > 0)
                            break;
                    }
                    catch (System.Exception ex)
                    {
                        Plugin.Log?.Warn($"Subscription attempt {i + 1} failed: {ex.Message}");
                    }
                    _levelDetailType = null;
                    _levelDetailInstance = null;
                    _levelCollectionType = null;
                    _levelCollectionInstance = null;
                    yield return new WaitForSeconds(1f);
                }

                OnMenuDifficultyChanged();
                _setupRoutine = null;
            }
        }
    }
}
