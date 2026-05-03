using UnityEngine;
using System.Collections;

// 보스 (또는 특수 패턴 몬스터) 의 AI 훅. Monster 에 컴포넌트로 부착되며
// MonsterData.bossAIType 가 None 이 아니면 BattleManager.SpawnEncounter 가 자동 AddComponent.
//
// 일반 몬스터는 IBossAI 가 없고 MonsterData.actionPool 의 Random/Sequential 로직을 그대로 사용.
// IBossAI 가 있으면 PickNextAction / ExecuteAction / 인텐트 표시를 모두 위임한다.
public interface IBossAI
{
    // 전투 시작 시 1회 호출 (Monster.InitializeForBattle 끝부분)
    void OnBattleStart(Monster m);

    // 다음에 무엇을 할지 결정하고 Monster 의 nextAction (혹은 자체 상태) 을 갱신.
    // 인텐트 UI 표시 직전에 호출됨.
    void PrepareNextAction(Monster m);

    // 자기 턴 행동 실행. 멀티히트 등 시간이 걸리는 액션을 위해 코루틴 반환.
    // BattleManager.MonsterTurn 코루틴이 yield 로 기다린다.
    IEnumerator ExecuteAction(Monster m, BattleManager bm);

    // 인텐트 UI 텍스트 (예: "공격 13 (+개안1)"). MonsterRuntimeUI 가 호출.
    string GetIntentText(Monster m);

    // 인텐트 아이콘 스프라이트 (BattleIcons 등 사용 가능). null 이면 폴백.
    Sprite GetIntentIcon(Monster m);

    // 인텐트 아이콘 색상 (스프라이트 없을 때 폴백 컬러로 사용)
    Color GetIntentColor(Monster m);
}
