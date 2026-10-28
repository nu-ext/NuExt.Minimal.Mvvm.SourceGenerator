namespace Minimal.Mvvm.SourceGenerator;

partial struct LocalizePropertyGenerator
{
    private static string GetLoadCodeSource(string assignments) =>
        $$"""
        /// <summary>
        /// Loads localized strings from the specified JSON file.
        /// </summary>
        /// <param name="fileName">The localization JSON file name.</param>
        public static void LoadFromJsonFile(string fileName)
        {
            _ = fileName ?? throw new global::System.ArgumentNullException(nameof(fileName));
            using var stream = global::System.IO.File.OpenRead(fileName);
            using var document = global::System.Text.Json.JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != global::System.Text.Json.JsonValueKind.Object)
            {
                throw new global::System.IO.InvalidDataException("The localization file must contain a JSON object.");
            }
            LoadFromJsonElement(document.RootElement, path: null);

            return;

            void LoadFromJsonElement(global::System.Text.Json.JsonElement element, string? path)
            {
                foreach (var property in element.EnumerateObject())
                {
                    var propertyPath = string.IsNullOrEmpty(path) ? property.Name : $"{path}.{property.Name}";
                    switch (property.Value.ValueKind)
                    {
                        case global::System.Text.Json.JsonValueKind.String:
                            SetLocalizedString(propertyPath, property.Value.GetString()!);
                            break;
                        case global::System.Text.Json.JsonValueKind.Object:
                            LoadFromJsonElement(property.Value, propertyPath);
                            break;
                        default:
                            throw new global::System.IO.InvalidDataException($"Localization entry '{propertyPath}' must be a string or an object.");
                    }
                }
            }

            void SetLocalizedString(string path, string value)
            {
                switch (path)
                {
        {{assignments}}
                    default:
                        break;
                }
            }
        }
        """;

    private static string GetStringCodeSource(string mappings) =>
        $$"""
        internal static string GetLocalizedString(string path)
        {
            return path switch
            {
        {{mappings}}
                _ => path
            };
        }
        """;

    private static string GetMarkupExtensionCodeSource(string markupExtensionName, string localizationTypeName) =>
        $$"""
        /// <summary>
        /// Provides localized strings from <see cref="{{localizationTypeName}}"/>.
        /// </summary>
        public sealed class {{markupExtensionName}}Extension : global::System.Windows.Markup.MarkupExtension
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="{{markupExtensionName}}Extension"/> class.
            /// </summary>
            /// <param name="path">The localization path.</param>
            public {{markupExtensionName}}Extension(string path)
            {
                Path = path;
            }

            /// <summary>
            /// Gets the localization path.
            /// </summary>
            [global::System.Windows.Markup.ConstructorArgument("path")]
            public string Path { get; }

            /// <inheritdoc/>
            public override object ProvideValue(global::System.IServiceProvider serviceProvider)
            {
                return {{localizationTypeName}}.GetLocalizedString(Path);
            }
        }
        """;
}