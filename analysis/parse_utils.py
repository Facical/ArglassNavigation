"""공통 extra_data 파싱 유틸리티 — eval() 대신 json.loads + ast.literal_eval 폴백."""

import json
import ast


def parse_extra(extra_str) -> dict:
    """extra_data 문자열을 딕셔너리로 안전하게 파싱.

    우선순위:
    1. json.loads — 표준 JSON (C# 측 출력)
    2. ast.literal_eval — Python repr 형태 (str({...}))
    3. 빈 dict — 파싱 실패 시
    """
    if not isinstance(extra_str, str) or not extra_str:
        return {}
    try:
        return json.loads(extra_str)
    except (json.JSONDecodeError, ValueError):
        pass
    try:
        return ast.literal_eval(extra_str)
    except (ValueError, SyntaxError):
        return {}
