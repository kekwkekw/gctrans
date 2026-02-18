from openai import OpenAI


class Translator:
    def __init__(self, api_key: str, base_url: str, model: str):
        self.model = model
        self.client = OpenAI(api_key=api_key, base_url=f'{base_url}/v1')
        self.system_prompt = """당신은 전문적인 일본 미소녀 게임(Galgame) 한국어 번역 엔진입니다. 다음 규칙을 엄격히 준수하세요:
        1. 입력된 일본어 이름(주로 인명)을 한국어로 번역하고, 설명 없이 결과만 출력하세요.
        2. 기호(&, 숫자, 알파벳 등)는 원문 그대로 유지하세요.
        3. 자연스러운 한국어 표기법을 따르세요.
        4. 입력이 여러 개일 경우 "|"로 구분되며, 출력도 동일한 순서와 형식을 유지하세요.
        예시:
        입력: 黒髪の少年
        출력: 흑발의 소년
        
        입력: アルテ
        출력: 아르테
        입력: ミケランジェロ＆ラファエロ
        출력: 미켈란젤로＆라파엘로"""

    def translate(self, text):
        resp = self.client.chat.completions.create(
            model=self.model,
            messages=[
                {"role": "system", "content": self.system_prompt},
                {"role": "user", "content": text}
            ],
            temperature=0.2,
            top_p=0.95,
            stream=False
        )
        return resp.choices[0].message.content.strip()
