﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord_Driver_Bot.Command;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Discord_Driver_Bot.Book.Host
{
    class Hitomi
    {
        public static void GetData(string url, ICommandContext e)
        {
            try
            {
                string[] urlSplit = url.Split(new char[] { '?' })[0].Trim('/').Split(new char[] { '/' });
                string ID = urlSplit[2];
                if (!ID.EndsWith(".html")) ID += ".html";

                if (!Function.GetIDIsExist(string.Format("https://hitomi.la/galleries/{0}", ID)))
                { e.Channel.SendMessageAsync(string.Format("{0} ID {1} 不存在本子", e.Message.Author.Mention, ID.Split(new char[] { '.' })[0])); return; }

                string thumbnailURL, title, artist, bookName;
                Dictionary<string, List<string>> dicTag;

                if (SQLite.SQLiteFunction.GetBookData(string.Format("https://hitomi.la/galleries/{0}", ID), out SQLite.Table.BookData bookData))
                {
                    thumbnailURL = bookData.ThumbnailUrl;
                    title = bookData.Title;
                    artist = bookData.ExtensionData;
                    bookName = title.Split(new char[] { '|' })[0].Trim().FormatBookName();
                    dicTag = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(bookData.Tags.Trim('"').Replace("\\", string.Empty));
                }
                else
                {
                    dicTag = new Dictionary<string, List<string>>();
                    HtmlWeb htmlWeb = new HtmlWeb();
                    IEnumerable<HtmlNode> htmlDocumentNode = htmlWeb.Load(string.Format("https://hitomi.la/galleries/{0}", ID)).DocumentNode.Descendants();

                    thumbnailURL = "https:" + htmlDocumentNode.First((x) => x.Name == "img" && x.ParentNode.ParentNode.HasClass("cover")).GetAttributeValue("src", "");
                    htmlDocumentNode = htmlDocumentNode.First((x) => (x.Name == "div" && x.HasClass("gallery"))).Descendants();
                    title = HttpUtility.HtmlDecode(htmlDocumentNode.First((x) => (x.Name == "a")).InnerText);
                    artist = HttpUtility.HtmlDecode(htmlDocumentNode.First((x) => (x.Name == "a" && x.ParentNode.ParentNode.HasClass("comma-list"))).InnerText);
                    bookName = title.Split(new char[] { '|' })[0].Trim().FormatBookName();

                    foreach (HtmlNode item2 in htmlDocumentNode.First((x) => x.Name == "div" && x.HasClass("gallery-info")).Descendants().Where((x) => x.Name == "tr"))
                    {
                        string tagName = item2.Descendants().First((x) => x.Name == "td").InnerText;
                        if (tagName == "") continue;

                        List<string> tagList = new List<string>(item2.Descendants().Where((x) => x.Name == "#text" && x.ParentNode.Name == "a").Select((x) => x.InnerText.Trim()));
                        if (tagList.Count == 0) tagList.Add("-");

                        dicTag.Add(tagName, tagList);
                    }

                    new SQLite.Table.BookData(string.Format("https://hitomi.la/galleries/{0}", ID), title, artist, thumbnailURL, dicTag).InsertNewData();
                }

                Log.FormatColorWrite(string.Format("{0} ({1})", thumbnailURL, bookName), ConsoleColor.Green);

                EmbedBuilder discordEmbedBuilder = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle(title)
                    .WithDescription(artist)
                    .WithUrl(string.Format("https://hitomi.la/galleries/{0}", ID))
                    .WithThumbnailUrl(e.Guild.Id == 463657254105645056 ? "" : thumbnailURL);

                foreach (var item in dicTag)
                    discordEmbedBuilder.AddField(item.Key, string.Join(", ", item.Value), true);

                SearchFunction.SearchE_Hentai(bookName, out string E_HentaiUrl, out string E_HentaiLanguage);
                SearchFunction.SearchExHentai(bookName, out string ExHentaiUrl, out string ExHentaiLanguage);
                SearchFunction.SearchNHentai(bookName, out string nHentaiUrl, out string nHentaiLanguage);
                SearchFunction.SearchWnacg(bookName, out string wnacgUrl, out string wnacgLanguage);

                if (E_HentaiUrl != "" || nHentaiUrl != "" || wnacgUrl != "")
                {
                    discordEmbedBuilder.AddField("其他網站(不一定正確):",
                        (E_HentaiUrl != "" ? string.Format("[E-站({0})]({1})\t",  E_HentaiLanguage, E_HentaiUrl) : "") +
                        (ExHentaiUrl != "" ? string.Format("[Ex站({0})]({1})\t", ExHentaiLanguage, ExHentaiUrl) : "") +
                        (nHentaiUrl != "" ? string.Format("[N站({0})]({1})\t", nHentaiLanguage, nHentaiUrl) : "") +
                        (wnacgUrl != "" ? string.Format("[W站({0})]({1})", wnacgLanguage, wnacgUrl) : ""), true);
                }
                else discordEmbedBuilder.AddField("其他網站:", "無", true);

                if (bookData != null) discordEmbedBuilder.AddField("被看過了", bookData.DateTime.Replace("T", " ") + " 被其他人看過", true);
                discordEmbedBuilder.WithFooter(e.Message.Author.Username + " ID: " + e.Message.Author.Id, e.Message.Author.GetAvatarUrl());
                e.Channel.SendMessageAsync(null, false, discordEmbedBuilder.Build());
            }
            catch (Exception e2)
            {
                Program.ApplicatonOwner.SendMessageAsync(string.Format("{0} ({1})\n{2}\n{3}", e.Message.Author.Username, e.Channel.Name, "https://" + url, e2.Message + "\n" + e2.StackTrace));
                Log.FormatColorWrite(e2.Message + "\r\n" + e2.StackTrace, ConsoleColor.Red);
            }
        }
    }
}