using System.Collections.Generic;

namespace ARNavExperiment.Core
{
    public static class LocalizationTable
    {
        private static readonly Dictionary<string, (string en, string ko)> Table = new()
        {
            // ===== AppModeSelector =====
            { "appmode.title", ("AR Navigation Experiment", "AR 내비게이션 실험") },
            { "appmode.glass_only_btn", ("Glass Only", "글래스 전용") },
            { "appmode.hybrid_btn", ("Hybrid", "하이브리드") },
            { "appmode.mapping_small_btn", ("Mapping", "매핑") },
            { "appmode.route_label", ("Route:", "경로:") },
            { "appmode.mapping_btn", ("Mapping Mode (Preparation)", "매핑 모드 (준비)") },
            { "appmode.mapping_status", ("Mapping status: Route A {0}, Route B {1} anchors", "매핑 상태: Route A {0}, Route B {1}개 앵커") },
            { "appmode.no_mapping", ("No mapping data \u2014 run Mapping Mode first", "매핑 데이터 없음 \u2014 매핑 모드를 먼저 실행하세요") },
            { "appmode.lang_en", ("English", "English") },
            { "appmode.lang_ko", ("\ud55c\uad6d\uc5b4", "\ud55c\uad6d\uc5b4") },

            // ===== Session (AppModeSelector에서 재사용) =====
            { "session.pid_label", ("Participant ID:", "참가자 ID:") },
            { "session.error_no_id", ("Please enter a participant ID (e.g., P01)", "\ucc38\uac00\uc790 ID\ub97c \uc785\ub825\ud558\uc138\uc694 (\uc608: P01)") },
            { "session.route_a", ("A (West-North)", "A (\uc11c\ucabd-\ubd81\ucabd)") },
            { "session.route_b", ("B (East-North)", "B (\ub3d9\ucabd-\ubd81\ucabd)") },
            { "session.glass_only", ("Glass Only", "\uae00\ub798\uc2a4 \uc804\uc6a9") },
            { "session.hybrid", ("Hybrid", "\ud558\uc774\ube0c\ub9ac\ub4dc") },
            { "session.title", ("AR Navigation Experiment", "AR 내비게이션 실험") },
            { "session.subtitle", ("Experimenter Setup", "실험자 설정") },

            // ===== ExperimentFlowUI =====
            { "flow.setup_title", ("Experiment Setup", "\uc2e4\ud5d8 \uc900\ube44") },
            { "flow.setup_detail", ("Participant: {0}\nCondition: {1}\nRoute: {2}\n\nPlease verify that glasses are worn and\ndevice is connected, then press 'Continue'.", "\ucc38\uac00\uc790: {0}\n\uc870\uac74: {1}\n\uacbd\ub85c: {2}\n\n\uae00\ub798\uc2a4 \ucc29\uc6a9 \ubc0f \uae30\uae30 \uc5f0\uacb0\uc744 \ud655\uc778\ud55c \ud6c4\n'\uacc4\uc18d' \ubc84\ud2bc\uc744 \ub204\ub974\uc138\uc694.") },
            { "flow.condition_title", ("Condition Start", "\uc870\uac74 \uc2dc\uc791") },
            { "flow.condition_glass", ("Glass Only", "\uae00\ub798\uc2a4 \uc804\uc6a9") },
            { "flow.condition_hybrid", ("Hybrid (Glass + Phone)", "\ud558\uc774\ube0c\ub9ac\ub4dc (\uae00\ub798\uc2a4 + \ud3f0)") },
            { "flow.condition_detail_glass", ("Use only the AR arrow on the glasses.\nNavigate by observing the environment.\nOperate glass UI via hand tracking.", "글래스의 AR 화살표만 사용하세요.\n환경을 관찰하며 내비게이션하세요.\n글래스 UI는 핸드트래킹으로 조작합니다.") },
            { "flow.condition_detail_hybrid", ("Use the AR arrow on glasses and\nthe info hub on the phone screen.", "AR \ud654\uc0b4\ud45c\ub294 \uae00\ub798\uc2a4\uc5d0\uc11c,\n\uc815\ubcf4 \ud5c8\ube0c\ub294 \ud3f0 \ud654\uba74\uc5d0\uc11c \uc0ac\uc6a9\ud558\uc138\uc694.") },
            { "flow.condition_info", ("Condition: {0}\nRoute: {1}\n\n{2}\n\nStart from the stairs.\nPress 'Continue' when ready.", "\uc870\uac74: {0}\n\uacbd\ub85c: {1}\n\n{2}\n\n\uacc4\ub2e8\uc5d0\uc11c \uc2dc\uc791\ud558\uc138\uc694.\n\uc900\ube44\ub418\uba74 '\uacc4\uc18d' \ubc84\ud2bc\uc744 \ub204\ub974\uc138\uc694.") },
            { "flow.continue", ("Continue", "\uacc4\uc18d") },
            { "flow.survey_title_single", ("Post-Condition Survey ({0})", "\uc870\uac74 \ud6c4 \uc124\ubb38 ({0})") },
            { "flow.survey_instr", ("A survey about your experience in this condition will follow.\n\nThe researcher will hand you a tablet/paper questionnaire.\n- NASA-TLX (Workload)\n- System Trust Scale\n\nPress 'Survey Done' when complete.", "\uc774 \uc870\uac74\uc5d0\uc11c\uc758 \uacbd\ud5d8\uc5d0 \ub300\ud55c \uc124\ubb38\uc774 \uc774\uc5b4\uc9d1\ub2c8\ub2e4.\n\n\uc5f0\uad6c\uc790\uac00 \ud0dc\ube14\ub9bf/\uc885\uc774 \uc124\ubb38\uc9c0\ub97c \uc804\ub2ec\ud569\ub2c8\ub2e4.\n- NASA-TLX (\uc791\uc5c5\ubd80\ud558)\n- \uc2dc\uc2a4\ud15c \uc2e0\ub8b0\ub3c4 \ucc99\ub3c4\n\n\uc644\ub8cc \ud6c4 '\uc124\ubb38 \uc644\ub8cc' \ubc84\ud2bc\uc744 \ub204\ub974\uc138\uc694.") },
            { "flow.survey_done", ("Survey Done", "\uc124\ubb38 \uc644\ub8cc") },
            { "flow.complete_title", ("Experiment Complete!", "\uc2e4\ud5d8 \uc644\ub8cc!") },
            { "flow.complete_text", ("Experiment complete!\n\nThank you for participating.\nData has been saved automatically.\n\nPlease inform the researcher.", "\uc2e4\ud5d8\uc774 \uc644\ub8cc\ub418\uc5c8\uc2b5\ub2c8\ub2e4!\n\n\ucc38\uc5ec\ud574 \uc8fc\uc154\uc11c \uac10\uc0ac\ud569\ub2c8\ub2e4.\n\ub370\uc774\ud130\uac00 \uc790\ub3d9 \uc800\uc7a5\ub418\uc5c8\uc2b5\ub2c8\ub2e4.\n\n\uc5f0\uad6c\uc790\uc5d0\uac8c \uc54c\ub824\uc8fc\uc138\uc694.") },
            { "flow.error_missions", ("<color=#FF6666>ERROR: Mission data not loaded.</color>\n\nIn Unity Editor:\n1. ARNav > Wire Mission Data to Scene\n2. Save the scene (Ctrl+S)\n3. Restart Play Mode", "<color=#FF6666>\uc624\ub958: \ubbf8\uc158 \ub370\uc774\ud130\uac00 \ub85c\ub4dc\ub418\uc9c0 \uc54a\uc558\uc2b5\ub2c8\ub2e4.</color>\n\nUnity \uc5d0\ub514\ud130\uc5d0\uc11c:\n1. ARNav > Wire Mission Data to Scene\n2. \uc528 \uc800\uc7a5 (Ctrl+S)\n3. \ud50c\ub808\uc774 \ubaa8\ub4dc \uc7ac\uc2dc\uc791") },
            { "flow.error_missions_hint", ("<color=#FFCC00>Tap 'Continue' to retry.\nIf the problem persists, restart the app.</color>", "<color=#FFCC00>'\uacc4\uc18d' \ubc84\ud2bc\uc744 \ub2e4\uc2dc \ub20c\ub7ec \uc7ac\uc2dc\ub3c4\ud558\uc138\uc694.\n\ubb38\uc81c\uac00 \uc9c0\uc18d\ub418\uba74 \uc571\uc744 \uc7ac\uc2dc\uc791\ud558\uc138\uc694.</color>") },

            // ===== RelocalizationUI =====
            { "reloc.scanning", ("Scanning the environment ({0})...\nPlease look around slowly.", "\ud658\uacbd\uc744 \uc2a4\uce94 \uc911 ({0})...\n\ucc9c\ucc9c\ud788 \uc8fc\uc704\ub97c \ub458\ub7ec\ubd10 \uc8fc\uc138\uc694.") },
            { "reloc.preparing", ("Preparing...", "\uc900\ube44 \uc911...") },
            { "reloc.coordinate_established", ("Coordinate frame established!\nWaiting for remaining anchors...", "\uc88c\ud45c\uacc4\uac00 \ud655\ub9bd\ub418\uc5c8\uc2b5\ub2c8\ub2e4!\n\ub098\uba38\uc9c0 \uc575\ucee4\ub97c \uae30\ub2e4\ub9ac\ub294 \uc911...") },
            { "reloc.tracked", ("Tracked", "\ucd94\uc801\ub428") },
            { "reloc.timed_out", ("Timed out", "\uc2dc\uac04 \ucd08\uacfc") },
            { "reloc.load_failed", ("Load failed", "\ub85c\ub4dc \uc2e4\ud328") },
            { "reloc.processing", ("Processing", "\ucc98\ub9ac \uc911") },
            { "reloc.success_count", ("Success: {0}/{1}", "\uc131\uacf5: {0}/{1}") },
            { "reloc.failed_count", ("Failed: {0}/{1}", "\uc2e4\ud328: {0}/{1}") },
            { "reloc.complete", ("Environment scan complete!", "\ud658\uacbd \uc2a4\uce94 \uc644\ub8cc!") },
            { "reloc.all_recognized", ("All anchors recognized.", "\ubaa8\ub4e0 \uc575\ucee4\uac00 \uc778\uc2dd\ub418\uc5c8\uc2b5\ub2c8\ub2e4.") },
            { "reloc.all_recognized_detail", ("Scan complete: {0}/{1} anchors recognized.", "스캔 완료: 성공 {0}/{1}") },
            { "reloc.partial_fail", ("Environment scan complete (partial failure)", "\ud658\uacbd \uc2a4\uce94 \uc644\ub8cc (\ubd80\ubd84 \uc2e4\ud328)") },
            { "reloc.result_detail", ("Success: {0} / Timed out: {1} / Failed: {2}", "\uc131\uacf5: {0} / \uc2dc\uac04\ucd08\uacfc: {1} / \uc2e4\ud328: {2}") },
            { "reloc.warning", ("Success rate: {0}\nFallback waypoints: {1}\n\nProceeding will use floor plan estimates (fallback) for failed anchors.\nRetry will reload only the failed anchors.", "\uc131\uacf5\ub960: {0}\nFallback \uc6e8\uc774\ud3ec\uc778\ud2b8: {1}\n\n\uacc4\uc18d \uc9c4\ud589 \uc2dc \uc2e4\ud328\ud55c \uc575\ucee4\ub294 \ub3c4\uba74 \ucd94\uc815\uce58(fallback)\ub97c \uc0ac\uc6a9\ud569\ub2c8\ub2e4.\n\uc7ac\uc2dc\ub3c4 \uc2dc \uc2e4\ud328\ud55c \uc575\ucee4\ub9cc \ub2e4\uc2dc \ub85c\ub4dc\ud569\ub2c8\ub2e4.") },
            { "reloc.auto_proceed", ("Auto-proceed in {0}s...", "{0}\ucd08 \ud6c4 \uc790\ub3d9 \uc9c4\ud589...") },
            { "reloc.proceed", ("Proceed", "\uc9c4\ud589") },
            { "reloc.retry", ("Retry", "\uc7ac\uc2dc\ub3c4") },
            { "reloc.retrying", ("Retrying failed anchors...\nPlease look around slowly.", "\uc2e4\ud328\ud55c \uc575\ucee4\ub97c \uc7ac\uc2dc\ub3c4 \uc911...\n\ucc9c\ucc9c\ud788 \uc8fc\uc704\ub97c \ub458\ub7ec\ubd10 \uc8fc\uc138\uc694.") },
            { "reloc.retrying_short", ("Retrying...", "\uc7ac\uc2dc\ub3c4 \uc911...") },
            { "reloc.result_summary", ("Anchor positions:\n{0}", "\uc575\ucee4 \uc704\uce58:\n{0}") },
            { "reloc.spatial_warning", ("SPATIAL WARNING: {0}", "\uacf5\uac04 \uacbd\uace0: {0}") },

            // ===== GlassModeStatusPanel =====
            { "glass.title", ("AR Navigation Experiment", "AR \ub0b4\ube44\uac8c\uc774\uc158 \uc2e4\ud5d8") },
            { "glass.waiting", ("Waiting for mode selection", "\ubaa8\ub4dc \uc120\ud0dd \ub300\uae30 \uc911") },
            { "glass.instruction", ("Select a mode on Beam Pro", "Beam Pro\uc5d0\uc11c \ubaa8\ub4dc\ub97c \uc120\ud0dd\ud558\uc138\uc694") },

            // ===== DifficultyRatingUI =====
            { "difficulty.prompt", ("Rate the difficulty of this mission", "\uc774 \ubbf8\uc158\uc758 \ub09c\uc774\ub3c4\ub97c \ud3c9\uac00\ud558\uc138\uc694") },

            // ===== ConfidenceRatingUI =====
            { "confidence.prompt", ("Rate your confidence in your answer", "\ub2f5\ubcc0\uc5d0 \ub300\ud55c \ud655\uc2e0\ub3c4\ub97c \ud3c9\uac00\ud558\uc138\uc694") },
            { "confidence.confirm", ("Confirm", "\ud655\uc778") },

            // ===== MissionBriefingUI =====
            { "briefing.mission", ("Mission {0}", "\ubbf8\uc158 {0}") },
            { "briefing.confirm", ("Confirm", "\ud655\uc778") },

            // ===== VerificationUI =====
            // (uses SO data directly, no static keys needed beyond labels)

            // ===== MissionRefPanel =====
            { "missionref.title", ("Mission {0}", "\ubbf8\uc158 {0}") },
            { "missionref.hint_a", ("Check the destination POI on the map", "\uc9c0\ub3c4\uc5d0\uc11c \ubaa9\uc801\uc9c0 POI\ub97c \ud655\uc778\ud558\uc138\uc694") },
            { "missionref.hint_b", ("Refer to the surrounding map info", "\uc8fc\ubcc0 \uc9c0\ub3c4 \uc815\ubcf4\ub97c \ucc38\uace0\ud558\uc138\uc694") },
            { "missionref.hint_c", ("Compare the attributes of both items", "\ub450 \ud56d\ubaa9\uc758 \uc18d\uc131\uc744 \ube44\uad50\ud558\uc138\uc694") },

            // ===== POIDetailPanel =====
            { "poi.capacity", ("Capacity: {0}", "\uc218\uc6a9 \uc778\uc6d0: {0}") },
            { "poi.equipment", ("Equipment: {0}", "\uc7a5\ube44: {0}") },

            // ===== ExperimentHUD =====
            { "hud.state", ("State: {0}", "\uc0c1\ud0dc: {0}") },
            { "hud.condition", ("Condition: {0}", "\uc870\uac74: {0}") },
            { "hud.condition_none", ("Condition: \u2014", "\uc870\uac74: \u2014") },
            { "hud.mission", ("Mission: {0} ({1})", "\ubbf8\uc158: {0} ({1})") },
            { "hud.no_mission", ("No active mission", "\ud65c\uc131 \ubbf8\uc158 \uc5c6\uc74c") },
            { "hud.waypoint", ("WP: {0} ({1}m)", "WP: {0} ({1}m)") },
            { "hud.no_waypoint", ("No waypoint", "\uc6e8\uc774\ud3ec\uc778\ud2b8 \uc5c6\uc74c") },

            // ===== ExperimenterHUD =====
            { "exphud.fallback", ("\u26a0 Fallback: {0} WP", "\u26a0 Fallback: {0} WP") },
            { "exphud.anchors_ok", ("Anchors: OK", "\uc575\ucee4: \uc815\uc0c1") },
            { "exphud.stop_capture", ("Stop Capture", "\ucea1\ucc98 \uc911\uc9c0") },
            { "exphud.capture", ("Capture", "\ucea1\ucc98") },
            { "exphud.rec", ("REC", "REC") },
            { "exphud.next_mission", ("Next Mission", "다음 미션") },
            { "exphud.skip_briefing", ("Skip Briefing", "브리핑 건너뛰기") },
            { "exphud.force_arrival", ("Force Arrival", "도착 선언") },
            { "exphud.skip_mission", ("Skip Mission", "미션 건너뛰기") },

            // ===== MappingModeUI =====
            { "mapping.title", ("Mapping Mode \u2014 Route {0}", "\ub9e4\ud551 \ubaa8\ub4dc \u2014 Route {0}") },
            { "mapping.slam_not_ready", ("SLAM not ready", "SLAM \uc900\ube44 \uc548 \ub428") },
            { "mapping.create_anchor", ("Create Anchor: {0}", "\uc575\ucee4 \uc0dd\uc131: {0}") },
            { "mapping.select_waypoint", ("Select a waypoint", "\uc6e8\uc774\ud3ec\uc778\ud2b8\ub97c \uc120\ud0dd\ud558\uc138\uc694") },
            { "mapping.creating", ("Creating...", "\uc0dd\uc131 \uc911...") },
            { "mapping.mapped", ("Mapped", "\ub9e4\ud551\ub428") },
            { "mapping.unmapped", ("Unmapped", "\ubbf8\ub9e4\ud551") },
            { "mapping.select", ("Select", "\uc120\ud0dd") },
            { "mapping.zoom_map", ("Zoom Map", "\uc9c0\ub3c4 \ud655\ub300") },
            { "mapping.save_all", ("Save All", "\ubaa8\ub450 \uc800\uc7a5") },
            { "mapping.back", ("Back", "\ub4a4\ub85c") },
            { "mapping.quality_hint", ("Select waypoint & create anchor", "\uc6e8\uc774\ud3ec\uc778\ud2b8\ub97c \uc120\ud0dd\ud558\uace0 \uc575\ucee4\ub97c \uc0dd\uc131\ud558\uc138\uc694") },

            // ===== MappingGlassOverlay =====
            { "overlay.header", ("Mapping Mode", "\ub9e4\ud551 \ubaa8\ub4dc") },
            { "overlay.select_wp", ("Select a waypoint", "\uc6e8\uc774\ud3ec\uc778\ud2b8\ub97c \uc120\ud0dd\ud558\uc138\uc694") },
            { "overlay.checking_slam", ("Checking SLAM...", "SLAM \ud655\uc778 \uc911...") },
            { "overlay.quality_waiting", ("Quality: Waiting", "\ud488\uc9c8: \ub300\uae30 \uc911") },
            { "overlay.quality_poor", ("Quality: Poor", "\ud488\uc9c8: \ub098\uc068") },
            { "overlay.quality_fair", ("Quality: Fair", "\ud488\uc9c8: \ubcf4\ud1b5") },
            { "overlay.quality_good", ("Quality: Good", "\ud488\uc9c8: \uc88b\uc74c") },
            { "overlay.anchor_created", ("\u2713 Anchor created!", "\u2713 \uc575\ucee4 \uc0dd\uc131 \uc644\ub8cc!") },
            { "overlay.creation_failed", ("\u2717 Creation failed", "\u2717 \uc0dd\uc131 \uc2e4\ud328") },
            { "overlay.mapped_count", ("{0}/{1} Mapped", "{0}/{1} \ub9e4\ud551\ub428") },

            // ===== Map =====
            { "map.current_position", ("You are here", "현재 위치") },
            { "map.destination", ("Destination", "목적지") },

            // ===== BeamProHubController =====
            { "beam.tab_map", ("Map", "\uc9c0\ub3c4") },
            { "beam.tab_info", ("Info Cards", "\uc815\ubcf4 \uce74\ub4dc") },
            { "beam.tab_mission", ("Mission Ref", "\ubbf8\uc158 \ucc38\uace0") },

            // ===== Locked Screen =====
            { "locked.message", ("This device is not available\nin the current condition.\n\nPlease use only the AR glasses.", "현재 조건에서는\n이 기기를 사용할 수 없습니다.\n\nAR 글래스만 사용해 주세요.") },

            // ===== GlassFlowUI =====
            { "glassflow.reloc_scanning", ("Scanning... ({0}%)\nLook around slowly.", "스캔 중... ({0}%)\n천천히 주위를 둘러보세요.") },
            { "glassflow.reloc_complete", ("Scan complete! ({0}%)", "스캔 완료! ({0}%)") },
            { "glassflow.reloc_proceed", ("Proceed", "진행") },
            { "glassflow.reloc_auto", ("Auto-proceed in {0}s", "자동 진행: {0}초") },
            { "glassflow.setup_title", ("Experiment Setup", "실험 준비") },
            { "glassflow.setup_detail", ("PID: {0} | {1} | Route {2}\n\nPinch 'Continue' to start.", "PID: {0} | {1} | Route {2}\n\n'계속' 버튼을 핀치하세요.") },
            { "glassflow.condition_title", ("Start Navigation", "내비게이션 시작") },
            { "glassflow.condition_detail_glass", ("Navigate using AR arrows.\nPinch glass UI to interact.", "AR 화살표로 내비게이션합니다.\n글래스 UI를 핀치로 조작하세요.") },
            { "glassflow.continue", ("Continue", "계속") },
            { "glassflow.start", ("Start", "시작") },
            { "glassflow.survey_title", ("Survey Time", "설문 시간") },
            { "glassflow.survey_instr", ("Please complete the survey\nfrom the researcher.\n\nPinch 'Done' when finished.", "연구자의 설문지를 작성해 주세요.\n\n완료 후 '완료'를 핀치하세요.") },
            { "glassflow.survey_done", ("Done", "완료") },
            { "glassflow.complete_title", ("Complete!", "완료!") },
            { "glassflow.complete_text", ("Experiment complete!\nThank you.", "실험 완료!\n감사합니다.") },
        };

        public static string GetEN(string key)
        {
            return Table.TryGetValue(key, out var pair) ? pair.en : $"[{key}]";
        }

        public static string GetKO(string key)
        {
            return Table.TryGetValue(key, out var pair) ? pair.ko : $"[{key}]";
        }
    }
}
