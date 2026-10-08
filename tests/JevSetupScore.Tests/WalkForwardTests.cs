using JevSetupScore.Core;

namespace JevSetupScore.Tests;

public class WalkForwardTests
{
    [Fact]
    public void Split_KeepsTrainingLabelsBeforeTheTestWindow()
    {
        const int horizon = 10;
        const int trainWindow = 120;
        const int testWindow = 30;
        var folds = WalkForward.Create(500, horizon, trainWindow, testWindow);

        Assert.NotEmpty(folds);
        Assert.True(folds[1].TestStart > folds[0].TestStart);

        foreach (var fold in folds)
        {
            Assert.Equal(trainWindow, fold.TrainFeatureEnd - fold.TrainFeatureStart + 1);
            Assert.Equal(testWindow, fold.TestEnd - fold.TestStart);
            Assert.True(fold.TrainFeatureEnd + horizon < fold.TestStart);
            Assert.Equal(fold.TestStart, fold.TrainFeatureEnd + horizon + 1);
            Assert.True(WalkForward.IsEligibleTrain(fold.TrainFeatureEnd, horizon, fold.TestStart));
            Assert.False(WalkForward.IsEligibleTrain(fold.TrainFeatureEnd + 1, horizon, fold.TestStart));
            Assert.True(fold.TrainFeatureEnd < fold.TestStart);
        }
    }

    [Fact]
    public void Split_IsEmpty_WhenHistoryIsShorterThanOneWindow()
    {
        var folds = WalkForward.Create(sampleCount: 40, horizon: 5, trainWindow: 30, testWindow: 20);
        Assert.Empty(folds);
    }
}
