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

        private static readonly int WaitFrames = SessionState.GetInt("PlayModeTest.WaitFrames", 5);
        private static readonly float TestTimeout = SessionState.GetFloat("PlayModeTest.TestTimeout", 15.0f);

        private static List<string> _capturedLogs = new List<string>();
        private const int MaxCapturedLogs = 80;

        static PlayModeTestRunner()
        {
            string state = SessionState.GetString(StateKey, "Idle");
            switch (state)
            {
                case "Idle": break;
                case "WaitingForCompile":
                    Debug.Log("[PlayModeTest] Bootstrap compiled. Scheduling Play Mode entry.");
                    EditorApplication.delayCall += () =>
                    {
                        SessionState.SetString(StateKey, "EnteringPlayMode");
                        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                        EditorApplication.isPlaying = true;
                    };
                    break;
                case "EnteringPlayMode":
                    EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                    if (EditorApplication.isPlaying)
                    {
                        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                        SessionState.SetString(StateKey, "InPlayMode");
                        EditorApplication.update += WaitFramesThenRun;
                    }
                    break;
                case "InPlayMode":
                    if (EditorApplication.isPlaying)
                        EditorApplication.update += WaitFramesThenRun;
                    break;
                case "Done":
                    Debug.Log(SentinelLog);
                    EditorApplication.delayCall += SelfDestruct;
                    break;
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                SessionState.SetString(StateKey, "InPlayMode");
                EditorApplication.update += WaitFramesThenRun;
            }
        }

        private static int _frameCount = 0;
        private static bool _setupDone = false;
        private static bool _testDone = false;
        private static double _testStartTime = 0;
        private static bool _screenshotCaptured = false;
        private static string _screenshotPath;

        private static void WaitFramesThenRun()
        {
            _frameCount++;
            if (_frameCount < WaitFrames) return;
            if (_testDone) return;

            if (!_setupDone)
            {
                _setupDone = true;
                Application.logMessageReceived += OnLogMessage;
                _testStartTime = EditorApplication.timeSinceStartup;
                try { Setup(); }
                catch (System.Exception e) { Debug.LogError("[PlayModeTest] Setup threw: " + e); FinishTest(true, e.Message); }
                return;
            }

            float elapsed = (float)(EditorApplication.timeSinceStartup - _testStartTime);
            bool timedOut = elapsed >= TestTimeout;
            try
            {
                bool complete = Tick(elapsed);
                if (complete || timedOut)
                    FinishTest(timedOut && !complete, timedOut ? "Timed out" : null);
            }
            catch (System.Exception e) { Debug.LogError("[PlayModeTest] Tick threw: " + e); FinishTest(true, e.Message); }
        }

        private static void FinishTest(bool isError, string errorMessage)
        {
            _testDone = true;
            EditorApplication.update -= WaitFramesThenRun;
            Application.logMessageReceived -= OnLogMessage;
            string resultJson;
            try { resultJson = GetResult(); }
            catch (System.Exception e)
            {
                resultJson = JsonUtility.ToJson(new TestResult { success = false, error = "GetResult threw: " + e.Message, logs = _capturedLogs.ToArray() });
            }
            if (isError && errorMessage != null)
                resultJson = JsonUtility.ToJson(new TestResult { success = false, error = errorMessage, logs = _capturedLogs.ToArray() });
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
        private class TestResult
        {
            public bool success;
            public string error;
            public string[] logs;
            public string screenshotPath;
        }

        private static VisualElement _root;

        private static string Rect(string label, VisualElement ve)
        {
            if (ve == null) return label + "=NOTFOUND";
            var r = ve.layout;
            return string.Format("{0}=[w{1:0} h{2:0} disp={3}]", label, r.width, r.height, ve.resolvedStyle.display);
        }

        private static void Setup()
        {
            var go = GameObject.Find("UIDocument_CoachDashboard");
            if (go == null) { Debug.LogError("[Test] UIDocument NOT FOUND"); return; }
            var uiDoc = go.GetComponent<UIDocument>();
            _root = uiDoc != null ? uiDoc.rootVisualElement : null;
            if (_root == null) { Debug.LogError("[Test] root NULL"); return; }

            var elem = _root.Q("drillListContainer");
            Debug.Log("[Test] drillListContainer runtime type = " + (elem == null ? "NULL" : elem.GetType().FullName));

            var foldout = _root.Q("pickAndRollFoldout");
            if (foldout != null) foldout.AddToClassList("expanded");

            var controller = go.GetComponent("CoachDashboardUIToolkitController") as MonoBehaviour;
            var sessionField = controller.GetType().GetField("_session", (System.Reflection.BindingFlags)36);
            var session = sessionField.GetValue(controller);
            var add = session.GetType().GetMethod("AddDrill");
            add.Invoke(session, new object[] { "PICK & ROLL (TEST)" });
            add.Invoke(session, new object[] { "SHOOTING (TEST)" });
            Debug.Log("[Test] Added 2 drills");
        }

        private static bool Tick(float elapsed)
        {
            if (elapsed < 1.5f) return false;
            if (!_screenshotCaptured)
            {
                var lv = _root.Q<ListView>("drillListContainer");
                Debug.Log("[Test] Q<ListView> = " + (lv == null ? "NULL (BUG)" : "FOUND"));
                Debug.Log("[Test] itemsSource count = " + (lv != null && lv.itemsSource != null ? lv.itemsSource.Count.ToString() : "n/a"));
                Debug.Log("[Test] " + Rect("drillListContainer", _root.Q("drillListContainer")));
                Debug.Log("[Test] " + Rect("buildSessionPlaceholder", _root.Q("buildSessionPlaceholder")));
                int rows = 0;
                _root.Query<VisualElement>(className: "drill-item-uss").ForEach(_ => rows++);
                Debug.Log("[Test] rendered drill-item rows = " + rows);

                _screenshotPath = "Assets/PlayModeTest_dashboard.png";
                ScreenCapture.CaptureScreenshot(_screenshotPath);
                SessionState.SetString("PlayModeTest.ScreenshotPath", _screenshotPath);
                _screenshotCaptured = true;
                return false;
            }
            return true;
        }

        private static string GetResult()
        {
            return JsonUtility.ToJson(new TestResult { success = true, screenshotPath = _screenshotPath, logs = _capturedLogs.ToArray() });
        }
    }
}
