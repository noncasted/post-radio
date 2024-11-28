using Cysharp.Threading.Tasks;

namespace Global.Publisher
{
    public interface IAds
    {
        UniTask ShowInterstitial();
        UniTask<RewardAdResult> ShowRewarded();
    }
}