#!/bin/bash
# Claude Code PreToolUse hook: Edit/Write 시 대상 파일의 도메인 컨텍스트를 stdout으로 출력
# .claude/settings.json에서 PreToolUse matcher로 호출됨

# stdin에서 JSON 입력 읽기
INPUT=$(cat)

# file_path 추출
FILE_PATH=$(echo "$INPUT" | grep -o '"file_path"[[:space:]]*:[[:space:]]*"[^"]*"' | head -1 | sed 's/.*"file_path"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/')

if [ -z "$FILE_PATH" ]; then
    exit 0
fi

# 프로젝트 루트
PROJECT_ROOT="/Users/macstudio_kang/Desktop/ARglasses"
SCRIPTS_DIR="$PROJECT_ROOT/ARNavExperiment/Assets/Scripts"

# 파일이 Scripts 디렉토리 내부인지 확인
case "$FILE_PATH" in
    *"Assets/Scripts/"*)
        ;;
    *)
        exit 0
        ;;
esac

# 상대 경로에서 도메인 폴더 추출
REL_PATH="${FILE_PATH#*Assets/Scripts/}"
DOMAIN_FOLDER=$(echo "$REL_PATH" | cut -d'/' -f1)

# 도메인 컨텍스트 파일 경로 결정
CONTEXT_FILE=""
case "$DOMAIN_FOLDER" in
    Domain)
        SUB_FOLDER=$(echo "$REL_PATH" | cut -d'/' -f2)
        CONTEXT_FILE="$SCRIPTS_DIR/Domain/$SUB_FOLDER/DOMAIN_CONTEXT.md"
        ;;
    Application)
        CONTEXT_FILE="$SCRIPTS_DIR/Application/DOMAIN_CONTEXT.md"
        ;;
    Core|Mission|Navigation|Logging|Utils)
        CONTEXT_FILE="$SCRIPTS_DIR/Domain/Experiment/DOMAIN_CONTEXT.md"
        ;;
    Presentation)
        SUB_FOLDER=$(echo "$REL_PATH" | cut -d'/' -f2)
        case "$SUB_FOLDER" in
            Glass|Experimenter)
                CONTEXT_FILE="$SCRIPTS_DIR/Domain/Experiment/DOMAIN_CONTEXT.md"
                ;;
            BeamPro)
                CONTEXT_FILE="$SCRIPTS_DIR/Domain/Experiment/DOMAIN_CONTEXT.md"
                ;;
            Mapping)
                CONTEXT_FILE="$SCRIPTS_DIR/Domain/SpatialMapping/DOMAIN_CONTEXT.md"
                ;;
        esac
        ;;
esac

# 컨텍스트 파일이 존재하면 출력
if [ -n "$CONTEXT_FILE" ] && [ -f "$CONTEXT_FILE" ]; then
    echo "--- Domain Context: $(basename $(dirname $CONTEXT_FILE)) ---"
    cat "$CONTEXT_FILE"
    echo "--- End Domain Context ---"
fi

exit 0
