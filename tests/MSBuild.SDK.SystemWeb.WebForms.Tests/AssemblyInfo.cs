// The generator keeps a process-wide document cache; the caching tests observe its counters,
// so test classes must not run concurrently.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
