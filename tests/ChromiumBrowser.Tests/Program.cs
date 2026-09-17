using ChromiumBrowser.Core;
using ChromiumBrowser.Core.Profile;

int failures = 0;
int total = 0;

void Check(string name, bool passed, string detail = "")
{
    total++;
    if (passed)
    {
        Console.WriteLine($"PASS  {name}");
        return;
    }

    failures++;
    Console.WriteLine($"FAIL  {name}{(detail.Length > 0 ? $" ({detail})" : string.Empty)}");
}

// ------------------------------------------------------------------ profile

{
    ProfileLocation portable = ProfileLocator.Resolve(
        @"E:\stick\browser", @"C:\Users\someone\AppData\Local", _ => true);
    Check("profile: a writable folder keeps the data beside the program",
        portable.IsPortable && portable.Path == @"E:\stick\browser\Data", portable.Path);

    ProfileLocation installed = ProfileLocator.Resolve(
        @"C:\Program Files\Browser", @"C:\Users\someone\AppData\Local", _ => false);
    Check("profile: a folder it may not write to falls back to the user's own",
        !installed.IsPortable
        && installed.Path == Path.Combine(@"C:\Users\someone\AppData\Local", Branding.FolderName),
        installed.Path);

    string temporary = Path.Combine(Path.GetTempPath(), $"cb-{Guid.NewGuid():N}");
    Check("profile: the writability test answers by actually writing",
        ProfileLocator.CanWrite(temporary));
    Check("profile: and says no for a drive that is not there",
        !ProfileLocator.CanWrite(@"Z:\nowhere\at\all"));
    Directory.Delete(temporary, true);
}

Console.WriteLine();
Console.WriteLine($"{total - failures}/{total} passed");
return failures == 0 ? 0 : 1;
