#!/usr/bin/env python3
"""
distance_m 이상 현상 진단 스크립트
====================================
nav_trace CSV에서 distance_m이 사용자가 걸어도 줄지 않는 문제를 분석.

핵심 가설: NavigationTraceLogger가 SLAM 좌표(player)와 도면 좌표(fallback target)를
직접 Vector3.Distance로 비교하여, 좌표계 불일치로 거리가 항상 ~48m 이상.

검증:
  A) ARRIVAL_FORCED 시점의 SLAM 위치 추출
  B) SlamToFloorPlan 변환을 수동 재현하여 올바른 거리 계산
  C) heading_offset 시간 변화 추적
  D) distance_m 시계열 분석 (미션별)
  E) 강제 도착 시 변환된 위치 vs 도면 타겟 비교
"""

import csv
import math
import os
import sys
from datetime import datetime
from collections import defaultdict

BASE_DIR = "/Users/macstudio_kang/Desktop/ARglasses"
DATA_DIR = os.path.join(BASE_DIR, "data/raw/raw")
OUTPUT_DIR = os.path.join(BASE_DIR, "analysis/output")
OUTPUT_FILE = os.path.join(OUTPUT_DIR, "distance_diagnosis.txt")

# 분석 대상 세션
PRIMARY_SESSION = "P01_glass_only_Set1_20260314_214203"
NAV_TRACE_FILE = os.path.join(DATA_DIR, f"{PRIMARY_SESSION}_nav_trace.csv")
EVENT_FILE = os.path.join(DATA_DIR, f"{PRIMARY_SESSION}.csv")

# 알려진 ARRIVAL_FORCED 정보
FORCED_ARRIVALS = [
    {"mission": "A1", "time_str": "21:42:41", "wp": "WP02", "fallback": (36, 0, 33)},
    {"mission": "B1", "time_str": "21:43:11", "wp": "WP03", "fallback": (36, 0, 45)},
    {"mission": "A2", "time_str": "21:43:52", "wp": "WP05", "fallback": (36, 0, 66)},
    {"mission": "B2", "time_str": "21:44:14", "wp": "WP06", "fallback": (39, 0, 72)},
    {"mission": "C1", "time_str": "21:44:49", "wp": "WP07", "fallback": (36, 0, 48)},  # WP07 fallback = (36,0,57) actually
]

# Waypoint fallback coordinates (from WaypointDataGenerator)
WP_FALLBACKS = {
    "WP00": (36, 0, 24),
    "WP01": (36, 0, 24),
    "WP02": (36, 0, 33),
    "WP03": (36, 0, 45),
    "WP04": (36, 0, 57),
    "WP05": (36, 0, 66),
    "WP06": (39, 0, 72),
    "WP07": (36, 0, 57),  # will check from data
    "WP08": (36, 0, 48),
}

out_lines = []

def log(msg=""):
    out_lines.append(msg)
    print(msg)

def parse_ts(ts_str):
    """Parse timestamp string to datetime."""
    ts_str = ts_str.strip().lstrip('\ufeff')
    try:
        return datetime.strptime(ts_str, "%Y-%m-%dT%H:%M:%S.%f")
    except ValueError:
        return datetime.strptime(ts_str, "%Y-%m-%dT%H:%M:%S")

def load_nav_trace(filepath):
    """Load nav_trace CSV into list of dicts with parsed values."""
    rows = []
    with open(filepath, 'r', encoding='utf-8-sig') as f:
        reader = csv.DictReader(f)
        for row in reader:
            parsed = {
                'timestamp': parse_ts(row['timestamp']),
                'session_id': row['session_id'],
                'mission_id': row.get('mission_id', ''),
                'current_wp_index': int(row['current_wp_index']) if row['current_wp_index'] else -1,
                'current_wp_id': row.get('current_wp_id', ''),
                'target_wp_id': row.get('target_wp_id', ''),
                'player_x': float(row['player_x']),
                'player_y': float(row['player_y']),
                'player_z': float(row['player_z']),
                'target_x': float(row['target_x']),
                'target_y': float(row['target_y']),
                'target_z': float(row['target_z']),
                'distance_m': float(row['distance_m']),
                'speed_ms': float(row['speed_ms']),
                'anchor_bound': row['anchor_bound'].lower() == 'true',
                'is_fallback': row['is_fallback'].lower() == 'true',
                'has_map_calib': row['has_map_calib'].lower() == 'true',
                'calib_source': row.get('calib_source', 'none'),
                'heading_offset_deg': float(row['heading_offset_deg']),
                'arrow_visible': row.get('arrow_visible', 'false').lower() == 'true',
                'beam_on': row.get('beam_on', 'false').lower() == 'true',
                'trigger_id': row.get('trigger_id', ''),
            }
            rows.append(parsed)
    return rows

def load_events(filepath):
    """Load event CSV into list of dicts."""
    rows = []
    with open(filepath, 'r', encoding='utf-8-sig') as f:
        reader = csv.DictReader(f)
        for row in reader:
            parsed = {
                'timestamp': parse_ts(row['timestamp']),
                'event_type': row['event_type'],
                'waypoint_id': row.get('waypoint_id', ''),
                'mission_id': row.get('mission_id', ''),
                'extra_data': row.get('extra_data', ''),
            }
            rows.append(parsed)
    return rows

def find_closest_nav_row(nav_rows, target_ts):
    """Find the nav_trace row closest to the given timestamp."""
    min_diff = None
    closest = None
    for row in nav_rows:
        diff = abs((row['timestamp'] - target_ts).total_seconds())
        if min_diff is None or diff < min_diff:
            min_diff = diff
            closest = row
    return closest, min_diff

def slam_to_floor_plan(slam_x, slam_z, heading_offset_deg, calib_offset_x, calib_offset_z):
    """
    Reproduce Unity's SlamToFloorPlan:
      theta = -heading_offset * deg2rad
      cos/sin of theta
      rx = x*cos - z*sin
      rz = x*sin + z*cos
      result = (rx + offset_x, rz + offset_z)
    """
    theta = -heading_offset_deg * math.pi / 180.0
    cos_t = math.cos(theta)
    sin_t = math.sin(theta)
    rx = slam_x * cos_t - slam_z * sin_t
    rz = slam_x * sin_t + slam_z * cos_t
    return (rx + calib_offset_x, rz + calib_offset_z)

def compute_calib_offset(slam_x, slam_z, fallback_x, fallback_z, heading_offset_deg):
    """
    Reproduce Unity's zero-anchor calibration offset:
      theta = -heading * deg2rad
      rotated_slam = rotate(slam, theta)
      offset = fallback - rotated_slam
    """
    theta = -heading_offset_deg * math.pi / 180.0
    cos_t = math.cos(theta)
    sin_t = math.sin(theta)
    rot_x = slam_x * cos_t - slam_z * sin_t
    rot_z = slam_x * sin_t + slam_z * cos_t
    return (fallback_x - rot_x, fallback_z - rot_z)


def analyze_primary_session():
    log("=" * 80)
    log("DISTANCE DIAGNOSIS REPORT")
    log(f"Session: {PRIMARY_SESSION}")
    log("=" * 80)
    log()

    # --- Load data ---
    if not os.path.exists(NAV_TRACE_FILE):
        log(f"ERROR: Nav trace file not found: {NAV_TRACE_FILE}")
        return
    if not os.path.exists(EVENT_FILE):
        log(f"ERROR: Event file not found: {EVENT_FILE}")
        return

    nav_rows = load_nav_trace(NAV_TRACE_FILE)
    events = load_events(EVENT_FILE)
    log(f"Nav trace rows: {len(nav_rows)}")
    log(f"Event rows: {len(events)}")
    log()

    # --- Section 1: Basic data overview ---
    log("-" * 80)
    log("1. DATA OVERVIEW")
    log("-" * 80)

    first = nav_rows[0]
    last = nav_rows[-1]
    duration_s = (last['timestamp'] - first['timestamp']).total_seconds()
    log(f"Time range: {first['timestamp'].strftime('%H:%M:%S.%f')[:12]} → {last['timestamp'].strftime('%H:%M:%S.%f')[:12]} ({duration_s:.0f}s)")
    log(f"Sample rate: {len(nav_rows) / duration_s:.1f} Hz")
    log()

    # anchor/fallback status
    n_anchor = sum(1 for r in nav_rows if r['anchor_bound'])
    n_fallback = sum(1 for r in nav_rows if r['is_fallback'])
    log(f"anchor_bound=true:  {n_anchor}/{len(nav_rows)} ({100*n_anchor/len(nav_rows):.1f}%)")
    log(f"is_fallback=true:   {n_fallback}/{len(nav_rows)} ({100*n_fallback/len(nav_rows):.1f}%)")
    log()

    # calib_source transitions
    log("Calibration source transitions:")
    prev_source = None
    for r in nav_rows:
        if r['calib_source'] != prev_source:
            log(f"  {r['timestamp'].strftime('%H:%M:%S.%f')[:12]}  calib_source = {r['calib_source']:<15}  heading = {r['heading_offset_deg']:.1f}")
            prev_source = r['calib_source']
    log()

    # --- Section 2: THE BUG ---
    log("-" * 80)
    log("2. ROOT CAUSE ANALYSIS: COORDINATE SYSTEM MISMATCH")
    log("-" * 80)
    log()
    log("NavigationTraceLogger.cs line 86-87:")
    log("    Vector3 targetPos = currentWP.Position;")
    log("    float distance = Vector3.Distance(playerPos, targetPos);")
    log()
    log("When anchor_bound=false (fallback mode):")
    log("  - playerPos = HeadTracker.CurrentPosition = SLAM coordinates (camera space)")
    log("  - targetPos = Waypoint.Position = fallbackPosition = FLOOR PLAN coordinates")
    log("  - These are in DIFFERENT coordinate systems!")
    log()
    log("Meanwhile, WaypointManager.ComputeDistanceToWaypoint() correctly uses:")
    log("  - SlamToFloorPlan(playerXZ) to convert player to floor plan coords")
    log("  - Then Vector2.Distance(playerFloorPlan, wpXZ)")
    log()
    log("The logger bypasses this transform and compares raw SLAM vs raw floor plan.")
    log()

    # Show first few rows as evidence
    log("Evidence from first 3 rows:")
    for i, r in enumerate(nav_rows[:3]):
        log(f"  Row {i}: player=({r['player_x']:.2f}, {r['player_y']:.2f}, {r['player_z']:.2f}) [SLAM]")
        log(f"          target=({r['target_x']:.2f}, {r['target_y']:.2f}, {r['target_z']:.2f}) [FLOOR PLAN]")
        log(f"          distance_m={r['distance_m']:.2f}  (raw 3D distance across coordinate systems)")
    log()

    # --- Section 3: Heading offset evolution ---
    log("-" * 80)
    log("3. HEADING OFFSET EVOLUTION")
    log("-" * 80)
    log()

    heading_changes = []
    prev_heading = None
    for r in nav_rows:
        h = r['heading_offset_deg']
        if prev_heading is None or abs(h - prev_heading) > 0.05:
            heading_changes.append((r['timestamp'], h, r['calib_source']))
            prev_heading = h

    log(f"{'Time':<15} {'Heading (deg)':>15} {'Calib Source':<20} {'Delta':>10}")
    log(f"{'----':<15} {'-------------':>15} {'------------':<20} {'-----':>10}")
    for i, (ts, h, src) in enumerate(heading_changes):
        delta = ""
        if i > 0:
            d = h - heading_changes[i-1][1]
            # normalize to [-180, 180]
            while d > 180: d -= 360
            while d < -180: d += 360
            delta = f"{d:+.1f}"
        log(f"{ts.strftime('%H:%M:%S.%f')[:12]:15} {h:15.1f} {src:<20} {delta:>10}")

    log()
    log("NOTE: -248.7 and 102.2 differ by ~350.9 degrees.")
    log("  Normalized: -248.7 + 360 = 111.3, so the actual change is 111.3 → 102.2 = -9.1 degrees.")
    log("  This is consistent with improved calibration from more anchors.")
    normalized_1 = -248.7 % 360  # = 111.3
    diff_normalized = 102.2 - normalized_1
    log(f"  Normalized heading 1: {-248.7 % 360:.1f}°,  heading 2: 102.2°")
    log(f"  Effective rotation change: {diff_normalized:.1f}°")
    log()

    # --- Section 4: Reproduce SlamToFloorPlan transform ---
    log("-" * 80)
    log("4. SLAM → FLOOR PLAN TRANSFORM RECONSTRUCTION")
    log("-" * 80)
    log()
    log("Unity's SlamToFloorPlan(slamXZ):")
    log("  theta = -headingOffset * deg2rad")
    log("  rx = x*cos(theta) - z*sin(theta)")
    log("  rz = x*sin(theta) + z*cos(theta)")
    log("  result = (rx, rz) + mapCalibOffset")
    log()
    log("mapCalibOffset is computed at calibration time:")
    log("  offset = fallbackXZ(WP01) - rotate(cameraSlamXZ, theta)")
    log()

    # Find the first row to get initial SLAM position for zero-anchor calibration
    # The first nav_trace row gives us the camera position at calibration time
    first_row = nav_rows[0]
    initial_slam_x = first_row['player_x']
    initial_slam_z = first_row['player_z']
    initial_heading = first_row['heading_offset_deg']

    # WP01 fallback (= WP00 in this route, both at (36,0,24)... need to check)
    # From the data, current_wp_id = WP02 at start, so WP01 already reached
    # The calibration happened before nav_trace started
    # Let's use WP01 fallback = (36, 24) as the zero-anchor reference

    log("--- Initial calibration state (from first nav_trace row) ---")
    log(f"  SLAM position: ({initial_slam_x:.2f}, {initial_slam_z:.2f})")
    log(f"  heading_offset: {initial_heading:.1f}°")
    log(f"  calib_source: {first_row['calib_source']}")
    log()

    # Since calib_source is anchor_2 from the start, the offset was computed from
    # 2 anchors, not zero-anchor. We need to estimate the offset.
    # We can reverse-engineer it from the data:
    # For the calibration to be correct:
    #   SlamToFloorPlan(slam_at_wp) should ≈ fallback_of_wp
    # But we don't know which anchors were used. Let's try to compute
    # what the offset would be for each heading value.

    log("--- Reconstructing mapCalibOffset for each calibration phase ---")
    log()

    # Phase 1: anchor_2, heading=-248.7
    # Phase 2: anchor_3, heading=102.2
    # Phase 3: anchor_4, heading=102.2 (same heading, different # anchors)

    phases = []
    prev_src = None
    prev_head = None
    for r in nav_rows:
        key = (r['calib_source'], r['heading_offset_deg'])
        if key != (prev_src, prev_head):
            phases.append({
                'calib_source': r['calib_source'],
                'heading': r['heading_offset_deg'],
                'start_ts': r['timestamp'],
                'first_slam': (r['player_x'], r['player_z']),
                'first_target': (r['target_x'], r['target_z']),
                'first_wp': r['current_wp_id'],
            })
            prev_src, prev_head = key

    for i, phase in enumerate(phases):
        log(f"  Phase {i+1}: calib_source={phase['calib_source']}, heading={phase['heading']:.1f}°")
        log(f"    start: {phase['start_ts'].strftime('%H:%M:%S')}")
        log(f"    first SLAM: ({phase['first_slam'][0]:.2f}, {phase['first_slam'][1]:.2f})")
        log(f"    first target (fallback): ({phase['first_target'][0]:.2f}, {phase['first_target'][1]:.2f})")
        log(f"    current WP: {phase['first_wp']}")
        log()

    log()

    # --- Section 5: Distance analysis per mission ---
    log("-" * 80)
    log("5. DISTANCE_M OVER TIME (per mission)")
    log("-" * 80)
    log()

    missions = defaultdict(list)
    for r in nav_rows:
        mid = r['mission_id']
        if mid:
            missions[mid].append(r)

    for mid in ["A1", "B1", "A2", "B2", "C1"]:
        if mid not in missions:
            log(f"Mission {mid}: NO DATA")
            continue
        mrows = missions[mid]
        dists = [r['distance_m'] for r in mrows]
        log(f"Mission {mid} ({len(mrows)} samples, {(mrows[-1]['timestamp']-mrows[0]['timestamp']).total_seconds():.1f}s):")
        log(f"  target WP: {mrows[0]['current_wp_id']} (fallback: {mrows[0]['target_x']:.0f}, {mrows[0]['target_z']:.0f})")
        log(f"  distance range: {min(dists):.2f} → {max(dists):.2f} m  (mean={sum(dists)/len(dists):.2f})")
        log(f"  heading: {mrows[0]['heading_offset_deg']:.1f}°")
        log(f"  SLAM X range: {min(r['player_x'] for r in mrows):.2f} → {max(r['player_x'] for r in mrows):.2f}")
        log(f"  SLAM Z range: {min(r['player_z'] for r in mrows):.2f} → {max(r['player_z'] for r in mrows):.2f}")
        log(f"  First: player=({mrows[0]['player_x']:.2f}, {mrows[0]['player_z']:.2f}), dist={mrows[0]['distance_m']:.2f}")
        log(f"  Last:  player=({mrows[-1]['player_x']:.2f}, {mrows[-1]['player_z']:.2f}), dist={mrows[-1]['distance_m']:.2f}")
        log()

    # --- Section 6: ARRIVAL_FORCED analysis ---
    log("-" * 80)
    log("6. FORCED ARRIVAL ANALYSIS: SLAM vs FLOOR PLAN POSITIONS")
    log("-" * 80)
    log()

    # Extract ARRIVAL_FORCED events from event CSV
    arrival_events = [e for e in events if e['event_type'] == 'ARRIVAL_FORCED']
    log(f"Found {len(arrival_events)} ARRIVAL_FORCED events")
    log()

    for ae in arrival_events:
        ts = ae['timestamp']
        mid = ae['mission_id']
        closest, diff_s = find_closest_nav_row(nav_rows, ts)

        if closest is None:
            log(f"  {mid}: No matching nav_trace row!")
            continue

        slam_x = closest['player_x']
        slam_y = closest['player_y']
        slam_z = closest['player_z']
        target_x = closest['target_x']
        target_z = closest['target_z']
        heading = closest['heading_offset_deg']
        reported_dist = closest['distance_m']
        wp_id = closest['current_wp_id']

        log(f"Mission {mid} (ARRIVAL_FORCED at {ts.strftime('%H:%M:%S.%f')[:12]}):")
        log(f"  Closest nav_trace: {closest['timestamp'].strftime('%H:%M:%S.%f')[:12]} (delta={diff_s:.3f}s)")
        log(f"  Current WP: {wp_id}")
        log(f"  SLAM position:    ({slam_x:.2f}, {slam_y:.2f}, {slam_z:.2f})")
        log(f"  Target (fallback): ({target_x:.0f}, {target_z:.0f})")
        log(f"  Reported distance: {reported_dist:.2f} m  ← THIS IS WRONG (cross-coordinate-system)")
        log(f"  heading_offset: {heading:.1f}°")
        log(f"  calib_source: {closest['calib_source']}")
        log()

    # --- Section 7: What the CORRECT distance would be ---
    log("-" * 80)
    log("7. CORRECTED DISTANCE CALCULATION")
    log("-" * 80)
    log()
    log("To compute correct distance, we need mapCalibOffset.")
    log("We can estimate it by finding the SLAM position at WP01 arrival (WAYPOINT_REACHED WP01)")
    log("and using: offset = fallback(WP01) - rotate(slam(WP01), theta)")
    log()

    # Find WAYPOINT_REACHED WP01 event
    wp01_events = [e for e in events if e['event_type'] == 'WAYPOINT_REACHED' and e['waypoint_id'] == 'WP01']
    if wp01_events:
        wp01_ts = wp01_events[0]['timestamp']
        wp01_nav, wp01_diff = find_closest_nav_row(nav_rows, wp01_ts)
        if wp01_nav:
            log(f"WP01 reached at {wp01_ts.strftime('%H:%M:%S.%f')[:12]}")
            log(f"  Closest nav row: {wp01_nav['timestamp'].strftime('%H:%M:%S.%f')[:12]} (delta={wp01_diff:.3f}s)")
            log(f"  SLAM at WP01: ({wp01_nav['player_x']:.2f}, {wp01_nav['player_z']:.2f})")
            log(f"  heading at WP01: {wp01_nav['heading_offset_deg']:.1f}°")
    else:
        log("WP01 WAYPOINT_REACHED event not found; WP01 was likely reached before nav_trace started.")

    log()
    log("Alternative: We know the actual waypoint reaching works correctly in Unity")
    log("(ComputeDistanceToWaypoint uses SlamToFloorPlan). The ARRIVAL_FORCED means")
    log("the experimenter manually forced arrival because the automatic detection")
    log("may have also had issues, OR the calibration was wrong, OR the arrival")
    log("worked but distance display was just misleading.")
    log()

    # Let's try to reconstruct the offset from the first available data
    # For each calibration phase, estimate what mapCalibOffset must be
    # by assuming the user was actually near the target waypoint at forced arrival

    log("--- Estimated mapCalibOffset per calibration phase ---")
    log()

    for ae in arrival_events:
        ts = ae['timestamp']
        mid = ae['mission_id']
        closest, _ = find_closest_nav_row(nav_rows, ts)
        if closest is None:
            continue

        heading = closest['heading_offset_deg']
        slam_x = closest['player_x']
        slam_z = closest['player_z']
        target_x = closest['target_x']
        target_z = closest['target_z']

        # If user was actually AT the target, then:
        # SlamToFloorPlan(slam) should = (target_x, target_z)
        # (rx + off_x, rz + off_z) = (target_x, target_z)
        # off = target - rotate(slam, theta)

        offset = compute_calib_offset(slam_x, slam_z, target_x, target_z, heading)
        log(f"  {mid}: If user at ({target_x:.0f},{target_z:.0f}), offset would be ({offset[0]:.2f}, {offset[1]:.2f})")
        log(f"     heading={heading:.1f}°, SLAM=({slam_x:.2f}, {slam_z:.2f})")

        # Also compute what the correct distance would be with THIS offset
        floor_pos = slam_to_floor_plan(slam_x, slam_z, heading, offset[0], offset[1])
        corrected_dist = math.sqrt((floor_pos[0] - target_x)**2 + (floor_pos[1] - target_z)**2)
        log(f"     → converted floor plan pos: ({floor_pos[0]:.2f}, {floor_pos[1]:.2f})")
        log(f"     → corrected distance: {corrected_dist:.4f} m (should be ~0 if user was at target)")
        log()

    # --- Section 8: Cross-validate with multiple offsets ---
    log("-" * 80)
    log("8. CROSS-VALIDATION: USE OFFSET FROM A1 TO PREDICT OTHER MISSIONS")
    log("-" * 80)
    log()

    # Use A1's forced arrival to establish the offset, then see if it predicts others
    # But we need to handle the heading change between phases

    # Group by calibration phase
    phase_arrivals = defaultdict(list)
    for ae in arrival_events:
        closest, _ = find_closest_nav_row(nav_rows, ae['timestamp'])
        if closest:
            key = (closest['calib_source'], closest['heading_offset_deg'])
            phase_arrivals[key].append((ae, closest))

    for (src, heading), arrivals in phase_arrivals.items():
        log(f"Calibration phase: {src}, heading={heading:.1f}°")
        log(f"  Arrivals in this phase: {len(arrivals)}")

        if len(arrivals) < 2:
            # Use the single arrival to estimate offset
            ae, nav = arrivals[0]
            mid = ae['mission_id']
            offset = compute_calib_offset(nav['player_x'], nav['player_z'],
                                           nav['target_x'], nav['target_z'], heading)
            log(f"  Using {mid} to estimate offset: ({offset[0]:.2f}, {offset[1]:.2f})")
            log()
            continue

        # Use first arrival to calibrate, test on rest
        first_ae, first_nav = arrivals[0]
        first_mid = first_ae['mission_id']
        offset = compute_calib_offset(first_nav['player_x'], first_nav['player_z'],
                                       first_nav['target_x'], first_nav['target_z'], heading)
        log(f"  Reference: {first_mid} → offset = ({offset[0]:.2f}, {offset[1]:.2f})")

        for ae, nav in arrivals[1:]:
            mid = ae['mission_id']
            floor_pos = slam_to_floor_plan(nav['player_x'], nav['player_z'],
                                            heading, offset[0], offset[1])
            target = (nav['target_x'], nav['target_z'])
            error = math.sqrt((floor_pos[0] - target[0])**2 + (floor_pos[1] - target[1])**2)
            log(f"  Predict {mid}: floor=({floor_pos[0]:.1f}, {floor_pos[1]:.1f}) vs target=({target[0]:.0f}, {target[1]:.0f}) → error={error:.1f}m")

        log()

    # --- Section 9: Detailed SLAM trajectory ---
    log("-" * 80)
    log("9. SLAM TRAJECTORY SUMMARY (position drift check)")
    log("-" * 80)
    log()

    # Sample every ~5 seconds
    sample_interval = 5.0
    last_sample_ts = None
    log(f"{'Time':<13} {'SLAM X':>8} {'SLAM Z':>8} {'Speed':>7} {'WP':>5} {'Mission':>8} {'Dist(wrong)':>12} {'Heading':>8} {'Calib':>10}")
    log(f"{'----':<13} {'------':>8} {'------':>8} {'-----':>7} {'--':>5} {'-------':>8} {'-----------':>12} {'-------':>8} {'-----':>10}")

    for r in nav_rows:
        ts = r['timestamp']
        if last_sample_ts is None or (ts - last_sample_ts).total_seconds() >= sample_interval:
            log(f"{ts.strftime('%H:%M:%S.%f')[:12]:<13} {r['player_x']:8.2f} {r['player_z']:8.2f} {r['speed_ms']:7.2f} {r['current_wp_id']:>5} {r['mission_id']:>8} {r['distance_m']:12.2f} {r['heading_offset_deg']:8.1f} {r['calib_source']:>10}")
            last_sample_ts = ts

    log()

    # --- Section 10: The fix ---
    log("-" * 80)
    log("10. RECOMMENDED FIX")
    log("-" * 80)
    log()
    log("NavigationTraceLogger.cs currently (line 82-87):")
    log("    var currentWP = wm.CurrentWaypoint;")
    log("    ...")
    log("    Vector3 targetPos = currentWP.Position;")
    log("    float distance = Vector3.Distance(playerPos, targetPos);")
    log()
    log("This should be replaced with something like:")
    log()
    log("    float distance;")
    log("    Vector3 targetPos;")
    log("    if (currentWP.anchorTransform != null)")
    log("    {")
    log("        // Both in SLAM space — direct distance OK")
    log("        targetPos = currentWP.anchorTransform.position;")
    log("        distance = Vector3.Distance(")
    log("            new Vector3(playerPos.x, 0, playerPos.z),")
    log("            new Vector3(targetPos.x, 0, targetPos.z));")
    log("    }")
    log("    else if (wm.HasMapCalibration)")
    log("    {")
    log("        // Convert player SLAM → floor plan, compare with fallback")
    log("        targetPos = currentWP.fallbackPosition;")
    log("        var playerFloorPlan = wm.SlamToFloorPlan(playerXZ); // needs public access")
    log("        distance = Vector2.Distance(playerFloorPlan,")
    log("            new Vector2(targetPos.x, targetPos.z));")
    log("    }")
    log("    else")
    log("    {")
    log("        targetPos = currentWP.Position;")
    log("        distance = -1; // unknown")
    log("    }")
    log()
    log("OR: Add a public method to WaypointManager that returns the correctly")
    log("computed distance, and call it from the logger:")
    log("    float distance = wm.GetDistanceToCurrentWaypoint();")
    log()
    log("Note: WaypointManager.ComputeDistanceToWaypoint() already does this correctly")
    log("but is private. Expose it or add a public wrapper.")
    log()

    # --- Section 11: Impact assessment ---
    log("-" * 80)
    log("11. IMPACT ASSESSMENT")
    log("-" * 80)
    log()
    log("The distance_m column in nav_trace CSV is UNRELIABLE for ALL fallback sessions.")
    log("This affects:")
    log("  - Any analysis using distance_m from nav_trace when anchor_bound=false")
    log("  - Speed derivations that use distance (speed_ms is OK — computed from playerPos delta)")
    log("  - Trajectory analysis that uses distance_m as proximity metric")
    log()
    log("NOT affected:")
    log("  - Actual waypoint arrival detection (uses ComputeDistanceToWaypoint internally)")
    log("  - Arrow rendering (uses correct SlamToFloorPlan)")
    log("  - The experiment flow itself (missions, triggers, etc.)")
    log("  - SLAM trajectory (player_x/y/z is correct raw SLAM)")
    log("  - speed_ms (computed from SLAM position deltas)")
    log()
    log("For post-hoc analysis: ignore distance_m column, recompute from SLAM")
    log("coordinates using the heading_offset_deg and estimated mapCalibOffset.")
    log()


def analyze_all_sessions():
    log("-" * 80)
    log("12. MULTI-SESSION OVERVIEW")
    log("-" * 80)
    log()

    # Find all nav_trace files
    nav_files = sorted([f for f in os.listdir(DATA_DIR) if f.endswith('_nav_trace.csv')])
    log(f"Found {len(nav_files)} nav_trace files:")
    log()

    log(f"{'File':<60} {'Rows':>6} {'Duration':>10} {'Anchor%':>8} {'Calib Sources':>30} {'Dist Range':>15}")
    log(f"{'----':<60} {'----':>6} {'--------':>10} {'-------':>8} {'-------------':>30} {'----------':>15}")

    for nf in nav_files:
        filepath = os.path.join(DATA_DIR, nf)
        try:
            rows = load_nav_trace(filepath)
            if not rows:
                log(f"{nf:<60} EMPTY")
                continue

            n_rows = len(rows)
            duration = (rows[-1]['timestamp'] - rows[0]['timestamp']).total_seconds()
            n_anchor = sum(1 for r in rows if r['anchor_bound'])
            pct_anchor = 100 * n_anchor / n_rows if n_rows else 0
            sources = sorted(set(r['calib_source'] for r in rows))
            dists = [r['distance_m'] for r in rows]
            dist_range = f"{min(dists):.0f}-{max(dists):.0f}"

            log(f"{nf:<60} {n_rows:6} {duration:8.0f}s {pct_anchor:7.0f}% {','.join(sources):>30} {dist_range:>15}")
        except Exception as e:
            log(f"{nf:<60} ERROR: {e}")

    log()

    # Check if ANY session has anchor_bound=true
    any_anchor = False
    for nf in nav_files:
        filepath = os.path.join(DATA_DIR, nf)
        try:
            rows = load_nav_trace(filepath)
            if any(r['anchor_bound'] for r in rows):
                any_anchor = True
                log(f"  *** {nf} has anchor_bound=true rows! ***")
        except:
            pass

    if not any_anchor:
        log("  No sessions have any anchor_bound=true rows.")
        log("  → ALL sessions affected by the distance_m coordinate mismatch bug.")
    log()

    # Check distance pattern across sessions
    log("Distance pattern per session (is distance_m always large?):")
    for nf in nav_files:
        filepath = os.path.join(DATA_DIR, nf)
        try:
            rows = load_nav_trace(filepath)
            if not rows:
                continue
            dists = [r['distance_m'] for r in rows]
            mean_d = sum(dists) / len(dists)
            any_small = any(d < 5 for d in dists)
            log(f"  {nf[:50]:<52}  mean={mean_d:.1f}m  min={min(dists):.1f}m  any<5m: {any_small}")
        except:
            pass
    log()


def analyze_slam_trajectory_consistency():
    """Check if SLAM trajectory makes physical sense (speed, total displacement)."""
    log("-" * 80)
    log("13. SLAM TRAJECTORY PHYSICAL CONSISTENCY CHECK")
    log("-" * 80)
    log()

    nav_rows = load_nav_trace(NAV_TRACE_FILE)

    # Compute total SLAM displacement
    total_disp = 0
    for i in range(1, len(nav_rows)):
        dx = nav_rows[i]['player_x'] - nav_rows[i-1]['player_x']
        dz = nav_rows[i]['player_z'] - nav_rows[i-1]['player_z']
        total_disp += math.sqrt(dx*dx + dz*dz)

    duration = (nav_rows[-1]['timestamp'] - nav_rows[0]['timestamp']).total_seconds()
    avg_speed = total_disp / duration if duration > 0 else 0

    start_pos = (nav_rows[0]['player_x'], nav_rows[0]['player_z'])
    end_pos = (nav_rows[-1]['player_x'], nav_rows[-1]['player_z'])
    straight_line = math.sqrt((end_pos[0]-start_pos[0])**2 + (end_pos[1]-start_pos[1])**2)

    log(f"SLAM trajectory stats:")
    log(f"  Total path distance: {total_disp:.2f} m (SLAM units)")
    log(f"  Duration: {duration:.1f} s")
    log(f"  Average speed: {avg_speed:.2f} m/s")
    log(f"  Start pos: ({start_pos[0]:.2f}, {start_pos[1]:.2f})")
    log(f"  End pos: ({end_pos[0]:.2f}, {end_pos[1]:.2f})")
    log(f"  Straight-line displacement: {straight_line:.2f} m")
    log()

    # Check if SLAM distance is plausible for indoor walking
    # Typical walking speed: 1.0-1.5 m/s
    # Session is ~3 minutes, route is maybe 50-100m
    log(f"Physical plausibility:")
    log(f"  Walking speed 1.2 m/s × {duration:.0f}s = {1.2 * duration:.0f}m expected path")
    log(f"  Actual SLAM path: {total_disp:.0f}m")
    if total_disp < 5:
        log(f"  *** VERY SHORT! SLAM barely moved. Possible SLAM tracking issue.")
    elif total_disp < 20:
        log(f"  *** SHORT for a building-scale route. May indicate limited movement or short session.")
    elif total_disp > 500:
        log(f"  *** VERY LONG! Possible SLAM drift or coordinate jump.")
    else:
        log(f"  Looks reasonable for indoor navigation.")
    log()

    # Speed histogram (text-based)
    speeds = [nav_rows[i]['speed_ms'] for i in range(len(nav_rows))]
    speed_bins = [0, 0.1, 0.3, 0.5, 1.0, 1.5, 2.0, 5.0, 100]
    log("Speed distribution:")
    for i in range(len(speed_bins)-1):
        lo, hi = speed_bins[i], speed_bins[i+1]
        count = sum(1 for s in speeds if lo <= s < hi)
        bar = "#" * (count * 50 // len(speeds)) if speeds else ""
        log(f"  {lo:5.1f}-{hi:5.1f} m/s: {count:4d} ({100*count/len(speeds):5.1f}%) {bar}")
    log()

    # Check for position jumps (SLAM resets)
    log("Position jumps (>2m between consecutive samples):")
    jump_count = 0
    for i in range(1, len(nav_rows)):
        dx = nav_rows[i]['player_x'] - nav_rows[i-1]['player_x']
        dz = nav_rows[i]['player_z'] - nav_rows[i-1]['player_z']
        jump = math.sqrt(dx*dx + dz*dz)
        if jump > 2.0:
            jump_count += 1
            log(f"  {nav_rows[i]['timestamp'].strftime('%H:%M:%S.%f')[:12]}: jump={jump:.2f}m "
                f"({nav_rows[i-1]['player_x']:.2f},{nav_rows[i-1]['player_z']:.2f}) → "
                f"({nav_rows[i]['player_x']:.2f},{nav_rows[i]['player_z']:.2f})")
    if jump_count == 0:
        log("  No large jumps detected — SLAM tracking appears stable.")
    log()


def analyze_waypoint_index_issue(nav_rows, events):
    """Check if current_wp_index stays stuck and doesn't advance with missions."""
    log("-" * 80)
    log("14. WAYPOINT INDEX ANALYSIS: current_wp_id vs mission target")
    log("-" * 80)
    log()

    # Check what current_wp_id is during each mission
    missions = defaultdict(list)
    for r in nav_rows:
        if r['mission_id']:
            missions[r['mission_id']].append(r)

    log("Expected: current_wp_id should match the mission's target WP")
    log("  A1 → WP02, B1 → WP03, A2 → WP05, B2 → WP06, C1 → WP07")
    log()

    expected_wp = {"A1": "WP02", "B1": "WP03", "A2": "WP05", "B2": "WP06", "C1": "WP07"}

    for mid in ["A1", "B1", "A2", "B2", "C1"]:
        if mid not in missions:
            continue
        mrows = missions[mid]
        wps = set(r['current_wp_id'] for r in mrows)
        target_wps = set(r['target_wp_id'] for r in mrows if r['target_wp_id'])
        log(f"  Mission {mid}: current_wp_id={wps}, target_wp_id={target_wps or '{empty}'}")
        exp = expected_wp.get(mid, "?")
        if exp not in wps:
            log(f"    *** ISSUE: Expected {exp} but got {wps} — WP index not advancing!")
        else:
            log(f"    OK: Contains expected {exp}")

    log()

    # Check unique WP progression through entire session
    wp_transitions = []
    prev_wp = None
    for r in nav_rows:
        if r['current_wp_id'] != prev_wp:
            wp_transitions.append((r['timestamp'].strftime('%H:%M:%S'), r['current_wp_id'], r['mission_id']))
            prev_wp = r['current_wp_id']

    log("Waypoint transitions through session:")
    for ts, wp, mid in wp_transitions:
        log(f"  {ts}: {wp} (mission={mid or 'none'})")
    log()

    if len(wp_transitions) <= 3:
        log("*** SECONDARY ISSUE: current_wp_index barely advances!")
        log("   This means the NavigationTraceLogger's current_wp_id does NOT reflect")
        log("   the mission target WP. WaypointManager.CurrentWaypointIndex may not")
        log("   update when missions advance, or ARRIVAL_FORCED doesn't trigger index advance.")
        log("   As a result, both current_wp_id AND target coordinates in the nav_trace")
        log("   point to WP02 for ALL missions — the target never changes in the log.")
    log()


def main():
    log("=" * 80)
    log(f"Distance Diagnosis Report — Generated {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    log("=" * 80)
    log()

    analyze_primary_session()
    analyze_all_sessions()
    analyze_slam_trajectory_consistency()

    # --- Section 14: Waypoint index sticking analysis ---
    nav_rows_primary = load_nav_trace(NAV_TRACE_FILE) if os.path.exists(NAV_TRACE_FILE) else []
    events_primary = load_events(EVENT_FILE) if os.path.exists(EVENT_FILE) else []
    if nav_rows_primary and events_primary:
        analyze_waypoint_index_issue(nav_rows_primary, events_primary)

    # --- Final summary ---
    log("=" * 80)
    log("FINAL SUMMARY")
    log("=" * 80)
    log()
    log("ROOT CAUSE: NavigationTraceLogger computes distance as")
    log("  Vector3.Distance(playerPos_SLAM, targetPos_FLOORPLAN)")
    log("mixing SLAM coordinates (~0-30m range) with floor plan coordinates (~24-72m range).")
    log()
    log("The WaypointManager.ComputeDistanceToWaypoint() method correctly converts")
    log("SLAM→FloorPlan using SlamToFloorPlan() before computing distance, but the")
    log("logger bypasses this and uses the raw Waypoint.Position property which returns")
    log("fallbackPosition (floor plan coords) when anchor_bound=false.")
    log()
    log("CONSEQUENCE: distance_m in nav_trace CSV is meaningless for all fallback sessions.")
    log("The actual distance could be 1-3m at forced arrival but is reported as 40-65m.")
    log()
    log("FIX: Replace the logger's distance calculation with a call to")
    log("WaypointManager's coordinate-system-aware distance computation,")
    log("or make SlamToFloorPlan public and use it in the logger.")
    log()

    # Write output file
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    with open(OUTPUT_FILE, 'w', encoding='utf-8') as f:
        f.write('\n'.join(out_lines))
    log(f"Report saved to: {OUTPUT_FILE}")


if __name__ == '__main__':
    main()
