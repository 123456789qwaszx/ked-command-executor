// 줄 태그 — 넘김. 박스 태그(#boxhide …)와 따로 둔다: 대사창 모양이 아니라 "다음 줄로 언제 가나"의 일이다.
//
//   #auto   줄이 다 보이면(타자기 끝 · 그 줄 앞머리의 연출 명령 끝) 입력 없이 다음으로 넘긴다.
//           연출만 하는 줄(#boxhide)을 클릭 없이 지나가게 할 때 쓴다 — 노드의 마지막 줄이면 그대로 노드가 끝난다.
//           자동 진행(Auto)의 대기 시간은 쓰지 않는다 — 다 보인 다음 프레임에 넘긴다.
//           재생 입력이 멈춘 동안(메뉴 · 팝업 · UI 숨김)은 기다린다. 넘기는 길이 재생 입력의 프레임(VNFeatureController.Tick)이다.
//           되감기로 닿은 줄에서는 넘기지 않는다 — 다시 보려고 돌아온 것이다(VNLinePresentationFlow).
//
// 표기는 박스 태그와 같은 정규화를 거친다(#auto · #Auto · #AUTO가 같다).
public static class VNLineAdvanceTag
{
    private const string Auto = "auto";

    public static bool IsAuto(string[] metadata)
    {
        if (metadata == null)
            return false;

        for (int i = 0; i < metadata.Length; i++)
        {
            if (DialogueBoxTagNormalizer.Normalize(metadata[i]) == Auto)
                return true;
        }

        return false;
    }
}
