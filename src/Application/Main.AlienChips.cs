using Godot;
using Earthward.Domain;

namespace Earthward.Application;

public partial class Main
{
    private long _unreportedAlienChips;
    private ulong _nextAlienChipNoticeAt;

    private void OnAlienChipDropped(DataMap payload)
    {
        _unreportedAlienChips += payload.L("alien_chips", 1);
    }

    private void UpdateAlienChipNotice()
    {
        // The permanent profile has already saved the claim. Aggregate loot feedback
        // without rebuilding the sidebar or replacing combat and tutorial notices.
        if (_unreportedAlienChips <= 0 || NoticeTime > 0 || Modal != "" || Time.GetTicksMsec() < _nextAlienChipNoticeAt)
            return;
        ShowNotice($"回收外星芯片 +{_unreportedAlienChips} · 可在特性背包购买与永久强化");
        _unreportedAlienChips = 0;
        _nextAlienChipNoticeAt = Time.GetTicksMsec() + 12000;
    }
}
