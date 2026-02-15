using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.Reflection;

namespace ARNavExperiment.EditorTools
{
    public class SceneWiringTool
    {
        [MenuItem("ARNav/Wire Scene References")]
        public static void WireReferences()
        {
            WireConditionController();
            WireTriggerController();
            WireBeamProHub();
            WireMissionBriefingUI();
            WireVerificationUI();
            WireConfidenceRatingUI();
            WireDifficultyRatingUI();
            WireExperimentHUD();
            WireInfoCardManager();
            WirePOIDetailPanel();

            Debug.Log("[SceneWiring] 모든 참조 연결 완료!");
            EditorUtility.DisplayDialog("완료", "컴포넌트 참조 연결이 완료되었습니다.", "확인");
        }

        private static void WireConditionController()
        {
            var cc = Object.FindObjectOfType<Core.ConditionController>();
            if (cc == null) return;
            var so = new SerializedObject(cc);
            SetObjectRef(so, "beamProUI", FindGO("BeamProCanvas"));
            SetObjectRef(so, "lockedScreenUI", FindGO("LockedScreen"));
            so.ApplyModifiedProperties();
        }

        private static void WireTriggerController()
        {
            var tc = Object.FindObjectOfType<Navigation.TriggerController>();
            if (tc == null) return;
            var so = new SerializedObject(tc);
            SetObjectRef(so, "arrowRenderer", FindComponent<Navigation.ARArrowRenderer>());
            SetObjectRef(so, "proximityText", FindComponent<TextMeshProUGUI>("ProximityText"));
            so.ApplyModifiedProperties();
        }

        private static void WireBeamProHub()
        {
            var hub = Object.FindObjectOfType<BeamPro.BeamProHubController>();
            if (hub == null) return;
            var so = new SerializedObject(hub);
            SetObjectRef(so, "hubRoot", FindGO("BeamProCanvas"));
            SetObjectRef(so, "lockedScreen", FindGO("LockedScreen"));
            SetObjectRef(so, "mapController", FindComponent<BeamPro.InteractiveMapController>());
            SetObjectRef(so, "infoCardManager", FindComponent<BeamPro.InfoCardManager>());
            SetObjectRef(so, "missionRefPanel", FindComponent<BeamPro.MissionRefPanel>());

            // tab panels
            var mapPanel = FindGO("MapPanel");
            var infoPanel = FindGO("InfoCardPanel");
            var missionRefPanel = FindGO("MissionRefPanel");
            var tabPanelsProp = so.FindProperty("tabPanels");
            if (tabPanelsProp != null)
            {
                tabPanelsProp.arraySize = 3;
                if (mapPanel) tabPanelsProp.GetArrayElementAtIndex(0).objectReferenceValue = mapPanel;
                if (infoPanel) tabPanelsProp.GetArrayElementAtIndex(1).objectReferenceValue = infoPanel;
                if (missionRefPanel) tabPanelsProp.GetArrayElementAtIndex(2).objectReferenceValue = missionRefPanel;
            }

            // tab buttons
            var tabBar = FindGO("TabBar");
            if (tabBar != null)
            {
                var buttons = tabBar.GetComponentsInChildren<Button>();
                var tabBtnsProp = so.FindProperty("tabButtons");
                if (tabBtnsProp != null)
                {
                    tabBtnsProp.arraySize = buttons.Length;
                    for (int i = 0; i < buttons.Length; i++)
                        tabBtnsProp.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
                }
            }

            so.ApplyModifiedProperties();
        }

        private static void WireMissionBriefingUI()
        {
            var ui = Object.FindObjectOfType<Mission.MissionBriefingUI>();
            if (ui == null) return;
            var panel = ui.gameObject;
            var so = new SerializedObject(ui);
            SetObjectRef(so, "panel", panel);

            var texts = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in texts)
            {
                if (t.gameObject.name == "MissionIdText") SetObjectRef(so, "missionIdText", t);
                if (t.gameObject.name == "BriefingText") SetObjectRef(so, "briefingText", t);
            }

            var btns = panel.GetComponentsInChildren<Button>(true);
            if (btns.Length > 0) SetObjectRef(so, "confirmButton", btns[0]);

            so.ApplyModifiedProperties();
        }

        private static void WireVerificationUI()
        {
            var ui = Object.FindObjectOfType<Mission.VerificationUI>();
            if (ui == null) return;
            var panel = ui.gameObject;
            var so = new SerializedObject(ui);
            SetObjectRef(so, "panel", panel);

            var texts = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in texts)
            {
                if (t.gameObject.name == "QuestionText") SetObjectRef(so, "questionText", t);
            }

            // answer buttons
            var answersGO = FindChildRecursive(panel.transform, "Answers");
            if (answersGO != null)
            {
                var btns = answersGO.GetComponentsInChildren<Button>(true);
                var btnsProp = so.FindProperty("answerButtons");
                var txtsProp = so.FindProperty("answerTexts");
                if (btnsProp != null)
                {
                    btnsProp.arraySize = btns.Length;
                    for (int i = 0; i < btns.Length; i++)
                        btnsProp.GetArrayElementAtIndex(i).objectReferenceValue = btns[i];
                }
                if (txtsProp != null)
                {
                    txtsProp.arraySize = btns.Length;
                    for (int i = 0; i < btns.Length; i++)
                    {
                        var txt = btns[i].GetComponentInChildren<TextMeshProUGUI>();
                        if (txt) txtsProp.GetArrayElementAtIndex(i).objectReferenceValue = txt;
                    }
                }
            }

            so.ApplyModifiedProperties();
        }

        private static void WireConfidenceRatingUI()
        {
            var ui = Object.FindObjectOfType<UI.ConfidenceRatingUI>();
            if (ui == null) return;
            var so = new SerializedObject(ui);
            SetObjectRef(so, "panel", ui.gameObject);
            so.ApplyModifiedProperties();
        }

        private static void WireDifficultyRatingUI()
        {
            var ui = Object.FindObjectOfType<UI.DifficultyRatingUI>();
            if (ui == null) return;
            var so = new SerializedObject(ui);
            SetObjectRef(so, "panel", ui.gameObject);
            so.ApplyModifiedProperties();
        }

        private static void WireExperimentHUD()
        {
            var hud = Object.FindObjectOfType<UI.ExperimentHUD>();
            if (hud == null) return;
            var panel = hud.gameObject;
            var so = new SerializedObject(hud);

            var texts = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in texts)
            {
                switch (t.gameObject.name)
                {
                    case "StateText": SetObjectRef(so, "stateText", t); break;
                    case "ConditionText": SetObjectRef(so, "conditionText", t); break;
                    case "MissionText": SetObjectRef(so, "missionText", t); break;
                    case "WPText": SetObjectRef(so, "waypointText", t); break;
                }
            }

            so.ApplyModifiedProperties();
        }

        private static void WireInfoCardManager()
        {
            var mgr = Object.FindObjectOfType<BeamPro.InfoCardManager>();
            if (mgr == null) return;
            var so = new SerializedObject(mgr);
            SetObjectRef(so, "comparisonCard", FindComponent<BeamPro.ComparisonCardUI>());
            so.ApplyModifiedProperties();
        }

        private static void WirePOIDetailPanel()
        {
            var panel = Object.FindObjectOfType<BeamPro.POIDetailPanel>(true);
            if (panel == null) return;
            var so = new SerializedObject(panel);
            SetObjectRef(so, "panel", panel.gameObject);
            so.ApplyModifiedProperties();
        }

        // --- Utility ---

        private static GameObject FindGO(string name)
        {
            var all = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in all)
            {
                if (go.name == name && go.scene.isLoaded)
                    return go;
            }
            return null;
        }

        private static T FindComponent<T>(string goName = null) where T : Component
        {
            if (!string.IsNullOrEmpty(goName))
            {
                var go = FindGO(goName);
                return go != null ? go.GetComponent<T>() : null;
            }
            return Object.FindObjectOfType<T>(true);
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name) return child;
                var found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static void SetObjectRef(SerializedObject so, string fieldName, Object value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop != null && value != null)
                prop.objectReferenceValue = value;
        }
    }
}
