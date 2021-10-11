﻿using System.Text;
using Pixeval.CommunityToolkit.Markdown.Parsers.Markdown.Enums;

namespace Pixeval.CommunityToolkit.Markdown.Parsers.Markdown.Blocks
{
    /// <summary>
    /// Represents a block of text that is displayed in a fixed-width font.  Inline elements and
    /// escape sequences are ignored inside the code block.
    /// </summary>
    public class CodeBlock : MarkdownBlock
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CodeBlock"/> class.
        /// </summary>
        public CodeBlock()
            : base(MarkdownBlockType.Code)
        {
        }

        /// <summary>
        /// Gets or sets the source code to display.
        /// </summary>
        public string? Text { get; set; }

        /// <summary>
        /// Gets or sets the Language specified in prefix, e.g. ```c# (GitHub Style Parsing).<para/>
        /// This does not guarantee that the Code Block has a language, or no language, some valid code might not have been prefixed, and this will still return null. <para/>
        /// To ensure all Code is Highlighted (If desired), you might have to determine the language from the provided string, such as looking for key words.
        /// </summary>
        public string? CodeLanguage { get; set; }

        /// <summary>
        /// Parses a code block.
        /// </summary>
        /// <param name="markdown"> The markdown text. </param>
        /// <param name="start"> The location of the first character in the block. </param>
        /// <param name="maxEnd"> The location to stop parsing. </param>
        /// <param name="quoteDepth"> The current nesting level for block quoting. </param>
        /// <param name="actualEnd"> Set to the end of the block when the return value is non-null. </param>
        /// <returns> A parsed code block, or <c>null</c> if this is not a code block. </returns>
        internal static CodeBlock? Parse(string markdown, int start, int maxEnd, int quoteDepth, out int actualEnd)
        {
            StringBuilder? code = null;
            actualEnd = start;
            var insideCodeBlock = false;
            var codeLanguage = string.Empty;

            /*
                Two options here:
                Either every line starts with a tab character or at least 4 spaces
                Or the code block starts and ends with ```
            */

            foreach (var lineInfo in Helpers.Common.ParseLines(markdown, start, maxEnd, quoteDepth))
            {
                var pos = lineInfo.StartOfLine;
                if (pos < maxEnd && markdown[pos] == '`')
                {
                    var backTickCount = 0;
                    while (pos < maxEnd && backTickCount < 3)
                    {
                        if (markdown[pos] == '`')
                        {
                            backTickCount++;
                        }
                        else
                        {
                            break;
                        }

                        pos++;
                    }

                    if (backTickCount == 3)
                    {
                        insideCodeBlock = !insideCodeBlock;

                        if (!insideCodeBlock)
                        {
                            actualEnd = lineInfo.StartOfNextLine;
                            break;
                        }
                        else
                        {
                            // Collects the Programming Language from the end of the starting ticks.
                            while (pos < lineInfo.EndOfLine)
                            {
                                codeLanguage += markdown[pos];
                                pos++;
                            }
                        }
                    }
                }

                if (!insideCodeBlock)
                {
                    // Add every line that starts with a tab character or at least 4 spaces.
                    if (pos < maxEnd && markdown[pos] == '\t')
                    {
                        pos++;
                    }
                    else
                    {
                        var spaceCount = 0;
                        while (pos < maxEnd && spaceCount < 4)
                        {
                            if (markdown[pos] == ' ')
                            {
                                spaceCount++;
                            }
                            else if (markdown[pos] == '\t')
                            {
                                spaceCount += 4;
                            }
                            else
                            {
                                break;
                            }

                            pos++;
                        }

                        if (spaceCount < 4)
                        {
                            // We found a line that doesn't start with a tab or 4 spaces.
                            // But don't end the code block until we find a non-blank line.
                            if (lineInfo.IsLineBlank == false)
                            {
                                break;
                            }
                        }
                    }
                }

                // Separate each line of the code text.
                if (code == null)
                {
                    code = new StringBuilder();
                }
                else
                {
                    code.AppendLine();
                }

                if (lineInfo.IsLineBlank == false)
                {
                    // Append the code text, excluding the first tab/4 spaces, and convert tab characters into spaces.
                    var lineText = markdown.Substring(pos, lineInfo.EndOfLine - pos);
                    var startOfLinePos = code.Length;
                    for (var i = 0; i < lineText.Length; i++)
                    {
                        var c = lineText[i];
                        if (c == '\t')
                        {
                            code.Append(' ', 4 - ((code.Length - startOfLinePos) % 4));
                        }
                        else
                        {
                            code.Append(c);
                        }
                    }
                }

                // Update the end position.
                actualEnd = lineInfo.StartOfNextLine;
            }

            if (code == null)
            {
                // Not a valid code block.
                actualEnd = start;
                return null;
            }

            // Blank lines should be trimmed from the start and end.
            return new CodeBlock()
            {
                Text = code.ToString().Trim('\r', '\n'),
                CodeLanguage = !string.IsNullOrWhiteSpace(codeLanguage) ? codeLanguage.Trim() : null
            };
        }

        /// <summary>
        /// Converts the object into it's textual representation.
        /// </summary>
        /// <returns> The textual representation of this object. </returns>
        public override string? ToString()
        {
            return Text;
        }
    }
}