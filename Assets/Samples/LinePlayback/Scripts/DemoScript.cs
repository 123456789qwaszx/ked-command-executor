using static MiniYarn.DialogueRunner.Content;

// 이 샘플이 재생하는 내용 — 대본 파일 대신 코드에 적었다. 한 묶음 = 명령 넷 + 대사 한 줄.
//
// 명령은 그 자리에서 실행되지 않는다. YarnCommandBridge가 스펙으로 바꿔 YarnPlaybackDriver에 쌓고,
// 다음 대사 줄에 들어갈 때 VNLinePresentationFlow가 PlayCollected로 한 스텝에 재생한다(줄 하나 = 스텝 하나).
//
// 명령: place_left|center|right <slot> <focus> <시간> [wait] · fade_in|fade_out <slot> <시간> · face <slot> <표정>
//       pause <초> · <Nfr>(24fps 대기) · bgm <키> <시간> · stop_bgm <시간>
public static class DemoScript
{
    public const string NodeName = "LinePlayback_Demo";

    public static readonly MiniYarn.DialogueRunner.Content[] Contents =
    {
        // l01 — 줄 앞머리 대기(24fr)가 끝날 때까지 Space는 거절된다.
        Cmd("bgm town_theme 2s"),
        Cmd("place_left s1 bust 0fr"),
        Cmd("fade_in s1 18fr"),
        Cmd("24fr"),
        Say("l01", "하린", "명령 넷이 이 줄에 들어오는 순간 한 스텝으로 재생됐어. 24fr이 끝날 때까지 넘길 수 없어."),

        // l02 — 3초짜리 배경 이동. 다 가기 전에 넘기면 다음 줄의 스텝 경계가 Finish로 끝낸다.
        Cmd("place_center s1 bust 72fr"),
        Cmd("place_right s2 bust 0fr"),
        Cmd("fade_in s2 14fr"),
        Cmd("face s2 smile"),
        Say("l02", "시구레", "하린이 3초 동안 걸어가요. 다 가기 전에 넘기면 다음 줄이 Finish로 끝내요."),

        // l03 — 기다리는 이동(wait). 여기서 R: 루트부터 다시 달려 l02까지 압축 시크.
        Cmd("face s1 smile"),
        Cmd("place_left s1 bust 36fr wait"),
        Cmd("fade_in s3 14fr"),
        Cmd("place_center s3 bust 0fr"),
        Say("l03", "카스미", "하린의 이동은 기다리는 명령이야. 여기서 R을 누르면 루트부터 다시 달려 앞 줄로 돌아가."),

        // l04 — 긴 pause 뒤에 명령이 하나 더. 글자가 다 나온 뒤 pause가 끝나기 전에 Ctrl / K.
        Cmd("fade_out s2 14fr"),
        Cmd("face s3 smile"),
        Cmd("pause 3"),
        Cmd("face s3 surprised"),
        Say("l04", "카스미", "글자가 다 나온 뒤, 3초 pause가 끝나기 전에 Ctrl이나 K를 눌러 봐."),

        // l05 — 감사 결과 보기.
        Cmd("face s3 neutral"),
        Cmd("face s1 serious"),
        Cmd("place_right s1 bust 24fr"),
        Cmd("12fr"),
        Say("l05", "하린", "AUDIT을 봐. 앞 줄이 끝나기 전에 이 줄이 시작됐다면 겹침과 늦은 쓰기가 잡혔을 거야."),

        // l06 — 대사창 없이 퇴장 연출, 다 보이면 입력 없이 넘어가 노드가 끝난다.
        Cmd("fade_out s1 12fr"),
        Cmd("fade_out s3 12fr"),
        Cmd("stop_bgm 2s"),
        Cmd("24fr"),
        Say("l06", null, "(퇴장)", "boxhide", "auto"),
    };
}
