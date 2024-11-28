using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Global.Publisher
{
    public interface ILeaderboardsProvider
    {
        void SetScore(ILeaderboardLink link, int score);

        UniTask<IReadOnlyList<LeaderboardUser>> GetLeaderBoard(
            ILeaderboardLink link,
            int quantityTop,
            int quantityAround,
            CancellationToken cancellation);
    }
}