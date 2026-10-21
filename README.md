# Windows AI Dog

바탕화면 위를 돌아다니는 토이푸들 데스크톱 펫입니다. 화면 아래쪽을 걷고 뛰다가 앉거나 엎드리고, 사용자를 바라보며 꼬리를 흔듭니다. 원하면 AI와 연결해 대화할 수도 있습니다.

A toy-poodle desktop pet for Windows. It walks and runs along your screen, sits, lies down and wags its tail while looking at you, and can optionally chat through an AI provider you connect yourself. (English summary at the end.)

## 기능

- 8가지 동작: 꼬리 흔들기, 걷기, 달리기(멈출 때 감속 장면), 점프, 간식 먹기, 고개 갸웃, 앉기, 엎드리기
- 클릭하면 쓰다듬기, 드래그로 이동, 오른쪽 클릭 메뉴에서 간식·동작 선택·집중 모드·음소거·설정
- 강아지 머리 위 말풍선과 강아지 옆 대화 입력창
- 짖는 소리·핥는 소리(CC0 녹음), 음량·음소거·소리 간격 설정
- 화면 잠금·전체 화면·숨김 상태에서는 소리와 움직임을 멈춤
- 트레이 아이콘에서 대화·설정·숨기기/보이기·화면 안으로 가져오기·종료

## 요구 사항

- Windows 10/11 64비트(x64)
- 직접 빌드할 때: .NET SDK 10.0.401(`global.json`)

## 실행

```bash
dotnet publish src/WindowsAiDog.Windows -c Release -r win-x64 --self-contained true -o out
```

`out` 폴더 전체를 그대로 두고 `out/WindowsAiDog.Windows.exe`를 실행합니다. 런타임이 함께 들어 있어 다른 PC에 폴더째 복사해도 동작합니다. 코드 서명이 없어서 처음 실행할 때 Windows가 경고를 띄울 수 있습니다("추가 정보 → 실행").

## AI 대화 (선택)

AI를 연결하지 않아도 강아지 동작·교감·소리는 모두 로컬에서 동작합니다. 대화를 쓰려면 오른쪽 클릭 → 설정 → AI · 연결 · 모델에서 각자 연결합니다.

- ChatGPT 구독: 브라우저 로그인(OAuth + PKCE)으로 본인 계정 연결
- OpenAI API / OpenAI 호환 API: 본인 API 키(별도 과금)
- 로컬 OpenAI 호환 서버: 직접 실행한 서버 주소

대기 중에는 AI를 호출하지 않으며, 유료 API로 자동 전환하지 않습니다. 대화는 메모리에 최근 몇 턴만 두고 창을 닫으면 잊습니다.

## 개인 정보

설정과 연결 정보는 각 PC의 `%LOCALAPPDATA%\WindowsAiDog`에만 저장되며, 자격 증명은 Windows DPAPI로 보호합니다. 저장소나 배포 파일에는 개인 설정·자격 증명이 들어가지 않습니다.

## 테스트

```bash
dotnet run --project tests/WindowsAiDog.Core.Tests
dotnet run --project tests/WindowsAiDog.Integration.Tests
dotnet run --project tests/WindowsAiDog.Timing.Tests
dotnet run --project tests/WindowsAiDog.Ui.Tests
```

UI 테스트는 실제 WPF 창을 잠깐 띄웁니다.

## 그림과 소리

- 강아지 그림은 이 프로젝트를 위해 AI로 생성했습니다. 현재 그림(`assets/sprites-v9`)은 Google Flow로 만든 영상에서 `tools/flow-matte.py`, `tools/flow-sheet.py`, `tools/import-flow-sprites.py`로 만들었습니다. Flow 생성물에는 보이지 않는 SynthID 워터마크가 들어 있습니다.
- 소리와 외부 코드의 출처·라이선스: [assets/licenses/THIRD_PARTY_NOTICES.md](assets/licenses/THIRD_PARTY_NOTICES.md)

## 라이선스

[MIT](LICENSE). 외부 자료는 각자의 라이선스를 따릅니다.

---

## English summary

- **Run:** `dotnet publish src/WindowsAiDog.Windows -c Release -r win-x64 --self-contained true -o out`, then start `out/WindowsAiDog.Windows.exe` (Windows 10/11 x64, unsigned).
- **Use:** click to pet, drag to move, right-click for treats, actions, focus mode, mute and settings.
- **AI chat (optional):** connect your own ChatGPT account, an OpenAI(-compatible) API key or a local OpenAI-compatible server. Nothing is called while idle.
- **Privacy:** settings and credentials stay in `%LOCALAPPDATA%\WindowsAiDog`, protected with DPAPI.
- **Art:** AI-generated for this project; the current sprites were made with Google Flow and carry invisible SynthID watermarks.
- **License:** MIT; third-party sounds (CC0) and Hermes-derived ideas (MIT) are listed in `assets/licenses`.
