// Uploads the mod to the Steam Workshop for Escape from Duckov (app 3167020).
//
// Does what the game's built-in uploader does (title, description, "Mod" tag, public visibility,
// preview.png, content folder) and additionally sets per-language titles/descriptions and the
// screenshot gallery, which the in-game uploader cannot do.
//
// Usage (Steam must be running and logged in to an account that owns the game):
//   WorkshopUploader --content <mod folder> --docs <docs/workshop> [--id <publishedFileId>]
//                    [--screenshots] [--note "change note"] [--write-id <info.ini> ...]
//
// --content      folder uploaded as the mod (must contain only shipped files: dll, info.ini, preview.png...)
// --docs         folder with description.{en,zh,ja,ko}.txt and screenshots/*.jpg|png
// --id           existing item to update; omitted = create a new item
// --screenshots  add docs/screenshots/* to the gallery (only on first upload, otherwise they are added again)
// --write-id     info.ini files that get "publishedFileId = <id>" after the item is created
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Steamworks;

internal static class Program
{
    private const uint AppId = 3167020;

    // Steam API language code -> (description file, title). The base (default) text is English.
    private static readonly (string steamLang, string file, string title)[] Localized =
    {
        ("schinese", "description.zh.txt", "鸭科夫字体管理器 Duckov Font Manager"),
        ("tchinese", "description.zh.txt", "鸭科夫字体管理器 Duckov Font Manager"),
        ("japanese", "description.ja.txt", "Duckov Font Manager（フォントマネージャー）"),
        ("koreana", "description.ko.txt", "Duckov Font Manager (글꼴 관리자)"),
    };
    private const string BaseTitle = "Duckov Font Manager";
    private const string BaseFile = "description.en.txt";

    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            var opt = Options.Parse(args);
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);
            File.WriteAllText("steam_appid.txt", AppId.ToString());

            if (!SteamAPI.Init())
            {
                Console.Error.WriteLine("SteamAPI.Init failed. Is Steam running and logged in?");
                return 2;
            }
            Console.WriteLine($"Steam user: {SteamFriends.GetPersonaName()}");
            try { return Run(opt); }
            finally { SteamAPI.Shutdown(); }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }

    private static int Run(Options opt)
    {
        string content = Path.GetFullPath(opt.Content);
        string docs = Path.GetFullPath(opt.Docs);
        string preview = Path.Combine(content, "preview.png");
        Require(Directory.Exists(content), "content folder not found: " + content);
        Require(File.Exists(preview), "preview.png missing in content folder");
        Require(File.Exists(Path.Combine(content, "DuckovFontManager.dll")), "DuckovFontManager.dll missing in content folder");
        foreach (var f in Directory.GetFiles(content, "*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(f).ToLowerInvariant();
            Require(ext != ".ttf" && ext != ".otf" && ext != ".ttc" && !f.EndsWith("config.json", StringComparison.OrdinalIgnoreCase),
                "refusing to upload fonts/config: " + f);
        }
        Console.WriteLine("Content: " + string.Join(", ", Directory.GetFiles(content).Select(Path.GetFileName)));

        ulong id = opt.Id;
        if (id == 0)
        {
            var created = Wait<CreateItemResult_t>(SteamUGC.CreateItem(new AppId_t(AppId), EWorkshopFileType.k_EWorkshopFileTypeFirst), "CreateItem");
            Require(created.m_eResult == EResult.k_EResultOK, "CreateItem failed: " + created.m_eResult);
            id = created.m_nPublishedFileId.m_PublishedFileId;
            Console.WriteLine($"Created item {id}");
            if (created.m_bUserNeedsToAcceptWorkshopLegalAgreement)
                Console.WriteLine("NOTE: the Steam Workshop legal agreement must be accepted before the item becomes visible.");
            foreach (var ini in opt.WriteId.Append(Path.Combine(content, "info.ini")).Distinct(StringComparer.OrdinalIgnoreCase))
                WritePublishedFileId(ini, id);
        }
        var fileId = new PublishedFileId_t(id);

        // 1) Content, preview, gallery and the base (English) text.
        var h = SteamUGC.StartItemUpdate(new AppId_t(AppId), fileId);
        SteamUGC.SetItemTitle(h, BaseTitle);
        SteamUGC.SetItemDescription(h, ReadDescription(docs, BaseFile));
        SteamUGC.SetItemVisibility(h, ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic);
        SteamUGC.SetItemTags(h, new List<string> { "Mod" }, false); // same tag the game's uploader uses
        Require(SteamUGC.SetItemPreview(h, preview), "SetItemPreview failed");
        Require(SteamUGC.SetItemContent(h, content), "SetItemContent failed");
        if (opt.Screenshots)
        {
            var dir = Path.Combine(docs, "screenshots");
            foreach (var img in Directory.GetFiles(dir).Where(f => f.EndsWith(".jpg") || f.EndsWith(".png")).OrderBy(f => f))
            {
                Require(new FileInfo(img).Length < 1024 * 1024, "screenshot must be < 1 MB: " + img);
                Require(SteamUGC.AddItemPreviewFile(h, img, EItemPreviewType.k_EItemPreviewType_Image), "AddItemPreviewFile failed: " + img);
                Console.WriteLine("  + screenshot " + Path.GetFileName(img));
            }
        }
        Submit(h, opt.Note, "content + English text");

        // 2) Localized titles/descriptions.
        foreach (var (lang, file, title) in Localized)
        {
            var hl = SteamUGC.StartItemUpdate(new AppId_t(AppId), fileId);
            Require(SteamUGC.SetItemUpdateLanguage(hl, lang), "SetItemUpdateLanguage failed: " + lang);
            SteamUGC.SetItemTitle(hl, title);
            SteamUGC.SetItemDescription(hl, ReadDescription(docs, file));
            Submit(hl, "", lang + " text");
        }

        Console.WriteLine($"Done: https://steamcommunity.com/sharedfiles/filedetails/?id={id}");
        return 0;
    }

    private static void Submit(UGCUpdateHandle_t h, string note, string what)
    {
        var call = SteamUGC.SubmitItemUpdate(h, note ?? "");
        var r = Wait<SubmitItemUpdateResult_t>(call, "SubmitItemUpdate (" + what + ")", () =>
        {
            var status = SteamUGC.GetItemUpdateProgress(h, out ulong done, out ulong total);
            return total > 0 ? $"{status} {done}/{total}" : status.ToString();
        });
        Require(r.m_eResult == EResult.k_EResultOK, $"Submit ({what}) failed: {r.m_eResult}");
        if (r.m_bUserNeedsToAcceptWorkshopLegalAgreement)
            Console.WriteLine("NOTE: accept the Steam Workshop legal agreement to make the item visible.");
        Console.WriteLine($"Submitted {what}.");
    }

    private static T Wait<T>(SteamAPICall_t call, string what, Func<string> progress = null)
    {
        bool done = false, ioFailure = false;
        T result = default;
        using var cr = CallResult<T>.Create((r, failure) => { result = r; ioFailure = failure; done = true; });
        cr.Set(call);
        var start = DateTime.UtcNow;
        var lastReport = DateTime.MinValue;
        while (!done)
        {
            SteamAPI.RunCallbacks();
            Thread.Sleep(50);
            if (progress != null && (DateTime.UtcNow - lastReport).TotalSeconds >= 2)
            {
                lastReport = DateTime.UtcNow;
                Console.WriteLine($"  {what}: {progress()}");
            }
            if ((DateTime.UtcNow - start).TotalMinutes > 15) throw new TimeoutException(what);
        }
        if (ioFailure) throw new IOException(what + ": Steam IO failure");
        return result;
    }

    private static string ReadDescription(string docs, string file)
    {
        var text = File.ReadAllText(Path.Combine(docs, file), Encoding.UTF8).Replace("\r\n", "\n").Trim();
        Require(Encoding.UTF8.GetByteCount(text) <= 8000, file + " exceeds Steam's 8000-byte description limit");
        return text;
    }

    private static void WritePublishedFileId(string iniPath, ulong id)
    {
        if (!File.Exists(iniPath)) return;
        var lines = File.ReadAllLines(iniPath, Encoding.UTF8)
            .Where(l => !l.TrimStart().StartsWith("publishedFileId", StringComparison.OrdinalIgnoreCase))
            .ToList();
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1])) lines.RemoveAt(lines.Count - 1);
        lines.Add($"publishedFileId = {id}");
        File.WriteAllText(iniPath, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
        Console.WriteLine("Wrote publishedFileId to " + iniPath);
    }

    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }

    private sealed class Options
    {
        public string Content, Docs, Note = "";
        public ulong Id;
        public bool Screenshots;
        public List<string> WriteId = new List<string>();

        public static Options Parse(string[] a)
        {
            var o = new Options();
            for (int i = 0; i < a.Length; i++)
            {
                switch (a[i])
                {
                    // Resolve now: Main later switches the working directory to the exe folder.
                    case "--content": o.Content = Path.GetFullPath(a[++i]); break;
                    case "--docs": o.Docs = Path.GetFullPath(a[++i]); break;
                    case "--id": o.Id = ulong.Parse(a[++i]); break;
                    case "--note": o.Note = a[++i]; break;
                    case "--screenshots": o.Screenshots = true; break;
                    case "--write-id": o.WriteId.Add(Path.GetFullPath(a[++i])); break;
                    default: throw new ArgumentException("Unknown argument: " + a[i]);
                }
            }
            if (o.Content == null || o.Docs == null) throw new ArgumentException("--content and --docs are required");
            return o;
        }
    }
}
