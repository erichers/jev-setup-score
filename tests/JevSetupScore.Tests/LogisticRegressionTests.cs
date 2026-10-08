using JevSetupScore.Core;

namespace JevSetupScore.Tests;

public class LogisticRegressionTests
{
    [Fact]
    public void Training_Converges_AndRecoversSigns()
    {
        var random = new Random(1);
        const int n = 600;
        var features = new double[n][];
        var labels = new int[n];
        double[] trueWeights = [0.3, 1.8, -2.2];

        for (var i = 0; i < n; i++)
        {
            var x1 = random.NextDouble() * 4 - 2;
            var x2 = random.NextDouble() * 4 - 2;
            features[i] = [x1, x2];
            var logit = trueWeights[0] + trueWeights[1] * x1 + trueWeights[2] * x2;
            var probability = 1.0 / (1.0 + Math.Exp(-logit));
            labels[i] = random.NextDouble() < probability ? 1 : 0;
        }

        var model = LogisticRegression.Train(features, labels, l2: 0.05, maxIterations: 30, standardize: false);

        Assert.True(model.Converged);
        Assert.True(model.FinalLoss < model.InitialLoss);
        Assert.InRange(model.Iterations, 1, 30);
        Assert.True(model.Weights[0] > 0.8);
        Assert.True(model.Weights[1] < -0.8);

        var correct = 0;
        for (var i = 0; i < n; i++)
        {
            var predicted = model.PredictProbability(features[i]) >= 0.5 ? 1 : 0;
            if (predicted == labels[i])
                correct++;
        }

        Assert.True(correct > 0.8 * n);
    }
}
