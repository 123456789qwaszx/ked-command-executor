using System;
using UnityEngine;

[Serializable]
public sealed class VNAdvanceInputBindings
{
    [Header("Advance")]
    public KeyCode advance = KeyCode.Space;

    [Header("Rapid Skip")]
    public KeyCode rapidSkipLeft = KeyCode.LeftControl;
    public KeyCode rapidSkipRight = KeyCode.RightControl;

    [Header("SpeedUp Mode")]
    public KeyCode speedUpHold = KeyCode.None;
    public KeyCode speedUpToggle = KeyCode.S;

    [Header("VN Features")]
    public KeyCode autoToggle = KeyCode.A;
    public KeyCode rollback = KeyCode.R;

    // 예전의 키 2~7(노드 실행·에피소드 사슬·진행 시작·새 게임·슬롯 저장/불러오기)은 읽는 곳이 없어 걷었다.
    // 그 일은 타이틀과 시스템 메뉴가 한다(framework-scope 4단계).

    public bool IsRapidSkipHeld() => IsHeld(rapidSkipLeft) || IsHeld(rapidSkipRight);
    public bool IsSpeedUpHeld() => IsHeld(speedUpHold);
    public bool IsSpeedUpTogglePressed() => IsPressed(speedUpToggle);
    public bool IsAutoTogglePressed() => IsPressed(autoToggle);
    public bool IsRollbackPressed() => IsPressed(rollback);
    public bool IsAdvancePressed() => IsPressed(advance);


    private static bool IsHeld(KeyCode key)
    {
        return key != KeyCode.None && Input.GetKey(key);
    }

    private static bool IsPressed(KeyCode key)
    {
        return key != KeyCode.None && Input.GetKeyDown(key);
    }
}
