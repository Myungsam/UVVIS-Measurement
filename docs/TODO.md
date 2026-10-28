# 남은 작업 / Open items

v2.3.0 릴리스(2026-10-28) 시점에 **나중에 하기로 미뤄 둔** 확인 사항입니다.
끝낸 항목은 체크하고, 결과는 CHANGELOG에 남기세요.

## 1. 램프를 켠 상태에서 표준 시료로 흡광도 실측 확인

2026-10-28 실장비 테스트(Flame-S, S/N FLMS18996, OmniDriver 2.56)는 **광원이 꺼진 상태**였습니다.
Dark와 White 평균이 2428.6 / 2427.2 counts로 거의 같아, 흡광도 수치는 잡음이었습니다.
측정, Dark/White, 흡광도 계산, CSV(`A = -log10((S-D)/(W-D))` 재현) 같은 소프트웨어 경로는
검증했지만, **실제 흡광도 값이 맞는지는 확인하지 못했습니다.**

- [ ] 램프 켜고 Dark(빛 차단) → White(용매/빈 셀) → 표준 시료 순서로 측정
- [ ] 알려진 흡광도 또는 몰흡광계수와 비교
- [ ] 피크 위치와 Beer-Lambert 계산값 확인

## 2. 실제 150% 배율 모니터에서 화면 확인

150% 배율은 에뮬레이션(`DpiHelper`를 144 dpi로 강제)으로만 확인했습니다.
c12af6f에서 시작 시 크래시와 분석 막대 잘림을 고쳤습니다.

- [ ] 실제 고배율 디스플레이에서 시작 화면, 분석 막대, 하드웨어 탭, About 창 확인
- [ ] 배율이 서로 다른 모니터 사이로 창을 옮겨 보기

## 3. Flame-S 외 분광기 모델 확인

실제로 확인한 장비는 Flame-S(USB2000+ 계열) 한 대입니다. 기능은 모델별 표 없이 드라이버에서
읽도록 만들었지만, 다른 모델에서는 직접 돌려 보지 못했습니다.

- [ ] Ocean FX: 트리거 지연 1 µs/count, dark 픽셀 0개 경로
- [ ] 두 대를 연결했을 때 실시간 광원 drift 보정

## 4. 코드 서명 (사용자 요청이 있을 때)

릴리스는 서명하지 않고 배포하며, README와 `사용법.txt`에 SmartScreen 경고를 넘기는 방법을 적어 두었습니다.
기관 PC에서 서명 없는 실행 파일이 막힌다는 요청이 오면 검토합니다.

- 후보: Certum Open Source Code Signing, OV 인증서(USB 토큰)
- 적용: `installer\build-installer.ps1 -CertificateThumbprint <지문>`

## 5. OmniDriver 단종 대응

OmniDriver/SPAM은 Ocean Optics의 단종 소프트웨어 목록에 있고, 후속 SDK는 OceanDirect입니다.
이 프로그램은 OmniDriver를 재배포하지 않고, 사용자가 따로 설치합니다.

- [ ] 신규 사용자가 OmniDriver를 정식으로 받을 수 있는지 Ocean Optics에 확인
- [ ] `Driver/` 계층만 OceanDirect로 교체하는 방안 검토(장비 인터페이스는 1단계에서 분리해 둠)
