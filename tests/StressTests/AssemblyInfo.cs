using Xunit;

// Stress tests each spin a live CopyEngineHost background loop and drive it to quiescence under a wall
// clock. Running them in parallel starves the host tasks of CPU and turns convergence timeouts flaky,
// so the whole assembly runs serially — deterministic, at the cost of wall-clock time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
