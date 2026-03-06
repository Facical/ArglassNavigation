#!/usr/bin/env python3
"""
지하1층 도면.jpeg에서 도면 영역만 추출하여 미니맵 배경 이미지로 변환.

사용법:
    python3 tools/extract_floorplan.py

출력: ARNavExperiment/Assets/Data/FloorPlan/KIT_B1F_FloorPlan.png
"""

import cv2
import numpy as np
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "지하1층 도면.jpeg"
DST = ROOT / "ARNavExperiment" / "Assets" / "Data" / "FloorPlan" / "KIT_B1F_FloorPlan.png"


def main():
    img = cv2.imread(str(SRC))
    if img is None:
        raise FileNotFoundError(f"소스 이미지를 찾을 수 없습니다: {SRC}")

    h, w = img.shape[:2]
    print(f"소스 이미지: {w}x{h}")

    # ── 1단계: 원근 보정 ──
    # 사진에서 도면의 흰 배경 영역 4 꼭짓점 (수동 지정, 5712x4284 기준)
    src_pts = np.array([
        [1600, 1150],   # top-left
        [4020, 1130],   # top-right
        [3990, 3420],   # bottom-right
        [1510, 3450],   # bottom-left
    ], dtype="float32")

    dst_w, dst_h = 1760, 1680
    dst_pts = np.array([
        [0, 0], [dst_w - 1, 0], [dst_w - 1, dst_h - 1], [0, dst_h - 1]
    ], dtype="float32")

    M = cv2.getPerspectiveTransform(src_pts, dst_pts)
    warped = cv2.warpPerspective(img, M, (dst_w, dst_h),
                                  borderMode=cv2.BORDER_CONSTANT,
                                  borderValue=(240, 240, 240))

    # ── 2단계: 밝기 균일화 ──
    gray = cv2.cvtColor(warped, cv2.COLOR_BGR2GRAY)
    bg_illum = cv2.GaussianBlur(gray, (201, 201), 60)
    ratio = 245.0 / np.clip(bg_illum.astype(np.float32), 1, 255)
    ratio = np.clip(ratio, 0.85, 1.6)

    result = warped.copy()
    for c in range(3):
        result[:, :, c] = np.clip(
            warped[:, :, c].astype(np.float32) * ratio, 0, 255
        ).astype(np.uint8)

    # ── 3단계: 벽선 강화 ──
    gray_result = cv2.cvtColor(result, cv2.COLOR_BGR2GRAY)
    gamma = 0.6
    lut = np.array([((i / 255.0) ** gamma) * 255 for i in range(256)],
                    dtype=np.uint8)
    dark_threshold = 180
    for c in range(3):
        channel = result[:, :, c].astype(np.float32)
        enhanced = lut[result[:, :, c]]
        blend = np.clip(
            (gray_result.astype(np.float32) - 80) / (dark_threshold - 80), 0, 1
        )
        result[:, :, c] = (
            enhanced.astype(np.float32) * (1 - blend) + channel * blend
        ).astype(np.uint8)

    # ── 4단계: 배경 얼룩 제거 ──
    gray_final = cv2.cvtColor(result, cv2.COLOR_BGR2GRAY)
    bright_mask = gray_final > 200
    for c in range(3):
        ch = result[:, :, c].astype(np.float32)
        ch[bright_mask] = np.clip(ch[bright_mask] * 1.05 + 5, 0, 250)
        result[:, :, c] = ch.astype(np.uint8)

    # ── 5단계: 파란 배경 잔여 제거 ──
    hsv = cv2.cvtColor(result, cv2.COLOR_BGR2HSV)
    blue_bg = cv2.inRange(hsv, (80, 15, 0), (140, 255, 180))
    kernel = np.ones((7, 7), np.uint8)
    blue_bg = cv2.dilate(blue_bg, kernel, iterations=1)
    blue_contours, _ = cv2.findContours(
        blue_bg, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE
    )
    blue_large = np.zeros_like(blue_bg)
    for c in blue_contours:
        if cv2.contourArea(c) > 500:
            cv2.drawContours(blue_large, [c], -1, 255, -1)
    result[blue_large > 0] = [240, 240, 240]

    # ── 6단계: 가장자리 페이드 ──
    margin = 25
    alpha_map = np.ones((dst_h, dst_w), dtype=np.float32)
    for i in range(margin):
        a = i / margin
        alpha_map[i, :] = np.minimum(alpha_map[i, :], a)
        alpha_map[dst_h - 1 - i, :] = np.minimum(alpha_map[dst_h - 1 - i, :], a)
        alpha_map[:, i] = np.minimum(alpha_map[:, i], a)
        alpha_map[:, dst_w - 1 - i] = np.minimum(alpha_map[:, dst_w - 1 - i], a)

    bg_val = 240.0
    for c in range(3):
        result[:, :, c] = (
            result[:, :, c].astype(np.float32) * alpha_map
            + bg_val * (1 - alpha_map)
        ).astype(np.uint8)

    # ── 7단계: 리사이즈 ──
    target_w = 1200
    scale = target_w / dst_w
    target_h = int(dst_h * scale)
    final = cv2.resize(result, (target_w, target_h), interpolation=cv2.INTER_AREA)

    # ── 8단계: inpainting으로 잔여 파란 영역 제거 ──
    hsv2 = cv2.cvtColor(final, cv2.COLOR_BGR2HSV)
    sat_mask = hsv2[:, :, 1] > 15
    val_mask = hsv2[:, :, 2] < 220
    hue_mask = (hsv2[:, :, 0] > 75) & (hsv2[:, :, 0] < 145)
    combined = (sat_mask & val_mask & hue_mask).astype(np.uint8) * 255
    contours, _ = cv2.findContours(
        combined, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE
    )
    inpaint_mask = np.zeros_like(combined)
    for c in contours:
        if cv2.contourArea(c) > 200:
            cv2.drawContours(inpaint_mask, [c], -1, 255, -1)
    inpaint_mask = cv2.dilate(inpaint_mask, np.ones((7, 7), np.uint8), iterations=2)
    final = cv2.inpaint(final, inpaint_mask, 15, cv2.INPAINT_TELEA)

    # ── 저장 ──
    DST.parent.mkdir(parents=True, exist_ok=True)
    cv2.imwrite(str(DST), final, [cv2.IMWRITE_PNG_COMPRESSION, 9])
    print(f"저장 완료: {DST}")
    print(f"출력 크기: {target_w}x{target_h}")


if __name__ == "__main__":
    main()
