using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace Unity.AI.Assistant.PlayModeTest
{
    [InitializeOnLoad]
    internal static class PlayModeTestRunner
    {
        private const string StateKey = "PlayModeTest.State";
        private const string ResultKey = "PlayModeTest.Result";
        private const string ScriptPathKey = "PlayModeTest.ScriptPath";
        private const string SentinelLog = "PLAY_MODE_TEST_COMPLETE";

        private static readonly int WaitFrames = 10;
        private static readonly float TestTimeout = 15.0f;

        private static List<string> _capturedLogs = new List<string>();
        private const int MaxCapturedLogs = 50;

        static PlayModeTestRunner()
        {
            string state = SessionState.GetString(StateKey, "Idle");
            switch (state)
            {
                case "Idle": break;
                case "WaitingForCompile":
                    EditorApplication.delayCall += () => {
                        SessionState.SetString(StateKey, "EnteringPlayMode");
                        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                        EditorApplication.isPlaying = true;
                    };
                    break;
                case "EnteringPlayMode":
                    EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                    if (EditorApplication.isPlaying) {
                        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                        SessionState.SetString(StateKey, "InPlayMode");
                        EditorApplication.update += WaitFramesThenRun;
                    }
                    break;
                case "InPlayMode":
                    if (EditorApplication.isPlaying) EditorApplication.update += WaitFramesThenRun;
                    break;
                case "Done":
                    EditorApplication.delayCall += SelfDestruct;
                    break;
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode) {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                SessionState.SetString(StateKey, "InPlayMode");
                EditorApplication.update += WaitFramesThenRun;
            }
        }

        private static int _frameCount = 0;
        private static bool _setupDone = false;
        private static bool _testDone = false;
        private static double _testStartTime = 0;

        private static void WaitFramesThenRun()
        {
            _frameCount++;
            if (_frameCount < WaitFrames) return;
            if (_testDone) return;

            if (!_setupDone) {
                _setupDone = true;
                Application.logMessageReceived += OnLogMessage;
                _testStartTime = EditorApplication.timeSinceStartup;
                try { Setup(); }
                catch (System.Exception e) { FinishTest(true, e.Message); }
                return;
            }

            float elapsed = (float)(EditorApplication.timeSinceStartup - _testStartTime);
            bool timedOut = elapsed >= TestTimeout;
            try {
                bool complete = Tick(elapsed);
                if (complete || timedOut) FinishTest(timedOut, timedOut ? "Timed out" : null);
            } catch (System.Exception e) { FinishTest(true, e.Message); }
        }

        private static void FinishTest(bool isError, string errorMessage)
        {
            _testDone = true;
            EditorApplication.update -= WaitFramesThenRun;
            Application.logMessageReceived -= OnLogMessage;
            string resultJson = GetResult();
            SessionState.SetString(ResultKey, resultJson);
            SessionState.SetString(StateKey, "Done");
            EditorApplication.isPlaying = false;
        }

        private static void OnLogMessage(string message, string stackTrace, LogType type)
        {
            if (_capturedLogs.Count >= MaxCapturedLogs) return;
            if (type == LogType.Error || type == LogType.Exception || message.Contains("[Test]"))
                _capturedLogs.Add("[" + type + "] " + message);
        }

        private static void SelfDestruct()
        {
            string scriptPath = SessionState.GetString(ScriptPathKey, "");
            if (!string.IsNullOrEmpty(scriptPath) && AssetDatabase.AssetPathExists(scriptPath))
                AssetDatabase.DeleteAsset(scriptPath);
            SessionState.EraseString(StateKey);
            SessionState.EraseString(ScriptPathKey);
        }

        [System.Serializable]
        private class TestResult {
            public bool success;
            public string error;
            public string[] logs;
            public bool isExpanded;
            public string displayStyle;
            public int subItemCount;
        }

        private static void Setup()
        {
            Debug.Log("[Test] Setup: Clicking pickAndRollHeader");
            var root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            var header = root.Q<Button>("pickAndRollHeader");
            if (header != null) {
                using (var e = ClickEvent.GetPooled()) { e.target = header; header.SendEvent(e); }
                Debug.Log("[Test] Clicked.");
            } else Debug.LogError("[Test] Header not found!");
        }

        private static bool Tick(float elapsed) { return elapsed > 3.0f; }

        private static string GetResult()
        {
            var result = new TestResult { success = true, logs = _capturedLogs.ToArray() };
            var root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            var foldout = root.Q<VisualElement>("pickAndRollFoldout");
            var content = root.Q<VisualElement>("pickAndRollContent");
            if (foldout != null) result.isExpanded = foldout.ClassListContains("expanded");
            if (content != null) {
                result.displayStyle = content.resolvedStyle.display.ToString();
                result.subItemCount = content.childCount;
            }
            return JsonUtility.ToJson(result);
        }
    }
}