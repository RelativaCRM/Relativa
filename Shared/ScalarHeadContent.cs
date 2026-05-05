namespace Relativa.Scalar;

/// <summary>
/// HTML for Scalar <c>AddHeadContent</c>: wrap long JSON/code in the docs UI instead of overflowing horizontally.
/// </summary>
internal static class ScalarHeadContent
{
    public const string WrapLongLinesInReference = """
        <style>
          #app pre,
          #app code {
            white-space: pre-wrap !important;
            word-break: break-word;
            overflow-wrap: anywhere;
            max-width: 100%;
          }
          #app pre {
            overflow-x: hidden;
          }
        </style>
        """;
}
