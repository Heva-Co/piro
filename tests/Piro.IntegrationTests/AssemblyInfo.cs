using Xunit;

// Each integration test class provisions its own Postgres via Testcontainers, but wires the API to it by
// setting PROCESS-GLOBAL environment variables (Database__ConnectionString, Auth__JwtSecret, ...) in its
// IAsyncLifetime setup. Environment variables are shared across the whole process, so running test classes
// in parallel lets them clobber each other's connection string and a test can end up talking to another
// class's database — surfacing as flaky DbUpdateExceptions. Serialize the assembly so only one class (and
// therefore one active connection string) runs at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
