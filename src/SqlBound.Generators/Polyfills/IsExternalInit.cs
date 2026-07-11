// Polyfill: records and init-only setters require this marker type, which netstandard2.0 lacks.
// Analyzer assemblies may carry no package dependencies, so it is declared here instead of
// referencing a polyfill package.

namespace System.Runtime.CompilerServices;

internal static class IsExternalInit;
