using System;
using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;

namespace Global.Systems
{
    public static class ProgressionExtensions
    {
        public static IProgression CreateProgression(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            float time,
            Action<float> callback,
            ProgressionLoop loop = ProgressionLoop.Frame)
        {
            return loop switch
            {
                ProgressionLoop.Frame => new UpdateProgression(lifetime, updater, time, callback),
                ProgressionLoop.Fixed => new FixedProgression(lifetime, updater, time, callback),
                _ => throw new ArgumentOutOfRangeException(nameof(loop), loop, null)
            };
        }

        public static async UniTask Progression(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            float time,
            Action<float> callback,
            ProgressionLoop loop = ProgressionLoop.Frame)
        {
            var handle = updater.CreateProgression(lifetime, time, callback, loop);
            await handle.Process();
        }
        
        

        public static UniTask CurveDeltaProgression(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            ICurve curve,
            ProgressionLoop loop,
            Action<float> callback)
        {
            var previousEvaluation = 0f;
            return updater.Progression(lifetime, curve.Time, Callback, loop);

            void Callback(float progress)
            {
                var evaluation = curve.Evaluate(progress);
                var delta = evaluation - previousEvaluation;
                previousEvaluation = evaluation;
                callback?.Invoke(delta);
            }
        }

        public static UniTask CurveProgression(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            AnimationCurve curve,
            float time,
            Action<float> callback)
        {
            return updater.Progression(lifetime, time, Callback);

            void Callback(float progress)
            {
                var factor = curve.Evaluate(progress);
                callback?.Invoke(factor);
            }
        }

        public static UniTask CurveProgression(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            Curve curve,
            Action<float> callback)
        {
            return updater.CurveProgression(lifetime, curve.Animation, curve.Time, callback);
        }

        public static UniTask TrajectoryProgression(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            TrajectoryCurve curve,
            Action<float, float> callback)
        {
            var instance = curve.CreateInstance();
            return updater.Progression(lifetime, curve.Time, Callback);

            void Callback(float progress)
            {
                var (move, height) = instance.Step(progress);
                callback?.Invoke(move, height);
            }
        }

        public static UniTask TrajectoryMoveProgression(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            TrajectoryCurve curve,
            Transform transform,
            Vector2 targetPosition,
            float maxHeight)
        {
            var instance = curve.CreateInstance();
            var origin = (Vector2)transform.position;
            return updater.Progression(lifetime, curve.Time, Callback);

            void Callback(float progress)
            {
                var (moveFactor, heightFactor) = instance.Step(progress);
                var position = Vector2.Lerp(origin, targetPosition, moveFactor);
                var height = maxHeight * heightFactor;
                position.y += height;

                transform.position = position;
            }
        }

        public static UniTask MoveProgression(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            Transform transform,
            Vector2 targetPosition,
            Curve curve)
        {
            var startPosition = (Vector2)transform.position;
            return updater.Progression(lifetime, curve.Time, OnProgress);

            void OnProgress(float progress)
            {
                var move = curve.Evaluate(progress);
                var position = Vector2.Lerp(startPosition, targetPosition, move);
                transform.position = position;
            }
        }

        public static UniTask FlickProgression(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            float time,
            float flickStep,
            Action<bool> callback)
        {
            var nextFlickTime = 0f;
            var flick = false;

            return updater.Progression(lifetime, time, Handle);

            void Handle(float process)
            {
                if (process < nextFlickTime)
                    return;

                callback?.Invoke(flick);
                nextFlickTime += flickStep;
                flick = !flick;
            }
        }
    }
}