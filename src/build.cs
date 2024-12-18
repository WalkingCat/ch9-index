﻿using System.Xml;
using System.Net.Http;
using System.Linq;

var out_dir = @"..\docs\";
var filter = args.FirstOrDefault("");
var has_filter = !string.IsNullOrEmpty(filter);
var indexes = new Dictionary<string, Tuple<StringWriter, StringWriter>>();
var http = new HttpClient();

foreach (var path in Directory.EnumerateFiles(@"..\sitemaps\", filter)) {
    var filename = Path.GetFileNameWithoutExtension(path);
    var name_start = filename.IndexOf("_", 8);
    var cat = filename.Substring(8, name_start - 8);
    var name = filename.Substring(name_start + 1);
    Console.WriteLine($"{cat} {name}");

    var xml = new XmlDocument();
    xml.Load(path);

    var index_name = $"{out_dir}{cat}.html";
    if (!has_filter) {
        if (!indexes.ContainsKey(index_name)) {
            indexes[index_name] = new(new StringWriter(), new StringWriter());
        }
    }
    var index_writers = indexes.GetValueOrDefault(index_name);

    using var content = new StreamWriter($"..\\docs\\{cat}_{name}.html", false, System.Text.Encoding.UTF8);

    var first = true;
    var urls = xml.GetElementsByTagName("url");
    var arc_date = "2020";
    var featured = false;

    foreach (XmlElement elem in urls) {
        var video = elem.GetElementsByTagName("video:video").Item(0) as XmlElement;
        var loc = elem.GetElementsByTagName("loc").Item(0)?.InnerText;
        var loc_name = Path.GetFileName(loc);
        var title = elem.GetElementsByTagName("title").Item(0)?.InnerText;
        if (string.IsNullOrEmpty(title)) { title = name.Replace('+', ' '); }
        var desc = elem.GetElementsByTagName("description").Item(0)?.InnerText;
        var thumb = elem.GetElementsByTagName("thumbnail_loc").Item(0)?.InnerText;
        if (!string.IsNullOrEmpty(thumb)) {
            thumb = "https://web.archive.org/web/2020if_/" + thumb;
        }

        if (first) {
            featured = elem.GetElementsByTagName("featured").Item(0)?.InnerText == "true";
            if (featured && !string.IsNullOrEmpty(thumb) && !thumb.StartsWith(".")) {
                var local_thumb = $"./thumbnails/{cat}_{name}{Path.GetExtension(thumb)}";
                if (has_filter) DownloadFile(thumb, local_thumb);
                thumb = local_thumb;
            }

            arc_date = (elem.GetElementsByTagName("archive_date").Item(0)?.InnerText) ?? arc_date;

            var link = (cat == "Posts") ? $"https://channel9.msdn.com/Niners/{name}/Posts" : loc;

            first = false;
            if (index_writers is object) {
                var index = featured ? index_writers.Item1 : index_writers.Item2;
                index.WriteLine(
                     $"<nobr id='{name}' class='title-container{(featured ? " featured" : "")}'>" +
                     (featured ? $"<img src='{thumb}' class='thumb-inline{(string.IsNullOrEmpty(thumb) ? " broken" : "")}'>" : "") +
                     $"<span class='title'><a href='{cat}_{name}.html' target='content' class='title'>{title}</a> ({((cat == "Posts") ? urls.Count : (urls.Count - 1))})</span>" +
                     $"<a class='permalink' href='index.html?p={cat}_{name}' target='_top'>#</a>" +
                     "</nobr>"
                 );
                index.Flush();
            }

            content.WriteLine("<head><link rel='stylesheet' href='styles.css'></head><body class='content'>");
            content.Write(
                "<nobr class='title-container'><h2><span class='title'>" +
                $"<a href='https://web.archive.org/web/{arc_date}/{link}' target='_blank'>{cat} - {title}</a>" +
                "</span></h2></nobr>"
            );
            if (!(string.IsNullOrEmpty(thumb) && string.IsNullOrEmpty(desc))) {
                content.Write(
                    (string.IsNullOrEmpty(thumb) ? "" : $"<img class='thumb' src='{thumb}'/>") +
                    $"<div class='desc'>{(System.Web.HttpUtility.HtmlDecode(desc))}</div>" +
                    (string.IsNullOrEmpty(thumb) ? "" : "<br clear='right'/>")
                );
            }
            content.WriteLine("<br/>");
            if (cat != "Posts") {
                continue;
            }
        }
        {
            var v_title = video?.GetElementsByTagName("video:title").Item(0)?.InnerText;
            var v_desc = video?.GetElementsByTagName("video:description").Item(0)?.InnerText;
            var v_date = video?.GetElementsByTagName("video:publication_date").Item(0)?.InnerText;
            var v_time = long.Parse(video?.GetElementsByTagName("video:duration").Item(0)?.InnerText ?? "-1");

            var v_thumb = video?.GetElementsByTagName("video:thumbnail_loc").Item(0)?.InnerText;
            v_thumb = v_thumb?.Replace("http://video.ch9.ms/", "https://sec.ch9.ms/");
            v_thumb = v_thumb?.Replace("http://files.channel9.msdn.com/", "https://f.ch9.ms/");
            v_thumb = v_thumb?.Replace("http://sessions.visitmix.com/images/", "https://sec.ch9.ms/ecn/content/mixvideos/");

            var v_res = new List<Resource>();
            var v_locs = video?.GetElementsByTagName("video:content_loc");
            if (v_locs is object) {
                for (var i = 0; i < v_locs.Count; ++i) {
                    var item = v_locs.Item(i)!;
                    var v_loc = item.InnerText;
                    if (!string.IsNullOrEmpty(v_loc)) {
                        bool v_fixed = (item.Attributes?["fixed"]?.InnerText == "true");
                        if (!v_fixed) {
                            v_loc = v_loc.Replace("http://video.ch9.ms/", "https://sec.ch9.ms/");
                            v_loc = v_loc.Replace("http://download.microsoft.com/", "https://download.microsoft.com/");
                        }
                        v_res.Add(new Resource(v_loc) { Label = item.Attributes?["label"]?.InnerText, ArchiveDate = item.Attributes?["arcdate"]?.InnerText });
                    }
                }
            }

            if (!string.IsNullOrEmpty(v_thumb) && !v_thumb.StartsWith(".")) {
                v_thumb = "https://web.archive.org/web/2020if_/" + v_thumb;
                if (featured) {
                    var local_thumb = $"./thumbnails/{cat}_{name}/" + Path.ChangeExtension(loc_name, Path.GetExtension(v_thumb));
                    if (has_filter) DownloadFile(v_thumb, local_thumb);
                    v_thumb = local_thumb;
                }
            }

            if (string.IsNullOrWhiteSpace(v_title)) {
                if (!string.IsNullOrWhiteSpace(loc)) {
                    v_title = $"[{new System.Uri(loc).Segments.Last()}]";
                } else {
                    v_title = "[Title Missing]";
                }
            }
            content.Write(
                "<nobr class='vtitle-container'><span class='vtitle'>" +
                $"<a href='https://web.archive.org/web/{arc_date}/{loc}' target='_blank'>" +
                System.Web.HttpUtility.HtmlDecode(v_title) +
                "</a><span></nobr>"
            );
            content.Write(
                ((v_thumb is object) ? $"<img class='vthumb' src='{v_thumb}'/>" : "")
            );
            content.Write("<nobr class='vinfo-container'><span class='vinfo'>");
            foreach (var res in v_res) {
                var ext = Path.GetExtension(res.Location).TrimStart('.').ToUpper();
                if (string.IsNullOrEmpty(ext)) {
                    ext = "LINK";
                }
                var res_label = (res.Label is object) ? $"({res.Label.ToUpper()})" : "";
                var res_arc_date = res.ArchiveDate;
                var res_loc = res.Location;
                if (cat != "Extras") {
                    res_loc = $"https://web.archive.org/web/{res_arc_date ?? arc_date}if_/" + res_loc;
                }
                content.Write($"<a href='{res_loc}' target='_blank'>[{ext}{res_label}]</a> ");
            }
            content.Write(
                ((v_time >= 0) ? $"[{v_time / 3600}:{(v_time % 3600) / 60:D2}:{v_time % 60:D2}] " : "") +
                ((v_date is object) ? DateTime.Parse(v_date).ToString("[yyyy/MM/dd]") : "") +
                "</span></nobr>");
            content.WriteLine(
                $"<div class='vdesc'>{(System.Web.HttpUtility.HtmlDecode(v_desc))}</div>" +
                "<br clear='left'/><br/>"
            );
        }
    }
}

foreach (var (name, index) in indexes) {
    var featured = index.Item1.ToString();
    var archive = index.Item2.ToString();

    using var index_file = new StreamWriter(name, false, System.Text.Encoding.UTF8);

    index_file.WriteLine(
        "<head><link rel='stylesheet' href='styles.css'></head><body class='index'>"
    );
    if (featured.Length > 0) {
        index_file.WriteLine("<hr class='featured-begin'/>");
    }
    index_file.Write(featured);
    if ((featured.Length > 0) && (archive.Length > 0)) {
        index_file.WriteLine("<hr class='featured'/>");
    }
    index_file.Write(archive);

    index_file.Flush();
    index_file.Close();
}

void DownloadFile(string uri, string file)
{
    var path = Path.Combine(out_dir, file);
    if (!File.Exists(path)) {
        bool done = false;
        do {
            try {
                Console.Write($"  Downloading {file}");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var data = http.GetByteArrayAsync(uri).Result;
                if (data.Length > 0) {
                    using var fs = File.OpenWrite(path);
                    fs.Write(data);
                    fs.Flush();
                    fs.Close();
                } else {
                    Console.Write(" [EMPTY]");
                }
                done = true;
            } catch (AggregateException ex) {
                if (ex.InnerException is HttpRequestException hrex) {
                    Console.Write($" [{hrex.StatusCode}]");
                    if ((hrex.StatusCode == System.Net.HttpStatusCode.NotFound)
                        || (hrex.StatusCode == System.Net.HttpStatusCode.GatewayTimeout)
                        || (hrex.StatusCode == System.Net.HttpStatusCode.Conflict)) {
                        done = true;
                    }
                } else throw;
            } catch {
                Console.Write(" [ERROR]");
                done = true;
            }
            Console.WriteLine();
        } while (!done);
    }

}

class Resource
{
    public string Location;
    public string? Label;
    public string? ArchiveDate;
    public Resource(string loc, string? label = null, string? arcDate = null)
    {
        Location = loc;
        Label = label;
        ArchiveDate = arcDate;
    }
}