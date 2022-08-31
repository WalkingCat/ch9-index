using System.Xml;
using System.Net.Http;

var out_dir = @"..\docs\";
var filter = args.FirstOrDefault("");
var has_filter = !string.IsNullOrEmpty(filter);
var indexes = new Dictionary<string, Tuple<StringWriter, StringWriter>>();
var http = new HttpClient();

foreach (var path in Directory.EnumerateFiles(@"..\sitemaps\", filter))
{
    var filename = Path.GetFileNameWithoutExtension(path);
    var name_start = filename.IndexOf("_", 8);
    var cat = filename.Substring(8, name_start - 8);
    var name = filename.Substring(name_start + 1);
    Console.WriteLine($"{cat} {name}");

    var xml = new XmlDocument();
    xml.Load(path);

    var index_name = $"{out_dir}{cat}.html";
    if (!has_filter)
    {
        if (!indexes.ContainsKey(index_name))
        {
            indexes[index_name] = new(new StringWriter(), new StringWriter());
        }
    }
    var index_writers = indexes.GetValueOrDefault(index_name);

    using var content = new StreamWriter($"..\\docs\\{cat}_{name}.html", false, System.Text.Encoding.UTF8);

    var first = true;
    var urls = xml.GetElementsByTagName("url");
    var arc_date = "2020";
    var featured = false;

    foreach (XmlElement elem in urls)
    {
        var video = elem.GetElementsByTagName("video:video").Item(0) as XmlElement;
        var loc = elem.GetElementsByTagName("loc").Item(0)?.InnerText;
        var loc_name = Path.GetFileName(loc);
        var title = elem.GetElementsByTagName("title").Item(0)?.InnerText;
        if (string.IsNullOrEmpty(title)) { title = name.Replace('+', ' '); }
        var desc = elem.GetElementsByTagName("description").Item(0)?.InnerText;
        var thumb = elem.GetElementsByTagName("thumbnail_loc").Item(0)?.InnerText;

        if (first)
        {
            featured = elem.GetElementsByTagName("featured").Item(0)?.InnerText == "true";
            if (featured && !string.IsNullOrEmpty(thumb) && !thumb.StartsWith("."))
            {
                var local_thumb = "./thumbnails/" + Path.ChangeExtension($"{cat}_{name}", Path.GetExtension(thumb));
                if (has_filter) DownloadFile(thumb, local_thumb);
                thumb = local_thumb;
            }

            arc_date = (elem.GetElementsByTagName("archive_date").Item(0)?.InnerText) ?? arc_date;

            var link = (cat == "Posts") ? $"https://channel9.msdn.com/Niners/{name}/Posts" : loc;

            first = false;
            if (index_writers is object)
            {
                var index = featured ? index_writers.Item1 : index_writers.Item2;
                index.WriteLine(
                     $"<nobr id='{name}' class='title-container{(featured ? " featured" : "")}'>" +
                     $"<a href='http://web.archive.org/web/{arc_date}/{link}' target='_blank'><img src='logo_archive-sm.png' width=24 height=24></a> " +
                     $"<span class='title'><a href='{cat}_{name}.html' target='content' class='title'>{title}</a> ({((cat == "Posts") ? urls.Count : (urls.Count - 1))})</span>" +
                     $"<a class='permalink' href='index.html?p={cat}_{name}' target='_top'>#</a>" +
                     "</nobr>"
                 );
                index.Flush();
            }

            content.WriteLine("<head><link rel='stylesheet' href='styles.css'></head><body class='content'>");
            content.Write(
                "<nobr class='title-container'><h2>" +
                $"<a href='http://web.archive.org/web/{arc_date}/{link}' target='_blank'><img src='logo_archive-sm.png' width=24 height=24></a> " +
                $"<span class='title'>{cat} - {title}</span>" +
                "</h2></nobr>"
            );
            if (!(string.IsNullOrEmpty(thumb) && string.IsNullOrEmpty(desc)))
            {
                content.Write(
                    (string.IsNullOrEmpty(thumb) ? "" : $"<img class='thumb' src='{thumb}'/>") +
                    $"<div class='desc'>{(System.Web.HttpUtility.HtmlDecode(desc))}</div>" +
                    (string.IsNullOrEmpty(thumb) ? "" : "<br clear='right'/>")
                );
            }
            content.WriteLine("<br/>");
            if (cat != "Posts")
            {
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
            v_thumb = v_thumb?.Replace("http://files.channel9.msdn.com/", "https://f.ch9.ms/");
            v_thumb = v_thumb?.Replace("http://sessions.visitmix.com/images/", "https://sec.ch9.ms/ecn/content/mixvideos/");
            v_loc = v_loc?.Replace("http://video.ch9.ms/", "https://sec.ch9.ms/");
            v_loc = v_loc?.Replace("http://download.microsoft.com/", "https://download.microsoft.com/");

            if (featured && !string.IsNullOrEmpty(v_thumb) && !v_thumb.StartsWith("."))
            {
                var local_thumb = $"./thumbnails/{cat}_{name}/" + Path.ChangeExtension(loc_name, Path.GetExtension(v_thumb));
                if (has_filter) DownloadFile(v_thumb, local_thumb);
                v_thumb = local_thumb;
            }

            if (string.IsNullOrWhiteSpace(v_title))
            {
                if (!string.IsNullOrWhiteSpace(loc))
                {
                    v_title = $"[{new System.Uri(loc).Segments.Last()}]";
                }
                else
                {
                    v_title = "[Title Missing]";
                }
            }
            content.WriteLine(
                "<nobr class='vtitle-container'>" +
                $"<a href='http://web.archive.org/web/{arc_date}/{loc}' target='_blank'><img src='logo_archive-sm.png' width=24 height=24></a> " +
                "<span class='vtitle'>" + (string.IsNullOrEmpty(v_loc) ? System.Web.HttpUtility.HtmlDecode(v_title) : $"<a href='{v_loc}' target='_blank'>{System.Web.HttpUtility.HtmlDecode(v_title)}</a>") +
                ((v_time >= 0) ? $" [{v_time / 3600}:{(v_time % 3600) / 60:D2}:{v_time % 60:D2}]" : "") +
                ((v_date is object) ? DateTime.Parse(v_date).ToString(" [yyyy/MM/dd]") : "") +
                "</span></nobr>" +
                ((v_thumb is object) ? $"<img class='vthumb' src='{v_thumb}'/>" : "") +
                $"<div class='vdesc'>{(System.Web.HttpUtility.HtmlDecode(v_desc))}</div>" +
                "<br clear='left'/><br/>"
            );
        }
    }
}

foreach (var (name, index) in indexes)
{
    var featured = index.Item1.ToString();
    var archive = index.Item2.ToString();

    using var index_file = new StreamWriter(name, false, System.Text.Encoding.UTF8);

    index_file.WriteLine(
        "<head><link rel='stylesheet' href='styles.css'></head><body class='index'>"
    );
    index_file.Write(featured);
    if ((featured.Length > 0) && (archive.Length > 0))
    {
        index_file.WriteLine("<hr class='featured'/>");
    }
    index_file.Write(archive);

    index_file.Flush();
    index_file.Close();
}

void DownloadFile(string uri, string file)
{
    var path = Path.Combine(out_dir, file);
    if (!File.Exists(path))
    {
        bool done = false;
        do
        {
            try
            {
                Console.Write($"  Downloading {file}");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var data = http.GetByteArrayAsync(uri).Result;
                if (data.Length > 0)
                {
                    using var fs = File.OpenWrite(path);
                    fs.Write(data);
                    fs.Flush();
                    fs.Close();
                }
                else
                {
                    Console.Write(" [EMPTY]");
                }
                done = true;
            }
            catch (AggregateException ex)
            {
                if (ex.InnerException is HttpRequestException hrex)
                {
                    Console.Write($" [{hrex.StatusCode}]");
                    if (hrex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        done = true;
                    }
                }
                else throw;
            }
            catch
            {
                Console.Write(" [ERROR]");
                done = true;
            }
            Console.WriteLine();
        } while (!done);
    }

}