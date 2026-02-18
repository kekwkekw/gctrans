import os
import json
import time
from deep_translator import GoogleTranslator

# ================= 설정 =================
# update.py가 다운로드한 원본 파일 경로
INPUT_FOLDER = 'GalTransl/sampleProject/gt_input'
# 번역된 파일이 저장될 캐시 경로 (merge.py가 여기서 가져감)
OUTPUT_FOLDER = 'GalTransl/sampleProject/transl_cache'

# 번역 설정
SOURCE_LANG = 'ja'  # 일본어 원문
TARGET_LANG = 'ko'  # 한국어
DELAY = 0.2         # API 호출 간격 (초)
# =======================================

def translate_text(text, translator):
    if not text:
        return ""
    try:
        # 줄바꿈 태그(<br>)가 있으면 잠시 치환 (구글 번역 오작동 방지)
        text = text.replace("<br>", "\n")
        translated = translator.translate(text)
        return translated.replace("\n", "<br>") # 다시 <br>로 복구
    except Exception as e:
        print(f"    [Error] 번역 실패: {text[:10]}... -> {e}")
        return text

def run_translation():
    # 폴더가 없으면 생성
    os.makedirs(OUTPUT_FOLDER, exist_ok=True)
    
    # 입력 폴더 확인
    if not os.path.exists(INPUT_FOLDER):
        print(f"알림: {INPUT_FOLDER} 폴더가 없습니다. 업데이트할 파일이 없는 것 같습니다.")
        return

    files = [f for f in os.listdir(INPUT_FOLDER) if f.endswith('.json')]
    total_files = len(files)
    
    if total_files == 0:
        print("알림: 번역할 새로운 파일이 없습니다.")
        return

    print(f"=== 번역 시작: 총 {total_files}개 파일 ===")
    translator = GoogleTranslator(source=SOURCE_LANG, target=TARGET_LANG)

    for idx, filename in enumerate(files):
        input_path = os.path.join(INPUT_FOLDER, filename)
        output_path = os.path.join(OUTPUT_FOLDER, filename)
        
        print(f"[{idx+1}/{total_files}] 처리 중: {filename}")
        
        try:
            with open(input_path, 'r', encoding='utf-8') as f:
                data = json.load(f)
            
            # 데이터 구조: [{"name": "이름", "message": "일본어 대사"}, ...]
            # 변환 목표: [{"pre_jp": "일본어", "post_zh_preview": "한국어"}, ...]
            # GalTransl의 캐시 포맷을 흉내내야 merge.py가 인식함
            
            translated_data = []
            
            for item in data:
                original_text = item.get('message', '')
                if not original_text:
                    continue
                
                # 번역 수행
                translated_text = translate_text(original_text, translator)
                
                # GalTransl 캐시 포맷으로 저장
                translated_data.append({
                    "pre_jp": original_text,
                    "post_zh_preview": translated_text,
                    # 아래 필드는 merge.py 호환성을 위해 더미 데이터로 채움
                    "index": 0,
                    "pre_zh": "",
                    "tokens": 0
                })
                
                time.sleep(DELAY)
            
            # 저장
            with open(output_path, 'w', encoding='utf-8') as f:
                json.dump(translated_data, f, ensure_ascii=False, indent=4)
                
        except Exception as e:
            print(f"  [Critical] 파일 처리 중 오류: {filename} - {e}")

    print("=== 번역 작업 완료 ===")

if __name__ == "__main__":
    run_translation()
