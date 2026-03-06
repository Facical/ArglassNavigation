#!/bin/bash
# pull_glass_captures.sh — XREAL 글래스 녹화 파일을 디바이스에서 로컬로 전송
#
# 사용법:
#   ./tools/pull_glass_captures.sh              # 기본 디바이스
#   ./tools/pull_glass_captures.sh 192.168.0.5  # 특정 디바이스 (무선 adb)

set -euo pipefail

PACKAGE="com.KIT_HCI.ARNavExperiment"
REMOTE_DIR="/storage/emulated/0/Android/data/${PACKAGE}/files/glass_captures/"
LOCAL_DIR="debug_captures"

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

# 원격 파일 목록
echo ""
echo "=== 디바이스 캡처 파일 목록 ==="
FILE_LIST=$(adb $DEVICE_ARG shell "ls -la ${REMOTE_DIR} 2>/dev/null" || true)
if [ -z "$FILE_LIST" ] || echo "$FILE_LIST" | grep -q "No such file"; then
    echo "캡처 파일이 없습니다."
    exit 0
fi
echo "$FILE_LIST"

# 로컬 디렉토리 생성
mkdir -p "$LOCAL_DIR"

# 파일 전송
echo ""
echo "=== 파일 전송 중 ==="
adb $DEVICE_ARG pull "$REMOTE_DIR" "$LOCAL_DIR/"
echo ""
echo "전송 완료: ./${LOCAL_DIR}/"
ls -la "$LOCAL_DIR/"

# 디바이스 파일 삭제 여부
echo ""
read -p "디바이스에서 캡처 파일을 삭제하시겠습니까? [y/N] " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    adb $DEVICE_ARG shell "rm -rf ${REMOTE_DIR}*"
    echo "디바이스 캡처 파일 삭제 완료."
fi
