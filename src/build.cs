using System.Xml;
var filter = args.FirstOrDefault("");
var index_files = new Dictionary<string, StreamWriter>();
foreach (var path in Directory.EnumerateFiles(@"..\sitemaps\", filter)) {
    var filename = Path.GetFileNameWithoutExtension(path);
    var name_start = filename.IndexOf("_", 8);
    var cat = filename.Substring(8, name_start - 8);
    var name = filename.Substring(name_start + 1);
    Console.WriteLine($"{cat} {name}");

    var xml = new XmlDocument();
    xml.Load(path);

    var index_name = $"..\\docs\\{cat}.html";
    if (string.IsNullOrEmpty(filter)) {
        if (!index_files.ContainsKey(index_name)) {
            index_files[index_name] = new StreamWriter(index_name, false, System.Text.Encoding.UTF8);
        }
    }
    var index = index_files.GetValueOrDefault(index_name);

    using var content = new StreamWriter($"..\\docs\\{cat}_{name}.html", false, System.Text.Encoding.UTF8);

    var first = true;
    var urls = xml.GetElementsByTagName("url");
    foreach (XmlElement elem in urls) {
        var video = elem.GetElementsByTagName("video:video").Item(0) as XmlElement;
        var loc = elem.GetElementsByTagName("loc").Item(0)?.InnerText;
        var title = elem.GetElementsByTagName("title").Item(0)?.InnerText;
        if (string.IsNullOrEmpty(title)) { title = name.Replace('+', ' '); }
        var thumb = elem.GetElementsByTagName("thumbnail_loc").Item(0)?.InnerText;
        var desc = elem.GetElementsByTagName("description").Item(0)?.InnerText;

        if (first) {
            first = false;
            if (index is object) {
                if (index.BaseStream.Position == 0) {
                    index.WriteLine(
                        "<head><link rel='stylesheet' href='styles.css'></head>"
                    );
                }
                index.WriteLine(
                    $"<nobr id='{name}'>" +
                    $"<a href='http://web.archive.org/web/2020/{loc}' target='_blank'><img src='logo_archive-sm.png' width=24 height=24></a> " +
                    $"<a href='{cat}_{name}.html' target='content'>{title}</a> ({((cat == "Posts") ? urls.Count : (urls.Count - 1))})" +
                    "</nobr><br/>"
                );
                index.Flush();
            }

            content.WriteLine(
                "<head><link rel='stylesheet' href='styles.css'></head>"
            );
            if (cat == "Posts") {
                content.WriteLine(
                    $"<nobr><h2>{cat} - {title}</h2></nobr><br/>"
                );
            } else {
                content.Write(
                    "<nobr><h2>" +
                    $"<a href='http://web.archive.org/web/2020/{loc}' target='_blank'><img src='logo_archive-sm.png' width=24 height=24></a> " +
                    $"{cat} - {title}" +
                    "</h2></nobr>"
                );
                if (!(string.IsNullOrEmpty(thumb) && string.IsNullOrEmpty(desc))) {
                    content.Write(
                        ((thumb is object) ? $"<img class='thumb' src='{thumb}'/>" : "") +
                        $"<div class='desc'>{(System.Web.HttpUtility.HtmlDecode(desc))}</div>" +
                        "<br clear='right'/>"
                    );
                }
                content.WriteLine("<br/>");
                continue;
            }
        }
        {
            var v_title = video?.GetElementsByTagName("video:title").Item(0)?.InnerText;
            var v_desc = video?.GetElementsByTagName("video:description").Item(0)?.InnerText;
            var v_loc = video?.GetElementsByTagName("video:content_loc").Item(0)?.InnerText;
            var v_date = video?.GetElementsByTagName("video:publication_date").Item(0)?.InnerText;
            var v_thumb = video?.GetElementsByTagName("video:thumbnail_loc").Item(0)?.InnerText;
            var v_time = long.Parse(video?.GetElementsByTagName("video:duration").Item(0)?.InnerText ?? "-1");

            v_thumb = v_thumb?.Replace("http://video.ch9.ms/", "https://sec.ch9.ms/");
            v_loc = v_loc?.Replace("http://video.ch9.ms/", "https://sec.ch9.ms/");

            if (string.IsNullOrWhiteSpace(v_title)) {
                if (!string.IsNullOrWhiteSpace(loc)) {
                    v_title = $"[{new System.Uri(loc).Segments.Last()}]";
                } else {
                    v_title = "[Title Missing]";
                }
            }
            content.WriteLine(
                "<nobr>" +
                $"<a href='http://web.archive.org/web/2020/{loc}' target='_blank'><img src='logo_archive-sm.png' width=24 height=24></a> " +
                (string.IsNullOrEmpty(v_loc) ? System.Web.HttpUtility.HtmlDecode(v_title) : $"<a href='{v_loc}' target='_blank'>{System.Web.HttpUtility.HtmlDecode(v_title)}</a>") +
                ((v_time >= 0) ? $" [{v_time / 3600}:{(v_time % 3600) / 60:D2}:{v_time % 60:D2}]" : "") +
                ((v_date is object) ? DateTime.Parse(v_date).ToString(" [yyyy/MM/dd]") : "") +
                "</nobr><br/>" +
                ((v_thumb is object) ? $"<img class='vthumb' src='{v_thumb}'/>" : "") +
                $"<div class='vdesc'>{(System.Web.HttpUtility.HtmlDecode(v_desc))}</div>" +
                "<br clear='left'/><br/>"
            );
        }
    }
}
foreach (var (_, index) in index_files) {
    index.Flush();
    index.Close();
    index.Dispose();
}