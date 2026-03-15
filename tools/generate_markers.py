#!/usr/bin/env python3
"""
AR Navigation 실험용 ArUco 마커 이미지 생성기.
A4 크기(210x297mm) PNG 파일을 생성합니다.

사용법:
    python3 tools/generate_markers.py

출력:
    ARNavExperiment/Assets/Data/ImageTracking/MARKER_WP01.png
    ARNavExperiment/Assets/Data/ImageTracking/MARKER_B101E.png
    ARNavExperiment/Assets/Data/ImageTracking/MARKER_WP07.png
    tools/print_markers.pdf  (인쇄용 통합 PDF)
"""

import cv2
import numpy as np
import os

# ArUco 딕셔너리: 4x4_50 (작은 마커 → 먼 거리에서도 인식 용이)
ARUCO_DICT = cv2.aruco.DICT_4X4_50

# 마커 정의: (마커 ID, 파일명, 레이블)
MARKERS = [
    (1, "MARKER_WP01", "WP01 - B111 Start"),
    (4, "MARKER_B101E", "B101E - U-turn East Wall"),
    (7, "MARKER_WP07", "WP07 - B104/B105 Return"),
]

# A4 크기 (300 DPI 기준: 210mm x 297mm)
DPI = 300
A4_WIDTH_PX = int(210 / 25.4 * DPI)   # 2480
A4_HEIGHT_PX = int(297 / 25.4 * DPI)  # 3508

# 마커 크기: A4 폭의 60% (약 126mm → 충분한 인식 거리)
MARKER_SIZE_PX = int(A4_WIDTH_PX * 0.6)

OUTPUT_DIR = os.path.join(os.path.dirname(__file__), "..",
                          "ARNavExperiment", "Assets", "Data", "ImageTracking")


def generate_marker(marker_id: int, filename: str, label: str):
    """ArUco 마커를 A4 크기 이미지로 생성합니다."""
    aruco_dict = cv2.aruco.getPredefinedDictionary(ARUCO_DICT)
    marker_img = cv2.aruco.generateImageMarker(aruco_dict, marker_id, MARKER_SIZE_PX)

    # A4 흰색 캔버스
    canvas = np.ones((A4_HEIGHT_PX, A4_WIDTH_PX), dtype=np.uint8) * 255

    # 마커를 중앙 상단에 배치
    x_offset = (A4_WIDTH_PX - MARKER_SIZE_PX) // 2
    y_offset = int(A4_HEIGHT_PX * 0.15)
    canvas[y_offset:y_offset + MARKER_SIZE_PX,
           x_offset:x_offset + MARKER_SIZE_PX] = marker_img

    # 레이블 텍스트 추가 (마커 아래)
    canvas_color = cv2.cvtColor(canvas, cv2.COLOR_GRAY2BGR)
    text_y = y_offset + MARKER_SIZE_PX + int(A4_HEIGHT_PX * 0.05)
    cv2.putText(canvas_color, f"ID: {marker_id}  |  {label}",
                (x_offset, text_y),
                cv2.FONT_HERSHEY_SIMPLEX, 2.0, (0, 0, 0), 4)

    # 하단에 "ARNav Experiment" + 파일명
    cv2.putText(canvas_color, f"ARNav Experiment  -  {filename}",
                (x_offset, text_y + int(A4_HEIGHT_PX * 0.04)),
                cv2.FONT_HERSHEY_SIMPLEX, 1.5, (128, 128, 128), 3)

    # 마커 둘레 검은 테두리 (인식 안정성 향상)
    border = 20
    cv2.rectangle(canvas_color,
                  (x_offset - border, y_offset - border),
                  (x_offset + MARKER_SIZE_PX + border,
                   y_offset + MARKER_SIZE_PX + border),
                  (0, 0, 0), border)

    # PNG 저장
    output_path = os.path.join(OUTPUT_DIR, f"{filename}.png")
    cv2.imwrite(output_path, canvas_color)
    print(f"  Generated: {output_path} (ID={marker_id}, {MARKER_SIZE_PX}x{MARKER_SIZE_PX}px)")
    return output_path


def main():
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    print(f"Generating {len(MARKERS)} ArUco markers (DICT_4X4_50)...")
    print(f"Output: {OUTPUT_DIR}")
    print(f"A4 size: {A4_WIDTH_PX}x{A4_HEIGHT_PX}px @ {DPI}DPI")
    print(f"Marker size: {MARKER_SIZE_PX}px ({MARKER_SIZE_PX / DPI * 25.4:.0f}mm)")
    print()

    paths = []
    for marker_id, filename, label in MARKERS:
        path = generate_marker(marker_id, filename, label)
        paths.append(path)

    print(f"\nDone! {len(paths)} markers generated.")
    print("\n다음 단계:")
    print("1. 생성된 PNG 파일을 A4로 인쇄")
    print("2. Unity 에디터에서 ARNav > Setup Image Tracking 실행")
    print("3. Assets > Create > XR > Reference Image Library 생성")
    print("4. 라이브러리에 마커 이미지 등록 (Physical Size = 0.126m)")
    print("5. ImageTrackingAligner에 라이브러리 + MarkerMappingData 연결")
    print("6. ExperimentManager Inspector에서 useImageTracking = true")


if __name__ == "__main__":
    main()
