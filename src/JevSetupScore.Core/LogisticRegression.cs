namespace JevSetupScore.Core;

public sealed class LogisticModel
{
    public required double Intercept { get; init; }
    public required double[] Weights { get; init; }
    public required double[] Means { get; init; }
    public required double[] Stds { get; init; }
    public required bool Standardized { get; init; }
    public required int Iterations { get; init; }
    public required bool Converged { get; init; }
    public required double InitialLoss { get; init; }
    public required double FinalLoss { get; init; }

    public double PredictProbability(double[] rawFeatures)
    {
        var z = Logit(rawFeatures);
        return Sigmoid(z);
    }

    public double Logit(double[] rawFeatures)
    {
        var scaled = Scale(rawFeatures);
        var z = Intercept;
        for (var j = 0; j < Weights.Length; j++)
            z += Weights[j] * scaled[j];
        return z;
    }

    public double[] Contributions(double[] rawFeatures)
    {
        var scaled = Scale(rawFeatures);
        var contributions = new double[Weights.Length];
        for (var j = 0; j < Weights.Length; j++)
            contributions[j] = Weights[j] * scaled[j];
        return contributions;
    }

    private double[] Scale(double[] rawFeatures)
    {
        if (rawFeatures.Length != Weights.Length)
            throw new ArgumentException("Feature length does not match the model.", nameof(rawFeatures));

        var scaled = new double[rawFeatures.Length];
        for (var j = 0; j < rawFeatures.Length; j++)
        {
            if (!Standardized)
            {
                scaled[j] = rawFeatures[j];
                continue;
            }

            var std = Stds[j];
            scaled[j] = std < 1e-12 ? 0 : (rawFeatures[j] - Means[j]) / std;
        }

        return scaled;
    }

    public static double Sigmoid(double z)
    {
        if (z >= 30)
            return 1;
        if (z <= -30)
            return 0;
        return 1.0 / (1.0 + Math.Exp(-z));
    }
}

public static class LogisticRegression
{
    public static LogisticModel Train(
        double[][] features,
        int[] labels,
        double l2 = 1.0,
        int maxIterations = 25,
        bool standardize = true,
        double tolerance = 1e-7)
    {
        if (features.Length == 0)
            throw new ArgumentException("Training data is empty.", nameof(features));
        if (features.Length != labels.Length)
            throw new ArgumentException("Features and labels must have the same length.");

        var n = features.Length;
        var d = features[0].Length;
        var means = new double[d];
        var stds = new double[d];

        if (standardize)
        {
            for (var j = 0; j < d; j++)
            {
                double sum = 0;
                for (var i = 0; i < n; i++)
                    sum += features[i][j];
                means[j] = sum / n;
                double varSum = 0;
                for (var i = 0; i < n; i++)
                {
                    var delta = features[i][j] - means[j];
                    varSum += delta * delta;
                }

                var std = Math.Sqrt(varSum / n);
                stds[j] = std < 1e-8 ? 1 : std;
            }
        }
        else
        {
            Array.Fill(stds, 1);
        }

        var x = new double[n][];
        for (var i = 0; i < n; i++)
        {
            if (features[i].Length != d)
                throw new ArgumentException("Every row must have the same number of features.");
            var row = new double[d];
            for (var j = 0; j < d; j++)
                row[j] = standardize && stds[j] >= 1e-12 ? (features[i][j] - means[j]) / stds[j] : features[i][j];
            x[i] = row;
        }

        var beta = new double[d + 1];
        var initial = MeanLogLoss(x, labels, beta);
        var converged = false;
        var iterations = 0;

        for (var iter = 1; iter <= maxIterations; iter++)
        {
            iterations = iter;
            var p = new double[n];
            var w = new double[n];
            for (var i = 0; i < n; i++)
            {
                var pi = LogisticModel.Sigmoid(Dot(beta, x[i]));
                pi = Math.Clamp(pi, 1e-8, 1 - 1e-8);
                p[i] = pi;
                w[i] = pi * (1 - pi);
            }

            var dim = d + 1;
            var hessian = new double[dim, dim];
            var gradient = new double[dim];
            for (var i = 0; i < n; i++)
            {
                var residual = labels[i] - p[i];
                gradient[0] += residual;
                for (var a = 0; a < d; a++)
                {
                    gradient[a + 1] += x[i][a] * residual;
                    hessian[0, a + 1] += w[i] * x[i][a];
                    hessian[a + 1, 0] += w[i] * x[i][a];
                    for (var b = a; b < d; b++)
                    {
                        var value = w[i] * x[i][a] * x[i][b];
                        hessian[a + 1, b + 1] += value;
                        if (a != b)
                            hessian[b + 1, a + 1] += value;
                    }
                }

                hessian[0, 0] += w[i];
            }

            // Newton step on the L2-penalized log likelihood. Intercept is not penalized.
            for (var j = 1; j < dim; j++)
            {
                gradient[j] -= l2 * beta[j];
                hessian[j, j] += l2;
            }

            for (var j = 0; j < dim; j++)
                hessian[j, j] += 1e-6;

            var step = Solve(hessian, gradient);
            double maxStep = 0;
            for (var j = 0; j < dim; j++)
            {
                beta[j] += step[j];
                maxStep = Math.Max(maxStep, Math.Abs(step[j]));
            }

            if (maxStep < tolerance)
            {
                converged = true;
                break;
            }
        }

        var weights = new double[d];
        Array.Copy(beta, 1, weights, 0, d);
        return new LogisticModel
        {
            Intercept = beta[0],
            Weights = weights,
            Means = means,
            Stds = stds,
            Standardized = standardize,
            Iterations = iterations,
            Converged = converged,
            InitialLoss = initial,
            FinalLoss = MeanLogLoss(x, labels, beta),
        };
    }

    private static double Dot(double[] beta, double[] row)
    {
        var z = beta[0];
        for (var j = 0; j < row.Length; j++)
            z += beta[j + 1] * row[j];
        return z;
    }

    private static double MeanLogLoss(double[][] x, int[] y, double[] beta)
    {
        double loss = 0;
        for (var i = 0; i < x.Length; i++)
        {
            var p = Math.Clamp(LogisticModel.Sigmoid(Dot(beta, x[i])), 1e-12, 1 - 1e-12);
            loss += y[i] == 1 ? -Math.Log(p) : -Math.Log(1 - p);
        }

        return loss / x.Length;
    }

    private static double[] Solve(double[,] a, double[] b)
    {
        var n = b.Length;
        var m = new double[n, n + 1];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
                m[i, j] = a[i, j];
            m[i, n] = b[i];
        }

        for (var col = 0; col < n; col++)
        {
            var pivot = col;
            var best = Math.Abs(m[col, col]);
            for (var r = col + 1; r < n; r++)
            {
                var value = Math.Abs(m[r, col]);
                if (value > best)
                {
                    best = value;
                    pivot = r;
                }
            }

            if (best < 1e-14)
                throw new InvalidOperationException("The logistic Hessian is singular.");

            if (pivot != col)
            {
                for (var j = col; j <= n; j++)
                    (m[col, j], m[pivot, j]) = (m[pivot, j], m[col, j]);
            }

            var div = m[col, col];
            for (var j = col; j <= n; j++)
                m[col, j] /= div;

            for (var r = 0; r < n; r++)
            {
                if (r == col)
                    continue;
                var factor = m[r, col];
                if (factor == 0)
                    continue;
                for (var j = col; j <= n; j++)
                    m[r, j] -= factor * m[col, j];
            }
        }

        var solution = new double[n];
        for (var i = 0; i < n; i++)
            solution[i] = m[i, n];
        return solution;
    }
}
