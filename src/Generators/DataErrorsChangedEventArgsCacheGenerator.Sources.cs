namespace Minimal.Mvvm.SourceGenerator;

partial struct DataErrorsChangedEventArgsCacheGenerator
{
    private static string GetCodeSource(string nullable, string eventArgsCache) => $$"""
        internal static partial class {{eventArgsCache}}
        {
            private static readonly global::System.Collections.Concurrent.ConcurrentDictionary<string, global::System.ComponentModel.DataErrorsChangedEventArgs> s_cache =
                new(global::System.StringComparer.Ordinal);
        
            private static readonly global::System.ComponentModel.DataErrorsChangedEventArgs s_allPropsChanged = new(string.Empty);
        
            public static global::System.ComponentModel.DataErrorsChangedEventArgs Get(string{{nullable}} propertyName) => propertyName is not { Length: > 0 } ? s_allPropsChanged
                : s_cache.GetOrAdd(propertyName, static name => new global::System.ComponentModel.DataErrorsChangedEventArgs(name));
        }
        """;
}
