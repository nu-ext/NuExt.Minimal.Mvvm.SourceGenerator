; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID  | Category | Severity | Notes
---------|----------|----------|-------
NUEXT001 | Usage    | Error    | Conflicting command attributes
NUEXT002 | Usage    | Error    | Command infrastructure method is declared manually
NUEXT003 | Usage    | Error    | Invalid command method signature
NUEXT004 | Usage    | Error    | Invalid notify method signature
NUEXT005 | Usage    | Error    | Invalid notify field declaration
NUEXT006 | Usage    | Error    | Invalid notify property declaration
NUEXT007 | Usage    | Error    | Invalid containing type
NUEXT008 | Usage    | Error    | Invalid containing type for notify data error info
NUEXT009 | Usage    | Error    | INotifyDataErrorInfo is not implemented
NUEXT010 | Usage    | Error    | Invalid localize configuration
NUEXT011 | Usage    | Error    | Invalid generate commands type
NUEXT012 | Usage    | Error    | Command can-execute method not found
NUEXT013 | Usage    | Error    | Invalid command can-execute method signature
NUEXT014 | Usage    | Error    | Command type not found
NUEXT015 | Usage    | Error    | Duplicate command method name
NUEXT016 | Usage    | Error    | Duplicate generated property name
NUEXT017 | Usage    | Error    | Generated property is already declared
NUEXT018 | Usage    | Error    | Invalid localization file