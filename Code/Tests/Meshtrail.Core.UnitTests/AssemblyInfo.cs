// Unit tests share no state, so every test method may run in parallel.
[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]
