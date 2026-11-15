using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace BankarskiSistem.LoadTests
{
    public sealed class EndpointMetrics
    {
        private readonly ConcurrentBag<double> _latencies = [];
        private int _failures;
        private int _businessRejections;

        public string Name { get; }

        public EndpointMetrics(string name)
        {
            Name = name;
        }

        public int Count => _latencies.Count;
        public int Failures => _failures;
        public int BusinessRejections => _businessRejections;

        public void Record(double milliseconds, int statusCode)
        {
            _latencies.Add(milliseconds);
            if (statusCode == 422)
                Interlocked.Increment(ref _businessRejections);
            else if (statusCode >= 400 || statusCode == 0)
                Interlocked.Increment(ref _failures);
        }

        public double Percentile(double percentile)
        {
            var sorted = _latencies.OrderBy(l => l).ToArray();
            if (sorted.Length == 0)
                return 0;
            var index = (int)Math.Ceiling(percentile / 100d * sorted.Length) - 1;
            return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
        }

        public double Average => _latencies.IsEmpty ? 0 : _latencies.Average();
    }

    public sealed class ScenarioResult
    {
        private readonly ConcurrentDictionary<string, EndpointMetrics> _endpoints = new();

        public string Name { get; }
        public int VirtualUsers { get; }
        public TimeSpan Elapsed { get; set; }

        public ScenarioResult(string name, int virtualUsers)
        {
            Name = name;
            VirtualUsers = virtualUsers;
        }

        public IEnumerable<EndpointMetrics> Endpoints => _endpoints.Values.OrderBy(e => e.Name);

        public void Record(string endpoint, double milliseconds, int statusCode) =>
            _endpoints.GetOrAdd(endpoint, name => new EndpointMetrics(name)).Record(milliseconds, statusCode);

        public int TotalRequests => _endpoints.Values.Sum(e => e.Count);
        public int TotalFailures => _endpoints.Values.Sum(e => e.Failures);
        public double Throughput => Elapsed.TotalSeconds == 0 ? 0 : TotalRequests / Elapsed.TotalSeconds;

        public string ToMarkdown()
        {
            var culture = CultureInfo.InvariantCulture;
            var builder = new StringBuilder();
            builder.AppendLine($"### {Name} ({VirtualUsers} virtuelnih korisnika, {Elapsed.TotalSeconds.ToString("0.0", culture)} s)");
            builder.AppendLine();
            builder.AppendLine($"Ukupno zahteva: **{TotalRequests}**, propusnost: **{Throughput.ToString("0.0", culture)} zahteva/s**, greške: **{TotalFailures}**");
            builder.AppendLine();
            builder.AppendLine("| Endpoint | Zahteva | Greške | Poslovno odbijeno (422) | Prosek ms | p50 ms | p95 ms | p99 ms |");
            builder.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|");
            foreach (var endpoint in Endpoints)
            {
                builder.AppendLine(string.Create(culture,
                    $"| {endpoint.Name} | {endpoint.Count} | {endpoint.Failures} | {endpoint.BusinessRejections} | {endpoint.Average:0.0} | {endpoint.Percentile(50):0.0} | {endpoint.Percentile(95):0.0} | {endpoint.Percentile(99):0.0} |"));
            }
            builder.AppendLine();
            return builder.ToString();
        }
    }

    public static class Timing
    {
        public static async Task<(T Value, double Milliseconds)> MeasureAsync<T>(Func<Task<T>> action)
        {
            var stopwatch = Stopwatch.StartNew();
            var value = await action();
            return (value, stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}
