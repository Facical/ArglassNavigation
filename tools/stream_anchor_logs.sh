#!/bin/bash
# SpatialAnchorManager logcat 실시간 모니터링
# 사용법:
#   ./tools/stream_anchor_logs.sh              # USB 연결
#   ./tools/stream_anchor_logs.sh 192.168.0.5  # 무선 adb
#   ./tools/stream_anchor_logs.sh --all        # 반복 폴링 로그 포함
set -euo pipefail

SHOW_ALL=false
DEVICE_ARG=""

for arg in "$@"; do
    case "$arg" in
        --all) SHOW_ALL=true ;;
        *) DEVICE_ARG="-s $arg" ;;
    esac
done

command -v adb &>/dev/null || { echo "Error: adb 없음"; exit 1; }
adb $DEVICE_ARG get-state &>/dev/null || { echo "Error: 디바이스 미연결"; exit 1; }

echo "=== SpatialAnchorManager 로그 스트리밍 ==="
echo "모드: $([ "$SHOW_ALL" = true ] && echo '전체' || echo '핵심만 (--all로 전체)')"
echo "Ctrl+C로 종료"
echo "==========================================="

adb $DEVICE_ARG logcat -c 2>/dev/null || true

if [ "$SHOW_ALL" = true ]; then
    adb $DEVICE_ARG logcat -s Unity:V | grep --line-buffered "\[SpatialAnchorManager\]"
else
    adb $DEVICE_ARG logcat -s Unity:V | grep --line-buffered "\[SpatialAnchorManager\]" \
        | grep -v --line-buffered "Waiting .* SLAM:" \
        | grep -v --line-buffered "TryRemap .* at " \
        | grep -v --line-buffered "Background #"
fi
