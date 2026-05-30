namespace KopiaWinUI3.Services;

public sealed record KopiaCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError)
{
    public bool Succeeded => ExitCode == 0;

    public string DisplayText
    {
        get
        {
            var text = string.Join(
                Environment.NewLine,
                new[] { StandardOutput.Trim(), StandardError.Trim() }.Where(static value => !string.IsNullOrWhiteSpace(value)));

            return string.IsNullOrWhiteSpace(text) ? $"kopia exited with code {ExitCode}" : text;
        }
    }
}
