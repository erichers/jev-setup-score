namespace JevSetupScore.Core;

public static class WalkForward
{
    public readonly record struct Fold(int TrainFeatureStart, int TrainFeatureEnd, int TestStart, int TestEnd);

    /// <summary>
    /// A training row is eligible only when its label bar is strictly before the test window.
    /// The label bar is featureBarIndex + horizon, so this is the no-lookahead rule.
    /// </summary>
    public static bool IsEligibleTrain(int featureBarIndex, int horizon, int testStartBarIndex) =>
        featureBarIndex + horizon < testStartBarIndex;

    public static IReadOnlyList<Fold> Create(int sampleCount, int horizon, int trainWindow, int testWindow)
    {
        if (sampleCount < 0)
            throw new ArgumentOutOfRangeException(nameof(sampleCount));
        if (horizon < 1)
            throw new ArgumentOutOfRangeException(nameof(horizon));
        if (trainWindow < 1)
            throw new ArgumentOutOfRangeException(nameof(trainWindow));
        if (testWindow < 1)
            throw new ArgumentOutOfRangeException(nameof(testWindow));

        var folds = new List<Fold>();
        var testStart = trainWindow + horizon;
        while (testStart + testWindow <= sampleCount)
        {
            var trainFeatureEnd = testStart - horizon - 1;
            var trainFeatureStart = trainFeatureEnd - trainWindow + 1;
            if (trainFeatureStart < 0)
                trainFeatureStart = 0;
            folds.Add(new Fold(trainFeatureStart, trainFeatureEnd, testStart, testStart + testWindow));
            testStart += testWindow;
        }

        return folds;
    }
}
