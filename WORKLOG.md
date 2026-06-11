# STIGMA 작업 로그 (WORKLOG)

> 사용자는 **데스크탑(집)**과 **맥북(외부)** 두 환경에서 작업한다.
> Claude Code의 대화/로컬 메모리는 컴퓨터 간 공유되지 않으므로, 양쪽 Claude가
> 서로의 작업을 알 수 있도록 **모든 작업 내역을 여기에 기록**한다.
>
> - **최신 항목을 맨 위에** 추가한다.
> - 형식: `## YYYY-MM-DD — [머신] 제목` 아래에 변경 내용/이유를 적는다.
> - 세션 시작 시 이 파일을 먼저 읽어 다른 머신의 작업을 파악한다.
> - 작업 후 commit & push / 다른 머신에서는 pull 먼저.

---

## 2026-06-11 — [맥북] 게임오버 씬 업그레이드 C(글귀 타자기) + D(분위기 오버레이)

`GameOverScene.cs`. 컴파일 에러 0. (보류였던 게임오버 B/C/D 중 **C·D 구현**, B=붉은 눈은 이번엔 제외.)
- **C. 글귀 타자기**(`TypeEpitaph`/`PlaceCursor`/`BlinkCursor`): 사망 글귀가 한 번에 뜨던 것 → 타이틀 reveal(~1.6s) 끝난 뒤 **한 글자씩**(0.06s/자) 새겨짐. `maxVisibleCharacters` 방식(태그/글리프 안 깨짐). 끝에 **깜빡이는 커서** — AppleMyungjo에 커서 글리프 없을 수 있어 작은 사각형 `Image`로, 가운데 정렬이라 매 글자 마지막 글자 오른쪽으로 위치 추적.
- **D. 분위기 오버레이**(`BuildAtmosphere`): 비네트(절차 라디얼) + **가장자리 붉은 펄스**(핏빛 틴트 호흡 `RedPulse`) + **필름 그레인**(RawImage α0.05, `uvRect` 랜덤 오프셋으로 ~22fps 깜빡 `GrainFlicker` → *움직이는* 필름 그레인). 최상단·클릭 통과·1.2s 페이드인. 절차 `VignetteSprite`/`NoiseTexture` 추가.
- ⚠️ 게임오버 씬은 GameManager 등 정상 씬 흐름 필요 → MCP 백그라운드 캡처 검증 불가. **직접 플레이(전투 HP 0 → 진입) 육안 확인 필요.** 타이밍/세기는 값만 조정.

## 2026-06-11 — [맥북] 상점 배경·상인 말풍선 정리

`ShopUI.cs`. 컴파일 에러 0.
- **상인 말풍선 위치**: 얼굴 바로 아래(`-470,-90`)라 얼굴/몸통 상단에 걸침 → `-470,-300`으로 내려 얼굴 안 가리고 몸체를 살짝 가리게.
- **검정 박스 둥둥 문제 해결**: 딤(`colDim`) 알파 0.93(반투명)이라 뒤 노드맵이 비쳐 상인 일러의 *순수 검정 배경*만 박스로 떠보였음 → 딤을 **순수 검정 `(0,0,0,1)` 불투명**으로. + **그레인(치지직 노이즈) 오버레이 제거** — 노이즈 배경 위에 노이즈 없는 상인이 매끈한 박스로 도드라져서 뺌. (`MakeGrainSprite`/`GetGrainSprite`는 코드엔 남기고 호출만 제거.) 이제 배경=균일 검정이라 상인이 자연스레 묻힘.

## 2026-06-09 — [데탑] 플레이어 공격 모션 시도 → **원복(폐기)**

공격 모션 프레임 애니메이션 + 카드 발동 대기열을 구현했으나, **프레임 끊김 + 시트 화질 문제로 사용자가 폐기 요청 → 전부 원복.** (idle 단일 스프라이트 상태로 복귀)
- 삭제: `PlayerAttackAnimator.cs`, `Assets/Resources/Player/AttackSheet.png`(시트 임포트).
- 되돌림: `BattleManager`(카드 큐 → 원래 즉시 발동, 애니메이터 부트스트랩 제거), `CardUI`(호버/드래그 준비자세 훅 제거).
- 결과적으로 이 항목 관련 코드는 직전 커밋(c5da54d) 상태와 동일. 플레이어는 원본 idle 스프라이트 그대로.
- (참고: 향후 재시도 시 — 시트가 검정 단색 배경이라 투명 버전 필요, 프레임 수가 적어 끊김. 더 많은 프레임/투명 시트가 있으면 그때.)

## 2026-06-09 — [데탑] 인트로 컷씬(Cutscene.unity) 표시·카메라 개선

`CutsceneManager.cs`. 컴파일 에러 없음. **컷씬 Canvas = ScreenSpaceOverlay라 MCP 카메라 스샷 검증 불가 → 직접 플레이 확인 필요.**
- **나레이션 하단 자막 밴드로**: 기존엔 화면 중앙이라 캐릭터를 가림 → 런타임에 `canvasRT` 직속으로 재배치(하단 중앙, anchoredPos y=120). + **하단 어두운 그라데이션 스크림**(`MakeBottomScrimSprite`)으로 글자가 일러 위에 둥둥 뜨지 않게 받쳐줌. (폰트는 이미 명조 AppleMyungjo였음 → 진짜 문제는 위치/스크림/스타일)
- **스타일**: 명조 **이탤릭**, 재빛 오프화이트(0.9,0.88,0.83), 자간+2/행간+8, 가독성 아웃라인 0.16.
- **타자기 → 페이드 인**(`RevealText`, DD 톤): 한 줄 통째로 0.45s 페이드.
- **카메라 무빙 최소화**: 줌 2.0~2.4 → **1.05~1.13**, 포커스 점프 축소, 드리프트 ×1.05→×1.02. 셰이크(RedSpike 6→2.5, FlashCut 5→2), 번개 2회 날카롭게→1회 은은하게.
- 나레이션 **워딩은 이번에 안 건드림**(둘 다 별로였음 → 표시 먼저, 워딩은 다음에).
- **(후속) 무빙 시 가장자리 공백 수정**: 줌을 낮춰서 포커스 이동량이 커버 범위를 초과 → `ApplyTransform`에 `ClampToCover` 추가(이미지 크기×scale − 화면 크기 만큼만 이동 허용, margin 8px). 모든 무빙/드리프트/전환이 이 함수를 거쳐 일괄 적용. 무빙 유지·공백 제거.
- **(후속) 나레이션 다크소울 톤 교체**: 찬란했던 과거(왕국·신앙) → 외신의 응시("맞선 자는 무너지리라") → 멸망 → 낙인자가 되어 삶과 죽음을 무한 반복(불사의 굴레). 6·7이 한 문장으로 이어져 물에 비친 자아에서 마무리. 주인공=특별한 영웅이 아닌 굴레에 든 자. (사용자가 비트 제시)
- **(후속) 웅덩이 전환**: 흰 `FlashCut` → **`DarkDip`**(암전 디졸브, 셰이크 X) — 어둠에 잠겼다 비친 자아가 떠오르듯.
- **(후속) 카메라 = 깜빡(블링크) 컷 기반 재구성** (사용자 지시): 무빙(팬) 대신 **눈 깜빡이듯 빠른 암전 컷**으로 구도 점프. CutScene_1 구도(좌=왕국 성채, 중앙상단=검은태양/눈, 우=폐허) 확인 후 좌표 지정. 시퀀스: 왕국 CU(-0.22,0.02 줌2.0, 1·2번) → 깜빡 눈(0.02,0.18, 3번) → 깜빡 폐허(0.27,-0.04 줌2.2, 4·5번) → 깜빡 전체(0,0 줌1.0, 6번) → 디졸브 이미지2(7번). `Shot.blink` 필드 + `BlinkCut`(0.12s닫/0.16s열) 추가. `driftCo` 추적해 전환 전 드리프트 정지(블링크 위치 덮어쓰기 방지). Lightning 비트 미사용.
  - 좌표는 일러 구도 기준 추정 → 플레이 확인 후 미세조정 필요.
- **(후속) 드리프트 제거 + 좌표 재조정**: "클로징 후 뒤로 뚝 빠짐"의 원인 = 매 컷 미세 줌인(드리프트) 후 다음 컷에서 기준 배율로 리셋되며 팝 → **드리프트 전면 제거(정적 컷)**. `driftCo`/`Drift` 호출 삭제. 매핑 검증: **CutsceneImage가 캔버스(1920×1080) 정확히 채움 + preserveAspect=false** → focus 정규화 좌표 정상. 줌 2.0~2.2→**1.55~1.7**로 낮춤(가장자리 클램프·블러 완화). 좌표 재추정(왕국 -0.18,-0.02/눈 0.08,0.18/폐허 0.20,-0.05). **단 컷씬=Overlay라 렌더 스샷 검증 불가 → 사용자 플레이 피드백으로 좌표 확정 필요.**
- **(후속·방향전환) 비/번개 폭풍 연출**: 깜빡/클로즈업 시퀀스 폐기 → ①왕국 CU(비·번개X, 1·2번) → ②"검은 태양…"(3번)에서 `LightningReveal`(흰 번개로 덮으며 전체 일러로 컷)+`StartWeather`(비 페이드인+`LightningLoop` 2.2~4.5s 주기 번개) → ③전체 일러 유지(4·5·6번) → ④이미지2 디졸브(7번). 비=절차 빗줄기(`MakeRainTexture`) RawImage 아래 스크롤(Update). `Beat.RevealStorm` 추가. 튜닝: FadeRain 0.7/스크롤 1.8/번개간격 2.2~4.5.
- **(후속·정정) 비 삭제 → 그레인(지지직) 복귀**: 사용자 의도 = 빗줄기가 아니라 **기존 필름 그레인(지지직 노이즈)** 연출. 추가했던 비 RawImage/MakeRainTexture/FadeRain 전부 삭제. `StartWeather`가 폭풍 시 `BoostGrain`(grain α 0.04→0.16)+Update에서 그레인 빠르게 스크롤(지지직 강화)+주기 번개. **왕국 CU 좌표**: 캐릭터(왼쪽 전경) 제외 위해 눈 아래 도시 쪽 `(0.22,-0.08)` 줌2.0으로(중앙→우측).

## 2026-06-09 — [데탑] 전투 업그레이드 P1 — 배경 분위기 (아트 0개)

`BattleAtmosphere.cs` 신규 + `BattleManager.Awake` 부트스트랩 1줄. 컴파일 에러 없음. MCP로 플레이 캡처 검증 완료(흰 하늘→음산 청보랏빛).
- **발견**: 전투씬 흰 하늘 정체 = **카메라 `clearFlags=Skybox`**(스프라이트 아님). URP가 아니라 **Built-in 파이프라인으로 보고됨**(Volume/포스트프로세싱 없음) → 렌더 파이프라인 무관 절차 방식 채택.
- **카메라 클리어 어둡게**(`overrideCameraClear`, skyColor): 스카이박스→솔리드 → 흰 하늘 즉시 제거(가장 큰 변화).
- **배경 틴트**: "Background" 하위 SpriteRenderer 6종을 어둡고 차갑게(절대색 지정, 멱등). 캐릭터 제외.
  - **함정**: 배경 오브젝트 Awake 가 색을 흰색으로 덮어써서, Awake 1회 적용은 안 남음 → **즉시 + 1프레임 뒤** 2회 적용으로 해결.
- **오버레이 캔버스**(sortingOrder -200, 월드 위/UI 아래): 비네트 + 상단 암전 그라데이션 + 보랏빛 색 워시 + 떠오르는 불티. **단 ScreenSpaceOverlay 는 카메라 경로 스샷에 안 잡힘** → 오버레이 효과는 실제 플레이에서만 보임(스샷 검증 불가).
- 전부 인스펙터 값 노출(tint/skyColor/vignette/topDarken/colorWash/emberCount).
- **MCP 학습**: `manage_camera screenshot game_view`는 카메라 렌더만 캡처 → ScreenSpaceOverlay UI 미포함. 직접 BattleScene 플레이는 GameManager/NextEncounter 부트스트랩이 없어 몬스터·카드·HUD 미생성(트랩 #7).

## 2026-06-09 — [데탑] 전투 업그레이드 Stage 1 — 드래그 화살표 + 타겟 하이라이트

`DragArrow.cs` 강화. 컴파일 에러 없음. (Stage 2 = 타격감 패스 예정)
- **화살표 굵게/연출**: 폭 0.1→0.22, 끝으로 갈수록 옅어지는 그라데이션(머리 진함), 둥근 끝(numCap/CornerVertices), **흐르는 점선**(절차 dash 텍스처 + 길이비례 타일 + offset 스크롤), curveRes 28.
  - **⚠ 직렬화 함정**: 기존 `lineWidth`가 씬에 0.1로 직렬화돼 코드 기본값을 바꿔도 무시됨 → **새 필드 `arrowWidth`로 교체**해 코드 기본값(0.22) 적용되게 함. (신규 필드 `validColor`/`flowSpeed`/`dashWorldSize`도 직렬값 없어 코드 기본 적용)
- **유효 타겟 위 색 전환**: 화살표가 살아있는 몬스터를 가리키면 빨강→**금빛**.
- **타겟 하이라이트**(`MonsterTargetHighlight`): 가리키는 몬스터 뒤에 **금빛 실루엣 글로우(맥동)** — 본체 스프라이트 복제·flipX 동기화, sortingOrder 본체-1(뒤). 본체 색/스케일은 안 건드림(피격 플린치 충돌 방지). 드래그 중 실시간 토글, `HideArrow`에서 해제.

## 2026-06-09 — [데탑] 상점 Phase 2 — 돈 없을 때 상인 말풍선 대사

- **상인 말풍선**(`BuildMerchantBubble`): 왼쪽 상인 곁(-470,255)에 금빛 명패 톤 말풍선(둥근 패널+금테+꼬리). 평소 알파0, 클릭 시 표시. `ShowMerchantLine`/`BubbleRoutine`(등장 펀치 → 2.6초 유지 → 페이드). 직전과 같은 대사 회피.
- **돈 없을 때 트리거**: `NoGoldLines` 8종(랜덤) + 체력가득 시 `FullHpLine`. `OnBuy`에서 `Gold<price`면 구매 대신 `ShowMerchantTaunt`. `OnPotion`은 체력가득→FullHpLine, 골드부족→Taunt.
- **클릭 가능하게 변경**: 못 사는 카드/포션이 기존엔 `interactable=false`라 클릭 무반응 → **카드 버튼 항상 interactable, 포션은 `!potionUsed`면 클릭 허용**(회색/빨강 시각은 유지). 그래야 클릭 시 상인 대사가 뜸.
- 컴파일 에러 없음.

## 2026-06-09 — [데탑] 상점 위로 드로잉 버튼 바 뚫고 나오던 것 수정

- **원인**: `MapDrawingUI` 캔버스 `sortingOrder 1100` > 상점 900 → 상점 위로 표시. (상점을 1100 위로 올리면 HUD 1000도 가려져 골드 사라짐)
- **해결**: 상점 진입 시 `MapDrawingUI` 자식 캔버스(`MapDrawingUICanvas`)만 `SetActive(false)`, `CloseRoutine`에서 복구. 같은 GO 의 `MapDrawingManager`(데이터/입력)는 유지. `FindFirstObjectByType` 사용.
- **상점 패널을 상단 HUD 아래로**: 패널을 중앙 앵커 → **상단 앵커(pivot 0.5,1)**로 변경, `anchoredPosition (385,-74)`(화면 top − HUD높이64 − 간격10), height 1010→976. 자식들은 패널 중앙 기준이라 그대로 따라 내려감. (HUD `hudHeight=64`, `hudTopOffset=0` 기준)

## 2026-06-09 — [데탑] 상점 Phase 2 일부 — 상인 일러스트 배치 + 레이아웃 우측 재배치

- **상인 일러스트 추가**: `OneDrive/.../스티그마 에셋/상점상인.png` → **`Assets/Resources/Shop/Merchant.png`** (Sprite, 1456×816). 다키스트던전풍 후드 상인.
- **`BuildMerchant`**: 와이드 원본에서 **상인만 크롭**(`Sprite.Create` 서브렉트, `merchantCrop` 정규화값 — **추정치라 나중 미세조정 필요**) 후 **왼쪽**에 배치(`merchantPos`/`merchantHeight` 필드). 미세 호흡 코루틴(`MerchantBreathe`). 일러 배경이 순수 검정이라 왼쪽을 어둡게 유지해 자연 블렌딩.
- **레이아웃 우측 재배치**: 중앙 글로우를 오른쪽(x=385)으로 이동. **제단 패널을 풀스크린→우측(x=385, 1140×1010)**. 카드 배율 1.45→**1.12**(좁아진 패널에 5장 맞춤), cardRow 1100×420·간격 20. 제목 46pt. 리롤/돌아가기 360×88. **포션 컨테이너 0.72배 축소**(호버 캡처 전 localScale 설정).
- 컴파일 에러 없음. (상인 위치/크롭, 카드·포션 크기는 사용자 확인 후 조정 예정)

## 2026-06-09 — [데탑] 상점 업그레이드 "외신 상인의 제단" Phase 1 (분위기+토대)

전부 절차생성. `ShopUI.cs`. 컴파일 에러 없음. (Phase 2=상인 NPC/대사, Phase 3=연출 예정)
- **분위기**(`BuildAtmosphere`): 단색 딤 → 보랏빛 거의-검정 딤 + **중앙 핏빛/보랏 글로우(맥동 BreatheGlow)** + **비네트** + **그레인(타일 노이즈 α0.028)** + **떠오르는 불티 12개**(`BuildEmbers`/`EmberLoop`, 최상단·클릭 비차단, 떠오르며 밝아졌다 사라짐).
- **제단 패널**(`StyleAltar`): 어두운 석재 톤 + 이중 테두리 + **네 모서리 룬(마름모 겹친 인장)**. 제목 "✦ 외신 상인의 제단 ✦".
- **카드 뒤 등급 글로우**: 등급색 방사 글로우, 희귀할수록 강하게(Common 0.16 → Mythic 0.6). 전설+ 는 맥동(PulseGlow).
- 절차 스프라이트 3종 추가: `MakeRadialSprite`/`MakeVignetteSprite`/`MakeGrainSprite` (static 캐시).
- **헤더 큰 골드 표시는 사용자 요청으로 제외** — 골드는 상단 HUD로만.

## 2026-06-09 — [데탑] 상점 카드 = 전투 카드 프리팹 그대로 사용 (절차생성 폐기)

직전 절차생성 카드(일러스트 찌그러짐·cost박스/아이콘 이상·우상단 군더더기 박스)가 별로여서 **전투용 `CardPrefab`을 그대로 인스턴스화**하는 방식으로 전환(리워드 씬과 동일 패턴).
- **프리팹 이동**: `Assets/Prefabs/CardPrefab.prefab` → **`Assets/Resources/CardPrefab.prefab`** (런타임/빌드에서 `Resources.Load`로 불러오려면 Resources 아래 필수). GUID 보존되어 리워드 씬 인스펙터 참조는 그대로 동작. (구 위치 `.bak`은 사용자 백업이라 그대로 둠)
- **`ShopUI.cs`**: `BuildCardVisual`/`TypeBadge`(절차생성) **삭제**. `BuildOffer`가 `Instantiate(cardPrefab)` → `CardUI.Setup(card)` → 루트 Button 리스너를 구매로 교체. 카드 표시 배율 `CardScale=1.45`(원본 180×240). 스케일은 레이아웃에 안 잡히므로 Offer 컨테이너 `LayoutElement` 크기를 스케일 후 크기로 잡아 간격 확보. 회색처리용 CanvasGroup 런타임 부착. 호버는 프리팹 CardUI 자체(compactHoverScale)가 처리.
- **`CardUI.cs`**: `OnBeginDrag`에 `if (BattleManager.Instance == null) return;` 가드 추가 → 전투가 아닌 상점/리워드에서 타겟 카드 드래그 시 NRE 방지.
- 컴파일 에러 없음.

## 2026-06-09 — [데탑] 상점 카드 비주얼 보강 · 호버 · 군더더기 텍스트 제거

`ShopUI.cs` 수정. 컴파일 에러 없음.
- **카드/포션 마우스 호버 확대**: `ShopHoverGrow`(IPointerEnter/Exit, unscaled lerp, ~1.1배) 신규 컴포넌트를 카드 루트·포션에 부착.
- **카드 비주얼 재구성**: 기존엔 일러스트만 보였음 — **설명 텍스트가 하단 앵커+top 피벗이라 카드 밖으로 빠져 잘려 있던 버그** 발견·수정. 이제 등급색 프레임 + 어두운 바탕 + **이름 밴드(상단 띠, 오토사이즈)** + 일러스트 + **cost 박스(좌상단 둥근 파란 배지+테두리)** + **타입 아이콘 배지(우상단, 종류별 색+글자 공/술/금/력 — `TypeBadge`)** + **설명 패널(하단, 오토사이즈 래핑)**. 둥근 모서리는 빌트인 `UISprite` 슬라이스.
- **군더더기 텍스트 제거**: "골드는 상단에 표시됩니다", "─ 체력 회복(상점당 1회) ─" 둘 다 삭제. **포션 1회 제한 자체는 유지**(텍스트만 제거).
- BuyPulse(구매 펄스)는 호버 스케일과 충돌해 제거.

## 2026-06-09 — [데탑] 상점 개선 (창 확대 · 반복구매 · 포션 모양)

`ShopUI.cs` 수정. 컴파일 에러 없음.
- **상점 창 화면 거의 꽉 채움**: 패널 `1560×780` → `1840×1010`, 중앙으로(-40→-8). 제목 46→58pt. 카드 줄/포션/버튼 레이아웃을 패널 중앙 앵커 기준으로 전면 재배치. 카드도 키움(210×330→250×410, 비주얼 240×336).
- **돈 되는 만큼 반복 구매**: 기존 "한 번 사면 SOLD로 잠김"(`o.sold`/`soldOverlay`) **완전 삭제**. 이제 골드만 충분하면 같은 카드도 계속 구매 가능(못 살 때만 회색+가격 빨강). 구매 시 카드 살짝 펄스(`BuyPulse`).
- **카드 5종 아래 포션 모양 체력회복**: 네모 텍스트 버튼 → **코드로 그린 포션 병**(코르크+병목+둥근 유리 몸통+빨간 액체+유리 광택+❤마크, 둥근 모서리는 빌트인 `UISprite` 슬라이스). 카드 줄 아래 중앙. **상점당 1회만**(`potionUsed` 유지), 가격 35→**85**(조금 비싸게)·회복 25→30. 사용 후/체력가득/골드부족 시 흐려짐.
- 리롤·돌아가기는 하단 한 줄로.

---

## 2026-06-08 — [데탑] 노드맵 꾸미기 + HUD 리뉴얼 + 상점 신규 구현

- **노드맵 배경**: 회색(카메라) → ScrollRect 배경 이미지를 **플랫 단색(머스터드 노란, 인스펙터 `mapBackgroundColor` 노출)**으로 교체. (그라데이션·비네트는 시도했다 "어둑/구분안됨" 피드백으로 단색 확정. ScrollRect 반투명 흰 배경이 회색 워시 원인이었음)
- **노드 비주얼 리뉴얼** (`MapNodeUI` 전면 재작성, 런타임 절차생성): 어두운 돌 본체 + 타입색 룬 테두리 + 타입 엠블럼(시작 점/전투 X/상점 동전/이벤트 다이아/낙인 8각별/보스 눈) + 갈수있는곳 호흡 글로우 + 현재위치 금빛 펄스링 + 방문/잠금 어둡게 + 보스 1.55배. 음산 톤다운 팔레트(밝은 원색이 "장난같다"는 피드백). 마우스 호버 확대.
  - `HoverScale`(BrandNodeManager): 기본 스케일을 1 고정 → **현재 스케일 기준(Awake 캡처)**으로 일반화(노드맵 mapScale 1.5 대응).
- **상단 HUD 리뉴얼** (`HUDManager`): 한 줄 다닥다닥 → **좌(HP/골드 크게)·중앙(시선+효과 pip 5개)·우(층/보스)** 3그룹 분리. placeholder 사각형 → 코드 하트/코인 아이콘. 시선효과 5칸을 작은 점(pip)으로 정리(B안).
- **상점 신규 구현**: 기존 `ShopManager.cs`+`Shop.unity` **삭제**(전임자 포기분) → `ShopUI.cs` 신규. 노드맵 위 **오버레이**(sortingOrder 900, HUD 아래라 HUD 골드 그대로 사용). 카드 N장(allCards 풀, 폴백 startingDeck) + 등급별 가격(고정+약간랜덤) **카드 아래 표시**, 못 사면 **카드 회색+가격 빨강**. 회복 포션(+25/35골드 1회), 리롤(20골드), 돌아가기. `MapSceneManager` Shop 케이스 → `ShopUI.Spawn`.
- **씬 값 수정**(요청): `NodeMap.unity` MapGenerator `shopChance 0→25`, combat 60→55 (브랜드 나머지=20). shopChance가 0이라 상점 노드가 안 떴던 것.
- **외신 조우 닫기 전환**: 선택 후 일러스트/검정 2단계 비침 → 독립 캔버스 암전으로 '검정 덮기→맵 드러내기' 대칭 전환.
- **(주의) `ShopUI.cs.meta` 손상 재발** — Write로 만든 .cs의 메타가 guid 두 줄만 생성됨. MonoImporter 블록 보강 후 커밋(StartEncounter 때와 동일 이슈).

---

## 2026-06-08 — [데탑] 외신 조우(StartEncounter) 일러스트·가독성·진입 개선 + .meta 손상 버그 수정

- **버그(중요): `StartEncounterUI.cs.meta` 손상 복구** — 맥북에서 커밋된 메타가 `fileFormatVersion`+`guid` 두 줄뿐(MonoImporter 블록 누락)이라, 데스크탑 체크아웃에서 Unity가 `StartEncounterUI.cs`를 컴파일 안 함 → `MapSceneManager`에서 `CS0103` → 외신 조우 호출이 아예 빠진 예전 빌드로 돌아 **스타트 시 조우가 안 떴음**. 표준 MonoImporter 블록 보강(GUID 유지)으로 해결.
- **니알라토텝 일러스트 추가**: `Assets/Resources/Encounters/Nyarlathotep.png`(Sprite 임포트) → `Resources.Load`로 조우 진입 시 풀스크린 표시. (이후 새 이미지로 1회 교체)
- **금빛 톤 통일**: `godColor`를 일러스트 금빛(0.86,0.62,0.26)으로. 선택지 버튼 색을 외신색 기반으로(니알=금빛). 미세 hover.
- **이름/상호명 가독성**: 상단 중앙 **어두운 띠 박스**에 배치(후광·얼굴 안 가림) + 금색 글자 + 검정 TMP 아웃라인 + 폰트 키움.
- **대사 줄바꿈**: 어절(공백)을 `<nobr>`로 묶어 단어 중간 끊김 방지("크툴루"→"크툴/루"). 타자기는 `maxVisibleCharacters` 방식으로 변경(태그 안 깨지게). 본문 시작점을 명패 아래로 내림. 클릭 힌트 박스 안쪽으로(우측 삐짐 수정).
- **진입 개선**: 조우 페이드인 제거 → 첫 프레임부터 불투명으로 덮어 **노드맵 깜빡임 없이 바로 외신**.
- (TODO: 선택지 문구/효과는 여전히 placeholder.)

---

## 2026-06-08 — [데탑] 낙인 노드 선택 연출 + 노드맵 확대

- **노드맵 전체 확대** (`MapSceneManager.cs`): `mapScale` 필드 추가(기본 1.5). 노드 위치·`localScale`·라인 두께·컨테이너 높이에 배율 적용. 표시 시점에만 적용 → 저장된 맵도 동일하게 확대. `MapGenerator`/프리팹/씬 미수정. 인스펙터에서 값만 바꿔 조정 가능.
- **낙인 선택 연출** (`BrandNodeManager.cs`): 효과 교체/카드 제거 확정 시 선택한 항목에 연출. (A) 낙인 각인 — 펀치 스케일 1→1.08→1 + 회전 미세 진동 + 텍스트 흰색→불씨주황→그을린색 + 붉은 글로우. (C) 사슬 봉인 — 가로/세로 링 번갈아 겹친 사슬 두 가닥 X자 + 중앙 인장, 전부 코드 생성(스프라이트 0). 기존 빨간 ✕ 표시는 폐기. 컴파일 에러 없음.
  - **(수정)** 화면 빨간 플래시 제거(카드 주황 번쩍과 겹쳐 짜침). 사슬을 통째 페이드 → **양 끝에서부터 링 하나씩 톡톡(0.5→1.1→1 오버슛, 링당 0.03초 스태거) X자로 그어지고 마지막에 중앙 인장이 잠김**으로 변경. 노드 크기(1.5배)는 확정 유지.
  - **(2차 수정)** 카드 주황 글로우(CreateGlow)·emberColor 완전 삭제 → 텍스트는 흰색→그을린색으로만 차분히 변색. 사슬 등장 방향을 좌→우에서 **아래→위 X자**로 변경(한쪽 가닥 순서 reverse해 두 가닥 모두 바닥에서 솟아오름).
  - **(3차 수정)** 중앙 인장: 진한 빨간 다이아 → 쇠색 8각 별 + 어두운 인셋 + 은은한 헤일로/작은 보석점(채도↓).

## 2026-06-08 — [데탑] 낙인 씬 UI 전면 업그레이드 (다크·음산 톤)

전부 코드 절차생성(외부 스프라이트 0). `BrandNodeManager.cs`.
- **효과 카드 재구성**: 300x384 다크 패널 + 테두리. 헤더밴드("시선 N", 단계별 위험색 20옅음→100핏빛) + 큰 이름 + **버프(청록)/디버프(진홍) 분리 패널**(좌측 accent 바·태그·내용, 폰트 크게). VerticalLayoutGroup으로 안정 배치. `StyleEffectCard`로 wired/절차 경로 통일.
- **배경 분위기**: 절차 텍스처로 비네트 + 그레인(α0.03) + 중앙 붉은 시선 글로우 + 떠오르는 불티 10개(EmberLoop). `BuildAtmosphere`.
- **모달**: 효과 교체 항목을 이름 + 버프/디버프 라인 배지로(`BuildEffectSwapEntry`/`BuildStatLine`).
- **타이틀/버튼**: 제목 96pt + 룬 장식선, 버튼 확대, **HoverScale**(마우스오버 확대, 잠금 시 Disable로 각인 펀치와 충돌 방지) 카드·버튼·모달항목에 부착.
- EffectCardUI 필드 교체(header/name/buff/debuff), 구 ✕/Desc 경로 폐기. 컴파일 에러 없음.

---

## 📌 TODO — 게임오버 씬 추가 연출 (보류, 데탑/다음 세션에서 이어서)

> 2026-06-01 맥북에서 게임오버 씬 연출 4종(A/B/C/D)을 제안 → **A만 구현 완료**, B/C/D는 보류.
> 진입은 **즉시 전환 유지**(페이드아웃 X), 씬 자체에 연출만 추가하는 방향. 전부 추가 에셋 없이 절차 생성 가능.
> 구현은 `Assets/Scripts/GameOverScene.cs` (UI 전부 코드 생성, A는 `GameOverTitleFX` 컴포넌트 참고).

- **A. 타이틀 피-reveal + 떨림 + 글로우 호흡** — ✅ **완료** (`GameOverTitleFX`).
- **B. 중앙 붉은 시선 눈 (핵심 추천)** — 타이틀 뒤에 거대한 붉은 눈동자(절차생성 라디얼)가 떠 있고 천천히 수축/확장(호흡) + 가끔 깜빡(blink). STIGMA "응시" 테마 직결. (A의 `RadialSprite` 글로우를 눈 형태로 확장하면 됨.)
- **C. 글귀 타자기 효과** — 랜덤 사망 글귀(`Epitaphs` 배열)가 명조체로 한 글자씩 새겨짐 + 끝에 깜빡이는 커서. 현재는 한 번에 표시중.
- **D. 비네트 + 필름 그레인 + 가장자리 붉은 펄스** — 상시 분위기 오버레이. 컷씬(`CutsceneManager`)에 이미 만든 비네트/그레인 절차생성 코드 재활용 가능.
- (E. 진입 화면 흔들림 / F. 사망 드론·심박 사운드 — 사운드는 오디오 에셋 유무 확인 필요.)

---

## 2026-06-05 — [맥북] 스타트 노드 = 외신 조우 "출발의 의식" 프레임워크 구현 (틀만, 선택지 효과는 보류)

> **배경**: `MapSceneManager.OnNodeSelected`의 `case Start`가 `Debug.Log("추후 구현")`뿐인 죽은 코드였음. 원인 — 새 런 시작 시 `InitializeStartState`에서 플레이어가 이미 Start 노드 위에 서 있고 `isAccessible=false`라 클릭 자체가 불가. 그래서 Start 노드는 "클릭 대상"이 아니라 **새 런 진입 시 자동 발동하는 연출**로 설계.
> **컨셉**: 플레이어(낙인자)가 잠든 크툴루를 찾아 심연으로 내려가는 길에, 각 장(Act)의 스타트 노드에서 **크툴루 신화 외우주의 신(Outer God)** 과 조우 — 그들이 대가를 걸고 돕는다. 슬더스 이벤트식: 중앙 일러스트 + 하단 다이얼로그(외신↔플레이어 교대, 화면 클릭으로 진행) → 마지막 대사 후 하단 선택지 3개.

- **새 `StartEncounterUI.cs`** (전부 코드 절차생성, OptionsMenu/GameOverScene 패턴 차용):
  - 풀스크린 캔버스(sortingOrder 1000) + 딤(맵 클릭 차단) + CanvasGroup 페이드인.
  - **중앙 일러스트**: Sprite 있으면 표시, **없으면 placeholder**(외신색 라디얼 글로우 절차생성 + "[일러스트]" 라벨 + 호흡 애니). → 나중에 `EncounterDef.illustration`에 Sprite만 꽂으면 교체.
  - **다이얼로그**: 화자 이름(외신/낙인자 교대·색 구분) + 타자기 효과(클릭=현재 줄 즉시완성/다음 줄). 레거시 `Input.GetMouseButtonDown` 사용(프로젝트 관례). 마지막 줄 후 박스 숨김.
  - **선택지 3개**: 하단에서 슬라이드업+페이드인. **지금은 "선택지 1/2/3" placeholder** — 클릭 시 효과 없이 `onComplete`만 호출(닫기). 효과 기획은 다음 단계.
  - **외신 데이터(장별, 코드 내 placeholder 대사)**: 1장 **니알라토텝**(기어다니는 혼돈/전령), 2장 **슈브니구라스**(천 마리 새끼의 검은 산양), 3장 **요그소토스**(문이자 열쇠). 각 5줄, 플레이어는 단답. `BuildEncounter(actIndex)`로 갈아끼움 — 인스펙터/SO 이전 쉬운 확장형.
- **`GameManager.cs`**: `startEncounterDone` 플래그 추가(SaveData 직렬화 + Save/Load + `ResetForTitle`/`InitializeDeck` 리셋). 한 장(맵)에서 1회만 발동, 전투 복귀/세이브 로드 진입 땐 안 뜸.
- **`MapSceneManager.cs`**: 새 맵 생성 분기(`else`)에서만 `isFreshRun=true`. `RefreshAllNodes` 후 `!startEncounterDone`이면 `StartEncounterUI.Spawn(bossesDefeated, OnStartEncounterDone)`. 콜백에서 플래그 set + Save. (※ 현재 Act1만 구현이라 실제로는 `bossesDefeated=0` → 니알라토텝만 등장. 2/3장은 멀티액트 붙으면 자동.)
- **검증**: 컴파일 **에러 0**(경고는 기존 코드들도 쓰는 `enableWordWrapping` 구식 경고뿐). `execute_code`로 에디트 모드 빌드 테스트 — 예외 없이 계층(딤/일러스트/박스/선택지3) + TMP 8개 정상 생성 확인 후 정리. **단, 다이얼로그 진행/선택지 등장 등 인터랙티브 흐름은 Play+정상 씬흐름(GameManager 살아있어야)이 필요** → 고립 NodeMap Play엔 GameManager null이라 안 뜸. **포그라운드 풀플레이 육안 확인 권장**(백그라운드 게임뷰 미렌더 제약).
- **남은 작업**: ① 선택지 3개의 실제 효과 기획·구현(최대HP/골드/시선 등) ② 외신 일러스트 에셋 연결 ③ 멀티액트 시 장별 외신 매핑 확인.

### 같은 날 1차 육안 피드백 반영 (UI 조정)
- **일러스트 풀스크린화**: 840×600 중앙배치 → **화면 전체 stretch**(맵 완전히 가림, raycast로 맵 클릭 차단). placeholder 글로우 560→1150 확대. 하단 가독성용 어두운 띠(BottomShade, 높이 470) 추가.
- **선택지 하단 짤림 수정**: y −250/−380/−510 → **−150/−290/−430**, 크기 1340×110. 최하단 경계 −485 > 화면바닥 −540 → 안 짤림(런타임 측정 확인).
- **화자 이름 왼쪽 짤림 수정 + 화자 식별 강화**: 박스 밖으로 삐져나가던 좌측정렬 텍스트 → **명패(NamePlate) 방식**으로 교체. 화자색을 어둡게 깐 배경 + 밝게 띄운 글자. **외신=좌측 / 플레이어=우측** 좌우 배치로 누가 말하는지 한눈에. 명패는 박스 자식이라 선택지 단계서 같이 사라짐.
- **대화 속도**: 타자기 0.028s → **0.055s/자**(절반 속도). 본문 폰트 33→38.
- **드로잉 UI 가림**: 조우 중 좌하단 드로잉 버튼바(`MapDrawingUICanvas`, sortingOrder 1100)가 조우(1000) 위로 비치던 것 → 조우 캔버스 sortingOrder **1000→2000**. 풀스크린 일러스트가 시각적으로 가리고 클릭도 차단, 조우 종료(캔버스 파괴) 시 자동 복귀. (드로잉 입력 자체는 맵 캔버스 오버레이라 이미 일러스트 raycast가 차단 중이었음.)
- **외신 상호명(별칭) 추가**: `EncounterDef.godEpithet` 필드 추가. 이름(62pt 볼드) 아래에 작게(30pt 명조 이탤릭 + 자간 10 + "— … —" 감싸기 + 이름색을 밝게 푼 무드 색)로 표시 — LoL 로딩화면식. 니알라토텝=「기어다니는 혼돈」, 슈브니구라스=「숲속의 검은 산양」, 요그소토스=「문이자 열쇠」. execute_code로 3외신 전부 이름+상호명 렌더 확인.
- 검증: 컴파일 에러 0 + execute_code 런타임 빌드 — 풀스크린/명패/선택지 위치 정상. (인터랙티브 흐름은 여전히 포그라운드 풀플레이 육안 확인 필요.)

---

## 2026-06-01 — [맥북] 낙인 노드 씬 UI/글자 확대 (약 1.35배 + 효과카드 행 중앙 재정렬)

- **목적**: 낙인 씬 UI(특히 효과 교체 모달)가 너무 작아 글자 읽기 어려움 → 씬 전체 UI·글자 확대.
- **원인 파악**: 낙인 씬 메인 UI는 **씬에 프리빌트**(BrandNodeManager.WireExistingUI 경로 사용)라 코드의 절차생성 크기값이 안 먹음. 효과카드 5개는 HorizontalLayoutGroup + 수동 x오프셋(-600)으로 정렬돼 있어 손대면 깨지기 쉬움. 모달(교체/제거)은 항상 코드 절차생성.
- **`BrandNodeManager.cs` 수정** (전부 코드, 양쪽 머신 git 공유):
  - **새 `ApplyLargerLayout()`** (WireExistingUI 끝에서 호출): 런타임에 프리빌트 UI 확대 —
    - 효과카드 220×240→**290×330**, 폰트 Threshold 32→42 / EffectName 22→30 / Desc 14→20 / LockX 140→180.
    - 제목 64→84, 부제 26→34(박스 1500 폭), 하단 버튼 360×80→480×104·라벨 28→36, 행이 커진 만큼 버튼 Y 재배치.
    - **효과카드 행 중앙 재정렬**: HLG 폭을 콘텐츠 총폭(5*290+4*26=1554)과 정확히 일치 + anchoredPos.x=0 → 화면 정중앙. (폭 0 두면 반쪽폭만큼 치우침 — 런타임 측정으로 확인 후 명시 폭 채택.)
  - **모달 확대(절차생성 코드 직접 수정)**:
    - 효과 교체 모달: 패널 900×700→**1120×820**, 제목 32→42, 항목 높이 70→96·제목 22→30·설명 16→22, 닫기 280×60→340×78·폰트 22→30.
    - 카드 제거 모달: 제목 36→44, 스크롤뷰 1300→1340폭, 그리드 셀 180×240→**224×300**(카드 프리팹 인스턴스도 동일 스케일), 푸터 버튼/라벨 확대(삭제 440×84·30, 취소 320×84·28).
- **검증**: 컴파일 에러 0. Play 런타임 직접 측정 — 카드 290×330·폰트 적용 확인, **효과카드 행 card60 중심=캔버스 중심(diff 0)** 완전 중앙정렬 확인, 교체 모달(1120×820/42/30/22)·제거 모달(셀 224×300/제목44) 빌드 정상. (시각 스샷은 백그라운드 게임뷰 미렌더 제약 — 포그라운드 육안 확인 권장.)
- 씬 에셋은 미변경(런타임 확대 방식). GazeEffectManager/GameManager 싱글톤은 고립 Play라 null → 카드명 "미배정"/덱 비어있음으로 표시되나 레이아웃엔 무관.

## 2026-06-01 — [맥북] 메인메뉴 Options 버튼 기능 구현 (볼륨/음소거/전체화면)

- **목적**: 메인메뉴 `Options_Button`이 아무 동작도 안 했음 → 옵션 패널 신규 추가. (사운드 에셋은 아직 없지만 추후 대비해 볼륨 체계 먼저 구축.)
- **새 `OptionsMenu.cs`** (`MainMenuWiring`과 동일 패턴 — `[RuntimeInitializeOnLoadMethod]`, 별도 GameObject 배치 X, UI 전부 코드 절차생성):
  - **Options_Button 와이어링** → 클릭 시 옵션 전용 Canvas(sortingOrder 1000) + 딤배경 + 중앙 카드 패널을 절차생성, CanvasGroup 페이드 인(0.18s). 폰트는 씬 기존 TMP에서 자동 차용.
  - ① **볼륨 슬라이더**: `AudioListener.volume`(전역 마스터 0~1) 조절. 슬라이더는 Unity 표준 구조(Background/Fill Area·Fill/Handle Slide Area·Handle) 코드 생성.
  - ② **음소거 토글(네모 버튼)**: 슬라이더 옆. 즉시 음소거↔복원. 음소거 중엔 슬라이더를 움직여도 볼륨값만 저장하고 적용은 보류, 해제 시 저장값 복원. 라벨/색 ON(녹)·OFF(적).
  - ③ **전체화면 토글**: `Screen.fullScreen` 전환, 라벨 ON/OFF.
  - **저장**: `PlayerPrefs`("opt_masterVolume"/"opt_muted"/"opt_fullscreen"). `BeforeSceneLoad` 훅에서 부팅 시 저장값을 모든 씬 공통으로 적용.
  - 닫기: "닫기" 버튼 + 우상단 "X" + 딤배경 클릭. (글리프 두부 방지로 텍스트만 사용 — 게임오버 씬 관례 동일.)
- **검증**: 컴파일 에러 0. Play 런타임 직접 검증 — 슬라이더 0.5→volume 0.5+저장, 음소거 ON→volume 0/저장, 음소거 중 슬라이더 0.3→volume 0 유지·값만 저장, 음소거 OFF→volume 0.3 복원, 전체화면 라벨 OFF(창모드) 모두 정상. (시각 스샷은 ScreenSpaceOverlay라 카메라 캡처에 안 잡힘 + 백그라운드 게임뷰 미렌더 — 포그라운드 육안 확인 권장.)
- (TODO 아이디어: BGM/SFX 분리 슬라이더는 사운드 에셋 추가 시 / 스크린쉐이크 ON·OFF 토글도 추후 가능.)

## 2026-06-01 — [맥북] 게임오버 타이틀 연출 추가 (피-reveal + 떨림 + 글로우 호흡)

- **목적**: 게임오버 씬이 너무 밋밋(어두운 단색 + 빨간 타이틀) → "GAME OVER" 타이틀에 분위기 연출 추가. (진입은 요청대로 **즉시 전환 유지**, 페이드아웃 안 건드림.)
- **새 `GameOverTitleFX` 컴포넌트** (`GameOverScene.cs` 하단, TMP 정점 직접 조작 — 추가 에셋 0):
  - ① **피-reveal**: 글자가 위→아래로 붉게 스며들 듯 등장(정점 알파 Y-그라데이션, 진행선엔 더 밝은 붉은 기 `BrightColor`). RevealDur 1.6s.
  - ② **떨림(jitter)**: reveal 후반(p>0.7)부터 글자마다 Perlin 으로 따로 미세 흔들림(±1.7px) — 불안한 러브크래프트 톤.
  - ③ **글로우 호흡**: 타이틀 뒤 붉은 라디얼 글로우(절차생성 `RadialSprite`) 알파/스케일이 sin 으로 호흡.
  - `Time.unscaled*` 사용. `title.UpdateVertexData`로 매 프레임 반영.
- **BuildUI 구조 변경**: BG를 Root(CanvasGroup 페이드) 밖 캔버스 직속 **즉시 솔리드**로, 타이틀도 Root 밖 `BuildTitleFX`로 분리 → 타이틀이 페이드+reveal **이중 페이드** 안 되게. 글귀/스탯/버튼은 기존 1.3s 페이드인 유지.
- **검증**: 컴파일 에러 0. Play 후 런타임 상태 직접 읽어 확인 — reveal 완료 시 정점 alpha 255, glowAlpha 0.16/scale 1.018(호흡), jitter delta (-0.14,-0.57)px 매 프레임 변동. (스크린샷은 Unity 백그라운드라 게임뷰 미렌더로 캡처 불가 — 알려진 제약. 포그라운드에서 시각 확인 권장.)
- (B.중앙 시선 눈 / C.글귀 타자기 / D.비네트+그레인 연출은 보류 — 추후 추가 가능.)

## 2026-05-31 — [맥북] 게임오버 화면 신규 구현

- **목적**: 사망 시 조용히 NodeMap 리셋되던 것 → 슬더스식 깔끔한 게임오버 화면 추가.
- **새 `GameOverScene.cs` + `GameOverScene.unity`** (빌드세팅 등록):
  - "GAME OVER"(붉은색+아웃라인, 명조체) + **랜덤 사망 글귀 5종** 중 1개.
  - 스탯: 처치한 보스 / 골드 / 덱(N장). **덱 클릭 시 팝업**으로 현재 덱 카드 확인(낙인 노드 카드 그리드 재활용 — CardPrefab+CardUI, 보기 전용).
  - **타이틀로** 버튼: `ResetForTitle()`(세이브 삭제+런 완전 초기화) 후 FadeManager 로 MainMenu 페이드 이동. 진입 시 CanvasGroup 페이드 인.
  - UI 전부 코드 절차 생성(프로젝트 관례). 폰트는 AppleMyungjo SDF 연결.
- **`GameManager` 수정**:
  - `GameOver()` → 상태 보존한 채 `GameOverScene` 로드(화면에서 골드/보스/덱 표시해야 하므로). 초기화는 '타이틀로'가 수행.
  - `ResetForTitle()` 추가. Awake 에서 baseMaxHp/baseGold 캡처(초기화 기준값).
- **글리프 주의**: AppleMyungjo 엔 ▸/✕ 글리프 없음(두부) → 화살표 제거+"클릭하여 확인" 힌트, 닫기버튼 "X"로 교체.
- **검증**: Play 캡처로 레이아웃/글귀/글리프 확인 완료(에러 0). 실제 사망 흐름(전투에서 HP 0) 연결은 BattleManager.CheckPlayerDeath→GameOver() 그대로라 자동.

## 2026-05-31 — [맥북] 테스트 카드 + 폰트 아틀라스 커밋

- `Assets/Cards/Card_Test 1.asset` 추가: 시선 게이지 증가 테스트용 카드(effectType 5, gazeChange).
  - → 직후 삭제: 중복본이라 실제 사용하는 `Card_Test.asset`만 남김.
- `Assets/Fonts/AppleMyungjo SDF.asset` 갱신: 컷씬 텍스트용 글리프 아틀라스 재생성.
- (`.DS_Store`, `.claude/settings.local.json`은 게임 무관이라 커밋 제외)

## 2026-05-29 — [맥북] 회귀버그 수정: 리워드씬 카드 겹침

- 손패 수정 때 `CardUI.InitPosition`(레이아웃 적용 후 1프레임 뒤 위치 캡처) 제거 → 리워드/상점 카드(LayoutGroup 의존)가 전부 (0,0)에 겹침.
- **수정**: `InitPosition` 복구하되 `homeSet==false`(PlayerHand 가 SetHome 안 한 경우)에만 실행. 손패는 SetHome, 리워드/상점은 InitPosition → 둘 다 정상.
- **추가 회귀**: 호버 시 `SetAsLastSibling()`(맨 앞으로)이 LayoutGroup 활성인 리워드 카드에선 재배치(순서 뒤바뀜·겹침) 유발. → 활성 LayoutGroup(`!lg.enabled` 체크) 아래에선 SetAsLastSibling 건너뛰게 수정. 손패(LayoutGroup 비활성)는 그대로 실행.

## 2026-05-29 — [맥북] 전투씬 업그레이드 ④: 호버 + 하단바 배치 + 낙인노드 일러스트

- **카드 호버 확대**: `CardPrefab` 의 `hoverYOffset` 70→230, `hoverScale` 1.6→1.9 — 호버/드래그 시 손패 위로 크게 솟아 description 가독성 ↑. 호버 카드 `SetAsLastSibling`(맨 앞).
- **하단 우측 UI 배치**: 마나/EndTurn/덱/버림 텍스트가 흩어져 있고 덱 텍스트가 화면 밖(x900,200폭→1000>960)으로 잘리던 것 → 우측 하단 박스에 2×2 정렬(열 x620·830 / 행 y-435·-500). 손패는 우측으로 이동(HandArea x -150→30) + 폭 축소(`PlayerHand.maxHandWidth` 1500→1100)해 좌하단 HP바와 우측 클러스터 양쪽 회피. (정확한 박스 경계는 추정 — 별도 박스 오브젝트 없음, 배경 하단 어두운 띠. 미세조정은 Unity 포그라운드에서 스크린샷 확인 필요.)
- **낙인 노드 카드 비주얼**: `BrandNodeManager.BuildCardItem` 이 일러스트를 공용 `cardSprite` 필드(빈값)에서 가져와 전부 빈칸 + 코스트박스/이름박스도 없었음(수동 빌드라 누락). → **실제 `CardPrefab`(180×240, art+코스트박스+아이콘+이름+설명 전부 연결됨)을 인스턴스화 + `CardUI.Setup(card)`** 로 인게임 카드와 동일하게 표시. CardUI 는 Setup 후 `enabled=false`(드래그/Update 차단), `CanvasGroup.blocksRaycasts=false`(클릭은 외곽 프레임 버튼이 받음), 0.92배 축소(프레임 테두리=선택강조 보이게). `BrandNodeManager.cardPrefab` 필드 추가 후 BrandNodeScene 에서 CardPrefab 연결. (미연결 시 기존 수동 빌드 폴백)
- **주의(검증 한계)**: Unity 가 백그라운드면 게임뷰 렌더가 멈춰(frame 고정) `ScreenCapture` 가 안 떠 시각 검증 불가. 배틀씬 직접 Play 는 `BattleManager.Instance` 가 불안정. 실제 플레이로 확인 필요.

## 2026-05-29 — [맥북] 전투씬 업그레이드 ③: 손패 배치 + 플레이어 HP/방어도 UI

- **손패 버그 2종 수정 (원인: `HandArea`의 HorizontalLayoutGroup ↔ `CardUI.Update` 충돌)**:
  - CardUI 가 매 프레임 처음 잡힌 `originalPosition` 으로 lerp 하며 LayoutGroup 과 싸움 → ① 초기 드로우 시 (0,0)에 뭉침(레이스), ② 카드 사용 시 빈칸 안 메움.
  - **수정**: `HandArea`의 HorizontalLayoutGroup **비활성화**. `PlayerHand.ArrangeHand()` 가 가운데 정렬로 직접 배치(spacing 220, maxWidth 1500) — `RefreshHand`(스폰) + `RemoveCardFromHand`(사용) 때마다 호출. `CardUI.SetHome()` 로 home 지정 + `Update` 가 부드럽게 lerp. `CardUI.InitPosition` 코루틴 제거(범인). 호버 시 `SetAsLastSibling`(맨 앞).
  - **검증**: 3장 → home -220/0/220 균등 확산. 가운데 카드 제거 → 남은 2장 -110/110 재중앙정렬 ✓.
- **플레이어 HP/방어도 UI (활성은 screen-space `PlayerHpBarUI`. world-space `PlayerRuntimeUI`는 `PlayerHpBarUI.Start→DisableWorldSpacePlayerUI` 가 파괴함)**:
  - HP바 위치: 좌상단 → **좌하단(anchor 0,0 / pos 40,180), 손패 위**.
  - **방어도 배지 추가**: `DefenseBadgeUI`(방패 아이콘 `BattleIcons.Defense` + 숫자, >0일 때 슬라이드 인)를 HP바 좌상단(26,34)에 부착, `Update`에서 `SetValue(playerDefense)`.
  - **방어 시 HP색**: `playerDefense>0` 이면 fill 을 푸른색(0.62,0.82,1), 아니면 평소 빨강.
  - 검증: 배지 SetValue(8) → 방패+'8' 표시, fill 푸른색 적용 OK.
- 참고: HP바 위치(`PlayerHpBarUI.anchoredPosition`), 배지 위치, 방어색(`defendedFillColor`), 손패 간격(`PlayerHand.cardSpacing`) 모두 인스펙터 조절 가능.

## 2026-05-29 — [맥북] 전투씬 업그레이드 ② (피드백 반영)

- **시선 게이지**: y+410로 약간 위로. 채움 끝이 선긋듯 잘리던 것 → `RectMask2D.softness=(40,0)` 로 부드럽게 스며들게. 채움은 붉은(중앙)→흰(양끝) 그라데이션.
- **시선 로그 패널 리디자인**: 흰색 기본 UI → 다크 테마(패널 검정 α0.92, 증가=어두운 크림슨/감소=어두운 틸, 명조 폰트 + 아웃라인, 증가 크림슨/감소 시안 텍스트). 위치를 게이지 바로 아래(y+250) + 정중앙(x=0) 정렬.
- **시선 로그 합산**: 같은 사유 여러 번 → 합산 한 줄. `gazeChangeLog`(List) → `gazeChangeOrder`+`gazeChangeAmounts`(Dict) 누적 후 `BuildGazeChangeLog()`. (예: 금단의 시선 +10 ×2 → "금단의 시선 +20")
- **플레이어 피격 데미지 숫자**: 발밑 → 스프라이트 상단(머리/가슴, 앞쪽)으로. bounds 기반 적응 계산 + popupSpawnOffset 역보정.
- **플레이어 피격 넉백 — 핵심 버그 추적**:
  - 카메라 줌인(`PlayerHitCloseup`) 호출 제거(어색) → 대신 넉백/움찔 추가했는데 **미동도 안 함**.
  - **근본 원인**: Player 에 **런타임에 ParallaxLayer 가 추가**됨(정적 씬엔 Transform/SpriteRenderer/HitEffect 뿐). `ParallaxLayer.LateUpdate` 가 매 프레임 `localPosition = origin+parallax+shake` 로 덮어써서, `transform.position` 직접 이동(flinch)이 그 즉시 리셋됨. (동기 테스트선 LateUpdate 전이라 위치 변화가 잡혀서 오판했음 — set +3 → 다음 프레임 원위치 복귀로 확정)
  - **수정**: `ParallaxLayer` 에 `Knockback(dir, amount, duration)` + `hitOffset` 추가(LateUpdate 에서 합산). `CombatCameraEffect.PlayerFlinch` 가 ParallaxLayer 있으면 `Knockback(Vector2.left, ...)` 호출(없으면 transform 폴백). **몬스터 쉐이크(shakeOffset)와 동일한 LateUpdate-합산 경로**라 안 덮어써짐. `flinchAmount` 0.5/`flinchDuration` 0.28.
  - **테스트 환경 주의**: BattleScene 직접 Play 시 GameManager 흐름이 없어 `BattleManager.Instance` 가 불안정(null)하고, 에디터가 IntroScene/ MainMenu 로 자꾸 튀며, Play 세션이 frame=2로 자주 리셋됨 → 시각 확인이 어려움. 실제 플레이(정상 흐름)에서 확인 필요.

## 2026-05-29 — [맥북] 전투씬 업그레이드 ①: 시선 게이지 장식 UI + 히트스톱

- **목적**: 전투씬을 프로토타입 → 본격 연출로 업글. 1차로 시선 게이지 비주얼 + 타격감.
- **시선 게이지 (새 `GazeGaugeUI.cs`)**:
  - 사용자 제공 에셋 `시선게이지 검정색/흰색.png` → `Assets/Sprites/GazeGauge_Black.png` / `GazeGauge_White.png` (Single, maxSize 4096). 눈이 중앙에 있는 와이드 장식 라인(~18:1).
  - **구조**: 프레임(장식 통째로 항상 표시) + 채움 bar(프레임 뒤, 가운데→양옆 대칭으로 차오름) + backing(어두운 글로우, 밝은 하늘 가독성). 레이어: Backing→FillMask>Fill→Frame.
  - 채움은 `RectMask2D`(중앙 pivot) 폭을 0→fillFullWidth(1280)로 키워 reveal. 채움 스프라이트는 **붉은(중앙)→흰(양끝) 가로 + 세로 소프트글로우** 절차생성. backing도 절차생성.
  - **시선 100 도달 시 프레임 검정→흰색 스왑** (배경 적색/하늘 눈 연출 대비). 증가 시 펀치 스케일.
  - BattleScene Canvas에 배치(상단중앙 y+380, HUD 시선표시와 분리). 기존 `GazeBar` 슬라이더는 SetActive(false). `BattleUI.gazeGauge` 연결 + `UpdateUI()`에서 `SetGaze(gazeLevel)` 호출.
  - **중요 오해 정정**: 초기엔 프레임 자체를 잘라서(reveal) 차오르게 만들었는데, 실제 요구는 "프레임은 통째로 고정 + 안쪽에 별도 bar 채움"이었음 → 구조 재작업함.
  - Play 캡처로 0/50/100% 검증 완료(채움·색스왑·가독성 정상).
- **히트스톱 (`CombatCameraEffect.HitStop`)**: `Time.timeScale=0` → `WaitForSecondsRealtime(0.06)` → 1 복구. **피격(`BattleManager.DamagePlayer`)·타격(`Monster.TakeDamage`/`DirectDamage`)** 양쪽에서 호출. 기존 줌인/흰플래시/셰이크 위에 "턱!" 멈춤 추가. OnDestroy에서 timeScale 복구 안전장치.
  - 주의: MCP로만 깨작댈 땐 게임뷰 프레임이 안 돌아 코루틴 복구가 안 보였음(실제 플레이는 정상). timeScale은 에디터 Play 세션 간 유지되니 테스트 후 1 확인 필요.
- **알아둘 점(캡처)**: 스크린샷을 `Assets/` 안에 쓰면 에셋 임포트→도메인 리로드로 **Play가 종료됨**. 캡처는 `ScreenCapture.CaptureScreenshot`로 **프로젝트 루트(Assets 밖)** 에 쓸 것. Canvas가 Screen Space-Overlay라 카메라 스크린샷엔 UI 안 잡힘.

## 2026-05-29 — [맥북] 인트로 컷씬에 2번째 일러스트(물 반영) 추가 + 나레이션 박스 제거

- **목적**: 컷씬이 일러스트 1장이라 "컷씬 느낌"이 약함 → 두 번째 일러스트(`CutScene_2.png`, 결정질 투구 인물이 갈라진 바닥의 고인 물에 비친 모습)를 추가해 2막 구성.
- **새 이미지 임포트**: `Downloads/컷씬2.png`(1672×941, 16:9) → `Assets/Sprites/CutScene_2.png`. MCP `set_import_settings`가 빈값만 전달되는 버그 → `.meta` 직접 편집(`spriteMode: 2→1`, textureType Sprite)으로 Single 스프라이트 세팅. guid `702a6049...`.
- **`CutsceneManager.cs` 확장 (2이미지 + 플래시 컷)**:
  - `cutsceneImage2` 필드 추가. `Shot`에 `int image`(1/2) 추가.
  - 동선: [이미지1] ①~⑤ 기존 검은태양 세계관 → **플래시(번쩍) 컷** → [이미지2] 2-A 인물 등장("그리고 낙인을 짊어진 자가, 그 응시에 맞선다." — 기존 ⑥ 라인 재활용) → 2-B 물웅덩이로 틸트(무대사) → 2-C 물에 비친 실루엣 클로즈업 reveal(무대사) → 피날레.
  - `FocusToPos/ApplyTransform/MoveTo/Drift`를 대상 RectTransform 인자로 일반화. `FlashCut(from,to)`(흰 플래시 절정에서 알파 스왑) + `SetImageAlpha` 추가. 이미지2 포커스는 전부 물 반영에 맞추고 줌으로 우하단 촉수팔을 프레임 밖으로 크롭.
- **나레이션 검정 박스 제거**: 씬의 `BottomBar`(UI_Btn_SelectBar_black, 일러스트 가리던 박스)의 Image 컴포넌트 제거. `NarrationText` 강조(36→48pt, Bold, 흰색 + 검정 아웃라인 0.22)로 박스 없이도 가독성 확보. 더미텍스트 "asdads" 제거.
- **잡은 버그**: ① 복제한 CutsceneImage2의 RectTransform offset이 캔버스 중앙값(960,540)으로 들어가 rect가 0×0으로 찌그러짐 → offset 전부 0으로 풀스크린 복구. ② execute_code가 (Play 아닌)에디트 모드에서 돌아 이미지1 alpha를 0으로 꺼버린 것 복구.
- **검증**: 컴파일 에러 0. Play로 구도 스크린샷 확인 — 물 반영 포커스 + 촉수팔 크롭 정상. (`ScreenCapture`는 프레임 지연 있으니 transform set ↔ capture 호출 분리 필요. Canvas가 Screen Space-Overlay라 카메라 스크린샷엔 UI가 안 잡힘 → `ScreenCapture.CaptureScreenshot`로 전체화면 캡처해야 보임.)

### 같은 날 1차 피드백 반영
- **이미지2 카메라 무빙 축소**: 3샷(인물→틸트→클로즈업+피날레) → **2샷**으로. 초기구도(인물) → 물에 비친 후드로 1회 무빙(클로즈업 없이 같은 줌 1.7로 팬) → **3초 정지** → 맵으로. **피날레 번쩍 플래시 제거**(Beat.Finale 미사용). 끝은 FadeManager 페이드로 자연 전환.
- **나레이션 폰트 교체**: NotoSansKR("형식적"이라 게임톤과 안 맞음) → **AppleMyungjo(명조/세리프)**. `/System/Library/Fonts/Supplemental/AppleMyungjo.ttf` → `Assets/Fonts/AppleMyungjo.ttf` 복사 후 `TMP_FontAsset.CreateFontAsset`로 **동적 SDF 에셋**(`Assets/Fonts/AppleMyungjo SDF.asset`, 아틀라스/머티리얼 서브에셋 포함) 생성. NarrationText에 적용(48pt/Bold/검정 아웃라인 0.2). **폰트 교체 시 머티리얼 새로 생겨 아웃라인이 0으로 리셋되므로 재설정 필요**.
- **엠대시 이슈 자동 해결**: AppleMyungjo엔 `—` 글리프가 있어 ⑤번 "빛도 — 모두..."가 이제 정상 표시(폰트 경고 사라짐).
- **주의(라이선스)**: AppleMyungjo는 Apple 시스템 폰트 → 게임 **배포 시엔 오픈 폰트(예: 나눔명조)로 교체** 필요. 발표/내부용은 무방.

## 2026-05-29 — [맥북] MCP 세션 자동연결 ON + 연결 절차 정정

- **증상**: 맥북에서 MCP 도구 호출이 `no_unity_session`으로 계속 실패. Unity MCP 창의 "Python" 빨간불.
- **원인**: 이 버전(CoplayDev unity-mcp **v9.6.6**)은 "서버"와 "세션"이 별개. 파이썬 HTTP 서버(8080)는 떠 있어도 **Unity↔서버 세션**이 따로 붙어야 함. HTTP transport는 `Auto-Start on Load` 토글이 **기본 꺼짐**이라 세션을 수동으로 열어야 했음. (stdio는 항상 자동.) Play 테스트 시 도메인 리로드로 세션이 끊기는 것도 원인.
- **조치**: `EditorPrefs MCPForUnity.AutoStartOnLoad = true` 로 설정 → 이제 에디터 열 때 서버+세션 자동 연결.
- **연결 절차 정정 (중요 — CLAUDE.md의 "Start Server/Start Bridge"는 v9에서 틀린 명칭)**:
  - MCP 창 열기: `Cmd+Shift+M` (메뉴: `Window → MCP For Unity → Toggle MCP Window`)
  - 수동 연결 시 Connection 섹션의 **`Start Session`** 클릭 (상태가 `Session Active (dead_hand)` 면 정상).
  - 세션 살았는지 확인: `mcpforunity://instances` 리소스의 `instance_count`.

## 2026-05-29 — [맥북] 인트로 컷씬 전면 재작업 (시네마틱)

- **목적**: 내일 학술대회 발표용 인게임 플레이 영상 — 인트로~전투 흐름을 다듬는 중. 그 첫 단계로 컷씬(인트로→노드맵) 연출이 너무 어색해서 전면 교체.
- **이미지 교체**: 기존 `CutScene_1.jpg`(2752×1400, 마을/사제 그림) → 새 `CutScene_1.png`(820×460=16:9, 후드 인물이 검은 태양을 바라보는 구도). **guid(`1c585463...`) 유지**한 채 에셋만 교체 → 씬의 Image 참조(`fileID 21300000`) 안 건드리고 그대로 연결됨.
- **`CutsceneManager.cs` 통째로 재작성**:
  - 카메라 좌표를 픽셀 매직넘버 → **이미지 정규화 좌표(-0.5~0.5)** 로. 런타임에 rect 크기로 환산(해상도 독립).
  - 동선: 와이드 → 후드인물 → 검은태양 → 눈(핵) 클로즈업 → 무너진 성채 → 와이드+피날레. **순간이동 컷 전부 제거**, 전부 ease-in-out + 정지 샷에도 느린 켄번스 드리프트.
  - 연출 오버레이 전부 코드 생성: 레터박스(2.35:1), 비네트(절차적 라디얼 텍스처), 필름 그레인(노이즈 RawImage), 붉은 시선 펄스, 번개 플래시(태양 등장), 화이트 피날레 플래시 + 카메라 셰이크.
  - 나레이션 6줄 새로 작성(STIGMA 테마: 응시/낙인). ESC 길게=스킵 게이지 기능 유지.
- **미검증**: 맥북 MCP 세션이 `no_unity_session`으로 계속 끊겨 **컴파일 에러 확인 / Play 테스트를 못 함**. Unity 콘솔에서 컴파일 에러 없는지 + Cutscene 씬 Play로 동선/박자 직접 확인 필요. 좌표·타이밍은 플레이 보면서 미세 튜닝 예정.

## 2026-05-29 — [맥북] Unity MCP 연결 방식 stdio → HTTP(8080) 정렬

- **증상**: Unity 창에서 "파이썬 로드 안 됨" / 기능 자체가 안 됨. `manage_scene get_active` → `No Unity Editor instances found`.
- **원인**: 맥북 Claude는 UnityMCP를 **stdio**로 등록 → Claude가 stdio 서버를 *따로* 띄움. 반면 **Unity는 HTTP 모드(`http://127.0.0.1:8080`)** 로 자기 서버를 띄우고 브리지를 거기 연결. 두 서버가 서로 다른 프로세스라 stdio 서버는 Unity를 `0 instances`로만 봄 = **전송 방식 불일치**.
- **조치**: 데스크탑 환경(HTTP 8080)과 동일하게 맞춤.
  - `claude mcp remove UnityMCP -s local`
  - `claude mcp add --transport http UnityMCP http://127.0.0.1:8080/mcp -s local`
- **주의**: 재등록 후 **Claude Code 세션 재시작 필요**(세션은 시작 시점 연결을 유지). 재시작 후 `get_active`로 검증할 것.

## 2026-05-29 — [맥북] 환경 세팅 & 협업 규칙 수립

- 맥북에서 처음 작업 시작 (기존엔 데스크탑 전용).
- **Unity MCP 연동**: `uv`/`uvx` 설치(`~/.local/bin`), UnityMCP 서버를 Claude Code에 등록 — `mcpforunityserver==9.6.6` (Unity 패키지 버전과 일치), stdio transport, local scope. `claude mcp get UnityMCP` → ✓ Connected.
  - 실제 Unity 조작은 에디터에서 `Window → MCP for Unity → Start Bridge` 켜야 동작.
- **협업 규칙 수립** (CLAUDE.md에 명문화): ①선컨펌 후작업 ②버그 단번에+원인설명 ③모르면 질문.
- **크로스 머신 작업 공유**: 이 `WORKLOG.md` 도입. 양쪽 환경에서 작업 내역을 여기에 기록하기로 함.
