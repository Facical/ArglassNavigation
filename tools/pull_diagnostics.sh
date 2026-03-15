#!/bin/bash
# pull_diagnostics.sh — 앵커 진단 로그를 디바이스에서 로컬로 전송
#
# 사용법:
#   ./tools/pull_diagnostics.sh              # 기본 디바이스
#   ./tools/pull_diagnostics.sh 192.168.0.5  # 특정 디바이스 (무선 adb)
#
# 진단 로그 위치 (디바이스):
#   /storage/emulated/0/Android/data/com.KIT_HCI.ARNavExperiment/files/diagnostics/
#
# 이벤트 CSV + 진단 로그를 함께 가져옵니다.

set -euo pipefail

PACKAGE="com.KIT_HCI.ARNavExperiment"
DIAG_DIR="/storage/emulated/0/Android/data/${PACKAGE}/files/diagnostics/"
RAW_DIR="/storage/emulated/0/Android/data/${PACKAGE}/files/data/raw/"
CALIB_DIR="/storage/emulated/0/Android/data/${PACKAGE}/files/calibration/"
LOCAL_DIAG="data/diagnostics"
LOCAL_RAW="data/raw"
LOCAL_CALIB="data/calibration"

# 선택적 디바이스 시리얼
DEVICE_ARG=""
if [ -n "${1:-}" ]; then
    DEVICE_ARG="-s $1"
fi

# adb 확인
if ! command -v adb &> /dev/null; then
    echo "Error: adb를 찾을 수 없습니다. Android SDK를 설치하세요."
    exit 1
fi

# 디바이스 연결 확인
echo "=== 디바이스 연결 확인 ==="
if ! adb $DEVICE_ARG get-state &> /dev/null; then
    echo "Error: 디바이스가 연결되지 않았습니다."
    echo "  - USB 연결 또는 'adb connect <IP>:5555' 실행"
    exit 1
fi
echo "OK: $(adb $DEVICE_ARG get-serialno)"

# --- 진단 로그 전송 ---
echo ""
echo "=== 앵커 진단 로그 ==="
DIAG_LIST=$(adb $DEVICE_ARG shell "ls -la ${DIAG_DIR} 2>/dev/null" || true)
if [ -z "$DIAG_LIST" ] || echo "$DIAG_LIST" | grep -q "No such file"; then
    echo "진단 로그가 없습니다."
else
    echo "$DIAG_LIST"
    mkdir -p "$LOCAL_DIAG"
    echo ""
    echo "=== 진단 로그 전송 중 ==="
    adb $DEVICE_ARG pull "$DIAG_DIR" "$LOCAL_DIAG/"
    echo ""
    echo "진단 로그 전송 완료: ./${LOCAL_DIAG}/"

    # 최신 로그 내용 미리보기
    LATEST=$(ls -t "$LOCAL_DIAG"/*.log 2>/dev/null | head -1)
    if [ -n "$LATEST" ]; then
        echo ""
        echo "=== 최신 진단 로그 미리보기: $(basename "$LATEST") ==="
        cat "$LATEST"

        echo ""
        echo "=== 실패/경고 요약 ==="
        echo "--- 타임아웃 ---"
        grep -i "타임아웃\|TimedOut" "$LATEST" 2>/dev/null | head -20 || echo "(없음)"
        echo "--- 로드 실패 ---"
        grep -i "로드 실패\|LoadFailed" "$LATEST" 2>/dev/null | head -20 || echo "(없음)"
        echo "--- SLAM 상태 ---"
        grep -i "SLAM=" "$LATEST" 2>/dev/null | tail -5 || echo "(없음)"
        echo "--- 상태 전환 ---"
        grep -i "StateTransition" "$LATEST" 2>/dev/null | head -10 || echo "(없음)"
    fi
fi

# --- 이벤트 CSV 전송 ---
echo ""
echo "=== 이벤트 CSV 로그 ==="
RAW_LIST=$(adb $DEVICE_ARG shell "ls -la ${RAW_DIR} 2>/dev/null" || true)
if [ -z "$RAW_LIST" ] || echo "$RAW_LIST" | grep -q "No such file"; then
    echo "이벤트 CSV가 없습니다."
else
    echo "$RAW_LIST"
    mkdir -p "$LOCAL_RAW"
    echo ""
    echo "=== CSV 전송 중 ==="
    adb $DEVICE_ARG pull "$RAW_DIR" "$LOCAL_RAW/"
    echo ""
    echo "CSV 전송 완료: ./${LOCAL_RAW}/"

    # 최신 CSV 이벤트 요약
    LATEST_CSV=$(ls -t "$LOCAL_RAW"/*.csv 2>/dev/null | head -1)
    if [ -n "$LATEST_CSV" ]; then
        echo ""
        echo "=== 최신 CSV 이벤트 요약: $(basename "$LATEST_CSV") ==="
        echo "--- 이벤트 타입별 카운트 ---"
        tail -n +2 "$LATEST_CSV" | cut -d',' -f4 | sort | uniq -c | sort -rn
        echo ""
        echo "--- FALLBACK/FAIL/SNAPSHOT 관련 ---"
        grep -i "FALLBACK\|FAIL\|SNAPSHOT\|BINDING" "$LATEST_CSV" | head -10 || echo "(없음)"
    fi
fi

# --- 위치 보정 로그 전송 ---
echo ""
echo "=== 위치 보정 로그 (PositionCalibrationLog) ==="
CALIB_LIST=$(adb $DEVICE_ARG shell "ls -la ${CALIB_DIR} 2>/dev/null" || true)
if [ -z "$CALIB_LIST" ] || echo "$CALIB_LIST" | grep -q "No such file"; then
    echo "위치 보정 로그가 없습니다."
else
    echo "$CALIB_LIST"
    mkdir -p "$LOCAL_CALIB"
    echo ""
    echo "=== 보정 로그 전송 중 ==="
    adb $DEVICE_ARG pull "$CALIB_DIR" "$LOCAL_CALIB/"
    echo ""
    echo "보정 로그 전송 완료: ./${LOCAL_CALIB}/"

    # 최신 JSON 미리보기
    LATEST_CALIB=$(ls -t "$LOCAL_CALIB"/*.json 2>/dev/null | head -1)
    if [ -n "$LATEST_CALIB" ]; then
        echo ""
        echo "=== 최신 보정 로그: $(basename "$LATEST_CALIB") ==="
        cat "$LATEST_CALIB"
    fi
fi

echo ""
echo "=== 완료 ==="
echo "진단 로그: ./${LOCAL_DIAG}/"
echo "이벤트 CSV: ./${LOCAL_RAW}/"
echo "보정 로그: ./${LOCAL_CALIB}/"
