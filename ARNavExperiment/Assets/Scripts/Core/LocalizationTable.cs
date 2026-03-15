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
            { "appmode.set_label", ("Mission Set:", "미션 세트:") },
            { "appmode.mapping_btn", ("Mapping Mode (Preparation)", "매핑 모드 (준비)") },
            { "appmode.set1", ("Set 1", "세트 1") },
            { "appmode.set2", ("Set 2", "세트 2") },
            { "appmode.mapping_status", ("Mapping status: {0} anchors", "매핑 상태: {0}개 앵커") },
            { "appmode.no_mapping", ("No mapping data \u2014 run Mapping Mode first", "매핑 데이터 없음 \u2014 매핑 모드를 먼저 실행하세요") },
            { "appmode.lang_en", ("English", "English") },
            { "appmode.lang_ko", ("\ud55c\uad6d\uc5b4", "\ud55c\uad6d\uc5b4") },

            // ===== Session (AppModeSelector에서 재사용) =====
            { "session.pid_label", ("Participant ID:", "참가자 ID:") },
            { "session.error_no_id", ("Please enter a participant ID (e.g., P01)", "\ucc38\uac00\uc790 ID\ub97c \uc785\ub825\ud558\uc138\uc694 (\uc608: P01)") },
            { "session.set1", ("Set 1", "\uc138\ud2b8 1") },
            { "session.set2", ("Set 2", "\uc138\ud2b8 2") },
            { "session.glass_only", ("Glass Only", "\uae00\ub798\uc2a4 \uc804\uc6a9") },
            { "session.hybrid", ("Hybrid", "\ud558\uc774\ube0c\ub9ac\ub4dc") },
            { "session.title", ("AR Navigation Experiment", "AR 내비게이션 실험") },
            { "session.subtitle", ("Experimenter Setup", "실험자 설정") },

            // ===== ExperimentFlowUI =====
            { "flow.setup_title", ("Experiment Setup", "\uc2e4\ud5d8 \uc900\ube44") },
            { "flow.setup_detail", ("Participant: {0}\nCondition: {1}\nMission Set: {2}\n\nPlease verify that glasses are worn and\ndevice is connected, then press 'Continue'.", "\ucc38\uac00\uc790: {0}\n\uc870\uac74: {1}\n\ubbf8\uc158 \uc138\ud2b8: {2}\n\n\uae00\ub798\uc2a4 \ucc29\uc6a9 \ubc0f \uae30\uae30 \uc5f0\uacb0\uc744 \ud655\uc778\ud55c \ud6c4\n'\uacc4\uc18d' \ubc84\ud2bc\uc744 \ub204\ub974\uc138\uc694.") },
            { "flow.condition_title", ("Condition Start", "\uc870\uac74 \uc2dc\uc791") },
            { "flow.condition_glass", ("Glass Only", "\uae00\ub798\uc2a4 \uc804\uc6a9") },
            { "flow.condition_hybrid", ("Hybrid (Glass + Phone)", "\ud558\uc774\ube0c\ub9ac\ub4dc (\uae00\ub798\uc2a4 + \ud3f0)") },
            { "flow.condition_detail_glass", ("Use only the AR arrow on the glasses.\nNavigate by observing the environment.\nOperate glass UI via hand tracking.", "글래스의 AR 화살표만 사용하세요.\n환경을 관찰하며 내비게이션하세요.\n글래스 UI는 핸드트래킹으로 조작합니다.") },
            { "flow.condition_detail_hybrid", ("Use the AR arrow on glasses and\nthe info hub on the phone screen.", "AR \ud654\uc0b4\ud45c\ub294 \uae00\ub798\uc2a4\uc5d0\uc11c,\n\uc815\ubcf4 \ud5c8\ube0c\ub294 \ud3f0 \ud654\uba74\uc5d0\uc11c \uc0ac\uc6a9\ud558\uc138\uc694.") },
            { "flow.condition_info", ("Condition: {0}\nMission Set: {1}\n\n{2}\n\nStart from the stairs.\nPress 'Continue' when ready.", "\uc870\uac74: {0}\n\ubbf8\uc158 \uc138\ud2b8: {1}\n\n{2}\n\n\uacc4\ub2e8\uc5d0\uc11c \uc2dc\uc791\ud558\uc138\uc694.\n\uc900\ube44\ub418\uba74 '\uacc4\uc18d' \ubc84\ud2bc\uc744 \ub204\ub974\uc138\uc694.") },
            { "flow.continue", ("Continue", "\uacc4\uc18d") },
            { "flow.survey_title_single", ("Post-Condition Survey ({0})", "\uc870\uac74 \ud6c4 \uc124\ubb38 ({0})") },
            { "flow.survey_instr", ("A survey about your experience in this condition will follow.\n\nThe researcher will hand you a tablet/paper questionnaire.\n- NASA-TLX (Workload)\n- System Trust Scale\n\nPress 'Survey Done' when complete.", "\uc774 \uc870\uac74\uc5d0\uc11c\uc758 \uacbd\ud5d8\uc5d0 \ub300\ud55c \uc124\ubb38\uc774 \uc774\uc5b4\uc9d1\ub2c8\ub2e4.\n\n\uc5f0\uad6c\uc790\uac00 \ud0dc\ube14\ub9bf/\uc885\uc774 \uc124\ubb38\uc9c0\ub97c \uc804\ub2ec\ud569\ub2c8\ub2e4.\n- NASA-TLX (\uc791\uc5c5\ubd80\ud558)\n- \uc2dc\uc2a4\ud15c \uc2e0\ub8b0\ub3c4 \ucc99\ub3c4\n\n\uc644\ub8cc \ud6c4 '\uc124\ubb38 \uc644\ub8cc' \ubc84\ud2bc\uc744 \ub204\ub974\uc138\uc694.") },
            { "flow.survey_done", ("Survey Done", "\uc124\ubb38 \uc644\ub8cc") },
            { "flow.complete_title", ("Experiment Complete!", "\uc2e4\ud5d8 \uc644\ub8cc!") },
            { "flow.complete_text", ("Experiment complete!\n\nThank you for participating.\nData has been saved automatically.\n\nPlease inform the researcher.", "\uc2e4\ud5d8\uc774 \uc644\ub8cc\ub418\uc5c8\uc2b5\ub2c8\ub2e4!\n\n\ucc38\uc5ec\ud574 \uc8fc\uc154\uc11c \uac10\uc0ac\ud569\ub2c8\ub2e4.\n\ub370\uc774\ud130\uac00 \uc790\ub3d9 \uc800\uc7a5\ub418\uc5c8\uc2b5\ub2c8\ub2e4.\n\n\uc5f0\uad6c\uc790\uc5d0\uac8c \uc54c\ub824\uc8fc\uc138\uc694.") },
            { "flow.error_missions", ("<color=#FF6666>ERROR: Mission data not loaded.</color>\n\nIn Unity Editor:\n1. ARNav > Wire Mission Data to Scene\n2. Save the scene (Ctrl+S)\n3. Restart Play Mode", "<color=#FF6666>\uc624\ub958: \ubbf8\uc158 \ub370\uc774\ud130\uac00 \ub85c\ub4dc\ub418\uc9c0 \uc54a\uc558\uc2b5\ub2c8\ub2e4.</color>\n\nUnity \uc5d0\ub514\ud130\uc5d0\uc11c:\n1. ARNav > Wire Mission Data to Scene\n2. \uc528 \uc800\uc7a5 (Ctrl+S)\n3. \ud50c\ub808\uc774 \ubaa8\ub4dc \uc7ac\uc2dc\uc791") },
            { "flow.error_missions_hint", ("<color=#FFCC00>Tap 'Continue' to retry.\nIf the problem persists, restart the app.</color>", "<color=#FFCC00>'\uacc4\uc18d' \ubc84\ud2bc\uc744 \ub2e4\uc2dc \ub20c\ub7ec \uc7ac\uc2dc\ub3c4\ud558\uc138\uc694.\n\ubb38\uc81c\uac00 \uc9c0\uc18d\ub418\uba74 \uc571\uc744 \uc7ac\uc2dc\uc791\ud558\uc138\uc694.</color>") },

            // ===== RelocalizationUI =====
            { "reloc.scanning", ("Scanning the environment ({0})...\nPlease walk along the mapped route and look around slowly.", "\ud658\uacbd\uc744 \uc2a4\uce94 \uc911 ({0})...\n\ub9e4\ud551\ud55c \uacbd\ub85c\ub97c \ub530\ub77c \ucc9c\ucc9c\ud788 \uac78\uc73c\uba70 \uc8fc\uc704\ub97c \ub458\ub7ec\ubd10 \uc8fc\uc138\uc694.") },
            { "reloc.preparing", ("Preparing...", "\uc900\ube44 \uc911...") },
            { "reloc.coordinate_established", ("First anchor recognized!\nPlease walk toward the remaining anchors and look around.", "\uccab \uc575\ucee4 \uc778\uc2dd \uc131\uacf5!\n\ub098\uba38\uc9c0 \uc575\ucee4 \uadfc\ucc98\ub85c \uc774\ub3d9\ud558\uba70 \uc8fc\uc704\ub97c \ub458\ub7ec\ubd10 \uc8fc\uc138\uc694.") },
            { "reloc.tracked", ("Tracked", "\ucd94\uc801\ub428") },
            { "reloc.timed_out", ("Timed out", "\uc2dc\uac04 \ucd08\uacfc") },
            { "reloc.load_failed", ("Load failed", "\ub85c\ub4dc \uc2e4\ud328") },
            { "reloc.processing", ("Processing", "\ucc98\ub9ac \uc911") },
            { "reloc.success_count", ("Success: {0}/{1}", "\uc131\uacf5: {0}/{1}") },
            { "reloc.failed_count", ("Failed: {0}/{1}", "\uc2e4\ud328: {0}/{1}") },
            { "reloc.complete", ("Environment scan complete!", "\ud658\uacbd \uc2a4\uce94 \uc644\ub8cc!") },
            { "reloc.all_recognized", ("All anchors recognized.", "\ubaa8\ub4e0 \uc575\ucee4\uac00 \uc778\uc2dd\ub418\uc5c8\uc2b5\ub2c8\ub2e4.") },
            { "reloc.all_recognized_detail", ("Scan complete: {0}/{1} anchors recognized.", "스캔 완료: 성공 {0}/{1}") },
            { "reloc.nav_ready", ("\u2713 Navigation ready", "\u2713 \ub124\ube44\uac8c\uc774\uc158 \uc900\ube44 \uc644\ub8cc") },
            { "reloc.auto_proceed", ("Auto-proceed in {0}s...", "{0}\ucd08 \ud6c4 \uc790\ub3d9 \uc9c4\ud589...") },
            { "reloc.proceed", ("Proceed", "\uc9c4\ud589") },
            { "reloc.retry", ("Retry", "\uc7ac\uc2dc\ub3c4") },
            { "reloc.retrying", ("Retrying failed anchors...\nPlease look around slowly.", "\uc2e4\ud328\ud55c \uc575\ucee4\ub97c \uc7ac\uc2dc\ub3c4 \uc911...\n\ucc9c\ucc9c\ud788 \uc8fc\uc704\ub97c \ub458\ub7ec\ubd10 \uc8fc\uc138\uc694.") },
            { "reloc.retrying_short", ("Retrying...", "\uc7ac\uc2dc\ub3c4 \uc911...") },
            { "reloc.result_summary", ("Anchor positions:\n{0}", "\uc575\ucee4 \uc704\uce58:\n{0}") },
            { "reloc.spatial_warning", ("SPATIAL WARNING: {0}", "\uacf5\uac04 \uacbd\uace0: {0}") },
            { "reloc.guide_face", ("Please face {0}", "{0}을(를) 마주봐주세요") },
            { "reloc.guide_recognized", ("✓ {0} recognized!", "✓ {0} 인식완료!") },
            { "reloc.guide_timeout", ("{0} skipped (timeout)", "{0} 건너뜀 (시간초과)") },
            { "reloc.guide_done", ("✓ Ready to start!", "✓ 준비 완료!") },
            { "reloc.guide_step", ("Step {0}/{1}", "단계 {0}/{1}") },
            { "reloc.slam_warmup", ("Initializing SLAM... Look around slowly", "SLAM 초기화 중... 천천히 주위를 둘러보세요") },

            // ===== Image Tracking Relocalization =====
            { "reloc.image_scan", ("Scan the marker on the wall.\nLook at it directly.", "\ubcbd\uc758 \ub9c8\ucee4\ub97c \uc2a4\uce94\ud558\uc138\uc694.\n\uc815\uba74\uc73c\ub85c \ubc14\ub77c\ubd10 \uc8fc\uc138\uc694.") },
            { "reloc.image_progress", ("Markers: {0}/{1}", "\ub9c8\ucee4: {0}/{1}") },
            { "reloc.image_marker_found", ("\u2713 {0} detected!", "\u2713 {0} \uac10\uc9c0!") },
            { "reloc.image_aligned", ("\u2713 Alignment complete! Ready to go.", "\u2713 \uc815\ub82c \uc644\ub8cc! \uc900\ube44\ub418\uc5c8\uc2b5\ub2c8\ub2e4.") },

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
            { "briefing.auto_advance", ("Auto-advance in {0}s", "{0}\ucd08 \ud6c4 \uc790\ub3d9 \uc9c4\ud589") },

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
            { "hud.see_phone", ("Check phone for mission info", "\ud3f0\uc5d0\uc11c \ubbf8\uc158 \uc815\ubcf4\ub97c \ud655\uc778\ud558\uc138\uc694") },

            // ===== ExperimenterHUD =====
            { "exphud.map_assist", ("\ud83d\udccd Map-assist: {0} WP", "\ud83d\udccd \ub3c4\uba74\ubcf4\uc815: {0} WP") },
            { "exphud.anchors_ok", ("Anchors: OK", "\uc575\ucee4: \uc815\uc0c1") },
            { "exphud.stop_capture", ("Stop Capture", "\ucea1\ucc98 \uc911\uc9c0") },
            { "exphud.capture", ("Capture", "\ucea1\ucc98") },
            { "exphud.rec", ("REC", "REC") },
            { "exphud.next_mission", ("Next Mission", "다음 미션") },
            { "exphud.skip_briefing", ("Skip Briefing", "브리핑 건너뛰기") },
            { "exphud.force_arrival", ("Force Arrival", "도착 선언") },
            { "exphud.skip_mission", ("Skip Mission", "미션 건너뛰기") },
            { "exphud.heading_left", ("\u2190 5\u00b0", "\u2190 5\u00b0") },
            { "exphud.heading_right", ("5\u00b0 \u2192", "5\u00b0 \u2192") },
            { "exphud.manual_calibrate", ("Calibrate", "\uc218\ub3d9 \ubcf4\uc815") },

            // ===== Glass ForceArrival =====
            { "glass.force_arrival", ("Arrived", "\ub3c4\ucc29") },

            // ===== MappingModeUI =====
            { "mapping.title", ("Mapping Mode", "\ub9e4\ud551 \ubaa8\ub4dc") },
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

            // ===== Reference Anchors =====
            { "mapping.ref_section_title", ("Room Reference Anchors", "\ud638\uc2e4 \ubcf4\uc815 \uc575\ucee4") },
            { "mapping.ref_select_room", ("Select room", "\ud638\uc2e4 \uc120\ud0dd") },
            { "mapping.ref_create", ("Create Ref Anchor", "\ubcf4\uc815 \uc575\ucee4 \uc0dd\uc131") },
            { "mapping.ref_status", ("Room anchors: {0}/{1}", "\ud638\uc2e4 \uc575\ucee4: {0}/{1}") },
            { "mapping.ref_mapped", ("Done", "\uc644\ub8cc") },
            { "mapping.ref_unmapped", ("Not mapped", "\ubbf8\ub9e4\ud551") },

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
            { "beam.toggle_map", ("Map", "\uc9c0\ub3c4") },

            // ===== Locked Screen =====
            { "locked.message", ("This device is not available\nin the current condition.\n\nPlease use only the AR glasses.", "현재 조건에서는\n이 기기를 사용할 수 없습니다.\n\nAR 글래스만 사용해 주세요.") },

            // ===== GlassFlowUI =====
            { "glassflow.reloc_scanning", ("Scanning... ({0}%)\nLook around slowly.", "스캔 중... ({0}%)\n천천히 주위를 둘러보세요.") },
            { "glassflow.reloc_done", ("Scan complete! Ready to go.", "\uc2a4\uce94 \uc644\ub8cc! \uc900\ube44\ub418\uc5c8\uc2b5\ub2c8\ub2e4.") },
            { "glassflow.reloc_proceed", ("Proceed", "진행") },
            { "glassflow.setup_title", ("Experiment Setup", "실험 준비") },
            { "glassflow.setup_detail", ("PID: {0} | {1} | {2}\n\nPinch 'Continue' to start.", "PID: {0} | {1} | {2}\n\n'계속' 버튼을 핀치하세요.") },
            { "glassflow.condition_title", ("Start Navigation", "내비게이션 시작") },
            { "glassflow.condition_detail_glass", ("Navigate using AR arrows.\nPinch glass UI to interact.", "AR 화살표로 내비게이션합니다.\n글래스 UI를 핀치로 조작하세요.") },
            { "glassflow.continue", ("Continue", "계속") },
            { "glassflow.start", ("Start", "시작") },
            { "glassflow.survey_title", ("Survey Time", "설문 시간") },
            { "glassflow.survey_instr", ("Please complete the survey\nfrom the researcher.\n\nPinch 'Done' when finished.", "연구자의 설문지를 작성해 주세요.\n\n완료 후 '완료'를 핀치하세요.") },
            { "glassflow.survey_done", ("Done", "완료") },
            { "glassflow.complete_title", ("Complete!", "완료!") },
            { "glassflow.complete_text", ("Experiment complete!\nThank you.", "실험 완료!\n감사합니다.") },
            { "glassflow.guide_face", ("Face {0}", "{0}을(를) 보세요") },
            { "glassflow.guide_recognized", ("✓ {0} done!", "✓ {0} 완료!") },
            { "glassflow.guide_ready", ("Ready! Starting...", "준비 완료!") },
            { "glassflow.guide_timeout", ("{0} skipped", "{0} 건너뜀") },

            // ===== Preflight Check =====
            { "preflight.header", ("Preflight Check", "사전 점검") },
            { "preflight.logger_ok", ("✓ EventLogger ready", "✓ 이벤트 로거 준비됨") },
            { "preflight.logger_fail", ("✗ EventLogger NOT ready", "✗ 이벤트 로거 미준비") },
            { "preflight.spatial_ok", ("✓ Spatial calibration active", "✓ 공간 보정 활성") },
            { "preflight.spatial_fail", ("✗ No spatial calibration", "✗ 공간 보정 없음") },
            { "preflight.condition_ok", ("✓ Condition applied: {0}", "✓ 조건 적용됨: {0}") },
            { "preflight.condition_fail", ("✗ Condition mismatch: expected {0}, got {1}", "✗ 조건 불일치: 예상 {0}, 현재 {1}") },
            { "preflight.beam_ok", ("✓ BeamPro tracker active", "✓ BeamPro 트래커 활성") },
            { "preflight.beam_fail", ("✗ BeamPro tracker missing", "✗ BeamPro 트래커 없음") },
            { "preflight.override_available", ("Override available in {0}s...", "{0}초 후 강제 진행 가능...") },

            // ===== GlassFlowUI Image Tracking =====
            { "glassflow.image_scan", ("Scan the marker...", "\ub9c8\ucee4\ub97c \uc2a4\uce94\ud558\uc138\uc694...") },
            { "glassflow.image_marker_found", ("\u2713 {0} found!", "\u2713 {0} \ubc1c\uacac!") },

            // ===== Post-Condition Survey (NASA-TLX + Trust) =====
            { "survey.section_header", ("{0} ({1}/{2})", "{0} ({1}/{2})") },
            { "survey.progress", ("{0} / {1}", "{0} / {1}") },
            { "survey.nasa_section", ("NASA-TLX", "NASA-TLX") },
            { "survey.trust_section", ("System Trust", "\uc2dc\uc2a4\ud15c \uc2e0\ub8b0") },
            { "survey.confirm", ("Confirm", "\ud655\uc778") },
            { "survey.rating_label", ("{0}/7", "{0}/7") },
            { "survey.low_label", ("Very Low", "\ub9e4\uc6b0 \ub0ae\uc74c") },
            { "survey.high_label", ("Very High", "\ub9e4\uc6b0 \ub192\uc74c") },

            // NASA-TLX 6 items
            { "survey.nasa_mental_demand", ("How mentally demanding was the navigation task?", "\ub0b4\ube44\uac8c\uc774\uc158 \uc791\uc5c5\uc740 \uc815\uc2e0\uc801\uc73c\ub85c \uc5bc\ub9c8\ub098 \ubd80\ub2f4\uc2a4\ub7ec\uc6e0\uc2b5\ub2c8\uae4c?") },
            { "survey.nasa_physical_demand", ("How physically demanding was the task?", "\uc791\uc5c5\uc740 \uc2e0\uccb4\uc801\uc73c\ub85c \uc5bc\ub9c8\ub098 \ubd80\ub2f4\uc2a4\ub7ec\uc6e0\uc2b5\ub2c8\uae4c?") },
            { "survey.nasa_temporal_demand", ("How hurried or rushed was the pace?", "\uc791\uc5c5 \uc18d\ub3c4\uac00 \uc5bc\ub9c8\ub098 \uae09\ud588\uc2b5\ub2c8\uae4c?") },
            { "survey.nasa_performance", ("How successful were you in accomplishing the task?", "\uc791\uc5c5\uc744 \uc5bc\ub9c8\ub098 \uc131\uacf5\uc801\uc73c\ub85c \uc218\ud589\ud588\uc2b5\ub2c8\uae4c?") },
            { "survey.nasa_effort", ("How hard did you have to work to accomplish the task?", "\uc791\uc5c5\uc744 \uc218\ud589\ud558\uae30 \uc704\ud574 \uc5bc\ub9c8\ub098 \ub9ce\uc740 \ub178\ub825\uc774 \ud544\uc694\ud588\uc2b5\ub2c8\uae4c?") },
            { "survey.nasa_frustration", ("How insecure, discouraged, or stressed were you?", "\uc5bc\ub9c8\ub098 \ubd88\uc548\ud558\uac70\ub098 \uc88c\uc808\uac10\uc744 \ub290\uaf08\uc2b5\ub2c8\uae4c?") },

            // Trust 7 items
            { "survey.trust_direction", ("The system provided correct directional guidance.", "\uc2dc\uc2a4\ud15c\uc774 \uc815\ud655\ud55c \ubc29\ud5a5 \uc548\ub0b4\ub97c \uc81c\uacf5\ud588\ub2e4.") },
            { "survey.trust_reliability", ("The system operated reliably without errors.", "\uc2dc\uc2a4\ud15c\uc774 \uc624\ub958 \uc5c6\uc774 \uc548\uc815\uc801\uc73c\ub85c \uc791\ub3d9\ud588\ub2e4.") },
            { "survey.trust_confidence", ("I felt confident following the system's guidance.", "\uc2dc\uc2a4\ud15c\uc758 \uc548\ub0b4\ub97c \ub530\ub97c \ub54c \ud655\uc2e0\uc774 \ub4e4\uc5c8\ub2e4.") },
            { "survey.trust_accuracy", ("The system accurately showed my location and destination.", "\uc2dc\uc2a4\ud15c\uc774 \ub0b4 \uc704\uce58\uc640 \ubaa9\uc801\uc9c0\ub97c \uc815\ud655\ud558\uac8c \ud45c\uc2dc\ud588\ub2e4.") },
            { "survey.trust_safety", ("I felt safe using the system for navigation.", "\uc2dc\uc2a4\ud15c\uc744 \uc0ac\uc6a9\ud558\uc5ec \ub0b4\ube44\uac8c\uc774\uc158\ud560 \ub54c \uc548\uc804\ud558\ub2e4\uace0 \ub290\uaf08\ub2e4.") },
            { "survey.trust_destination", ("I believed the system would lead me to the correct destination.", "\uc2dc\uc2a4\ud15c\uc774 \uc62c\ubc14\ub978 \ubaa9\uc801\uc9c0\ub85c \uc548\ub0b4\ud560 \uac83\uc774\ub77c \ubbff\uc5c8\ub2e4.") },
            { "survey.trust_reuse", ("I would be willing to use this system again.", "\uc774 \uc2dc\uc2a4\ud15c\uc744 \ub2e4\uc2dc \uc0ac\uc6a9\ud560 \uc758\ud5a5\uc774 \uc788\ub2e4.") },

            // ===== GlassFlowUI: Comparison Survey Guide =====
            { "glassflow.comparison_title", ("Comparison Survey", "\ube44\uad50 \uc124\ubb38") },
            { "glassflow.comparison_instr", ("Please complete the comparison survey\non the phone screen.", "Beam Pro \ud654\uba74\uc5d0\uc11c\n\ube44\uad50 \uc124\ubb38\uc744 \uc644\ub8cc\ud574 \uc8fc\uc138\uc694.") },

            // ===== ExperimentFlowUI: Comparison Survey =====
            { "flow.comparison_title", ("Comparison Survey", "\ube44\uad50 \uc124\ubb38") },
            { "flow.comparison_instr", ("Comparison survey in progress on Beam Pro.\nPlease wait for the participant to finish.", "Beam Pro\uc5d0\uc11c \ube44\uad50 \uc124\ubb38 \uc9c4\ud589 \uc911.\n\ucc38\uac00\uc790\uac00 \uc644\ub8cc\ud560 \ub54c\uae4c\uc9c0 \ub300\uae30\ud574 \uc8fc\uc138\uc694.") },

            // ===== Comparison Survey UI (BeamPro) =====
            { "comparison.title", ("Comparison Survey", "\ube44\uad50 \uc124\ubb38") },
            { "comparison.page", ("Page {0}/{1}", "\ud398\uc774\uc9c0 {0}/{1}") },
            { "comparison.preferred", ("Which condition did you prefer for navigation?", "\ub0b4\ube44\uac8c\uc774\uc158\uc5d0 \uc5b4\ub5a4 \uc870\uac74\uc744 \uc120\ud638\ud558\uc2ed\ub2c8\uae4c?") },
            { "comparison.glass_only", ("Glass Only", "\uae00\ub798\uc2a4 \uc804\uc6a9") },
            { "comparison.hybrid", ("Hybrid", "\ud558\uc774\ube0c\ub9ac\ub4dc") },
            { "comparison.no_difference", ("No Difference", "\ucc28\uc774 \uc5c6\uc74c") },
            { "comparison.trust_compare", ("In which condition did you feel more trust in the system?", "\uc5b4\ub5a4 \uc870\uac74\uc5d0\uc11c \uc2dc\uc2a4\ud15c\uc5d0 \ub300\ud55c \uc2e0\ub8b0\uac00 \ub354 \ub192\uc558\uc2b5\ub2c8\uae4c?") },
            { "comparison.glass_higher", ("Glass Only higher", "\uae00\ub798\uc2a4 \uc804\uc6a9\uc774 \ub354 \ub192\uc74c") },
            { "comparison.hybrid_higher", ("Hybrid higher", "\ud558\uc774\ube0c\ub9ac\ub4dc\uac00 \ub354 \ub192\uc74c") },
            { "comparison.same", ("Same", "\ub3d9\uc77c") },
            { "comparison.preference_reason", ("Why did you prefer that condition?", "\ud574\ub2f9 \uc870\uac74\uc744 \uc120\ud638\ud55c \uc774\uc720\ub294 \ubb34\uc5c7\uc785\ub2c8\uae4c?") },
            { "comparison.switching_behavior", ("When did you decide to look at the phone or switch devices?", "\uc5b8\uc81c \ud3f0\uc744 \ubcf4\uac70\ub098 \uae30\uae30\ub97c \uc804\ud658\ud558\uae30\ub85c \uacb0\uc815\ud588\uc2b5\ub2c8\uae4c?") },
            { "comparison.suggestions", ("Any suggestions for improvement?", "\uac1c\uc120\uc744 \uc704\ud55c \uc81c\uc548\uc774 \uc788\uc2b5\ub2c8\uae4c?") },
            { "comparison.text_placeholder", ("Enter your response...", "\uc751\ub2f5\uc744 \uc785\ub825\ud558\uc138\uc694...") },
            { "comparison.prev", ("Previous", "\uc774\uc804") },
            { "comparison.next", ("Next", "\ub2e4\uc74c") },
            { "comparison.submit", ("Submit", "\uc81c\ucd9c") },

            // ===== Hybrid Mission Overlay =====
            { "hybrid.confirm", ("Confirm", "\ud655\uc778") },
            { "hybrid.verify_title", ("Verification", "\uac80\uc99d") },
            { "hybrid.confidence_title", ("Confidence Rating", "\ud655\uc2e0\ub3c4 \ud3c9\uac00") },
            { "hybrid.difficulty_title", ("Difficulty Rating", "\ub09c\uc774\ub3c4 \ud3c9\uac00") },

            // ===== AppModeSelector: 2nd run =====
            { "appmode.condition_done", ("{0} (Done)", "{0} (\uc644\ub8cc)") },
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
