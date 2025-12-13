namespace DPorch.Runtime.Python;

/// <summary>
///     Redirects Python stdout/stderr output. Can be directly assigned to sys.stdout/stderr.
/// </summary>
internal class PythonStdOutRedirect
{
    private bool _isEndLine;

    /// <summary>
    ///     Called by Python when text is written to the stream.
    /// </summary>
    public void write(string text)
    {
        try
        {
            // Python calls once to print string and a second time to print a platform-dependent end that line
            if (_isEndLine)
            {
                _isEndLine = false;

                return;
            }

            // Using Console.Write instead of Console.WriteLine helps keep terminal endline behavior consistent
            Console.WriteLine(text);
            _isEndLine = true;
        }
        catch
        {
            // Console IO can be finicky and we don't want the pipeline to crash over it
            // It's likely not the fault of DPorch, so we ignore it
        }
    }

    /// <summary>
    ///     Called by Python when the stream should be flushed.
    /// </summary>
    public void flush()
    {
        // No-op: Console.WriteLine already flushes
    }
}