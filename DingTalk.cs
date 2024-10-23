using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Net;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace DingtalkChatbot
{
    public class DingtalkChatbot
    {
        private static readonly HttpClient httpClient = new HttpClient();

        private string webhook;
        private string secret;
        private bool pcSlide;
        private bool failNotice;
        private DateTime startTime;
        private Queue<DateTime> requestQueue;

        public DingtalkChatbot(string webhook, string secret = null, bool pcSlide = false, bool failNotice = false)
        {
            this.webhook = webhook;
            this.secret = secret;
            this.pcSlide = pcSlide;
            this.failNotice = failNotice;
            this.startTime = DateTime.UtcNow;
            this.requestQueue = new Queue<DateTime>(20);

            if (!string.IsNullOrEmpty(this.secret) && this.secret.StartsWith("SEC"))
            {
                UpdateWebhook();
            }
        }

        private void UpdateWebhook()
        {
            long timestamp = (long)(startTime - new DateTime(1970, 1, 1)).TotalMilliseconds;
            string stringToSign = $"{timestamp}\n{secret}";

            // 使用 UTF-8 编码
            byte[] secretBytes = Encoding.UTF8.GetBytes(secret);
            byte[] stringToSignBytes = Encoding.UTF8.GetBytes(stringToSign);

            using (var hmac = new HMACSHA256(secretBytes))
            {
                byte[] hash = hmac.ComputeHash(stringToSignBytes);
                string sign = WebUtility.UrlEncode(Convert.ToBase64String(hash));

                if (webhook.Contains("timestamp"))
                {
                    int index = webhook.IndexOf("&timestamp");
                    webhook = $"{webhook.Substring(0, index)}&timestamp={timestamp}&sign={sign}";
                }
                else
                {
                    webhook = $"{webhook}&timestamp={timestamp}&sign={sign}";
                }
            }
        }

        private string MsgOpenType(string url)
        {
            // 使用 UTF-8 编码进行 URL 编码
            string encodeUrl = WebUtility.UrlEncode(url);
            string finalLink = $"dingtalk://dingtalkclient/page/link?url={encodeUrl}&pc_slide={pcSlide.ToString().ToLower()}";
            return finalLink;
        }

        public async Task<Dictionary<string, object>> SendTextAsync(string msg, bool isAtAll = false, List<string> atMobiles = null, List<string> atDingtalkIds = null, bool isAutoAt = true)
        {
            if (string.IsNullOrWhiteSpace(msg))
                throw new ArgumentException("文本消息内容不能为空。");

            var data = new Dictionary<string, object>
            {
                { "msgtype", "text" },
                { "text", new Dictionary<string, string> { { "content", msg } } },
                { "at", new Dictionary<string, object>() }
            };

            if (isAtAll)
            {
                ((Dictionary<string, object>)data["at"])["isAtAll"] = true;
            }

            if (atMobiles != null && atMobiles.Count > 0)
            {
                ((Dictionary<string, object>)data["at"])["atMobiles"] = atMobiles;
                if (isAutoAt)
                {
                    string mobilesText = "\n@" + string.Join("@", atMobiles);
                    ((Dictionary<string, string>)data["text"])["content"] += mobilesText;
                }
            }

            if (atDingtalkIds != null && atDingtalkIds.Count > 0)
            {
                ((Dictionary<string, object>)data["at"])["atUserIds"] = atDingtalkIds;
            }

            return await PostAsync(data);
        }

        public async Task<Dictionary<string, object>> SendImageAsync(string picUrl)
        {
            if (string.IsNullOrWhiteSpace(picUrl))
                throw new ArgumentException("图片链接不能为空。");

            var data = new Dictionary<string, object>
            {
                { "msgtype", "image" },
                { "image", new Dictionary<string, string> { { "picURL", picUrl } } }
            };

            return await PostAsync(data);
        }

        public async Task<Dictionary<string, object>> SendLinkAsync(string title, string text, string messageUrl, string picUrl = "")
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(messageUrl))
                throw new ArgumentException("链接消息的标题、内容和链接不能为空。");

            var data = new Dictionary<string, object>
            {
                { "msgtype", "link" },
                { "link", new Dictionary<string, string>
                    {
                        { "text", text },
                        { "title", title },
                        { "picUrl", picUrl },
                        { "messageUrl", MsgOpenType(messageUrl) }
                    }
                }
            };

            return await PostAsync(data);
        }

        public async Task<Dictionary<string, object>> SendMarkdownAsync(string title, string text, bool isAtAll = false, List<string> atMobiles = null, List<string> atDingtalkIds = null, bool isAutoAt = true)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Markdown消息的标题和内容不能为空。");

            // 处理 Markdown 文本中的链接
            string processedText = Regex.Replace(text, @"(?<!!)\[.*?\]\((.*?)\)", match =>
            {
                string url = match.Groups[1].Value;
                string newUrl = MsgOpenType(url);
                return match.Value.Replace(url, newUrl);
            });

            var data = new Dictionary<string, object>
            {
                { "msgtype", "markdown" },
                { "markdown", new Dictionary<string, string>
                    {
                        { "title", title },
                        { "text", processedText }
                    }
                },
                { "at", new Dictionary<string, object>() }
            };

            if (isAtAll)
            {
                ((Dictionary<string, object>)data["at"])["isAtAll"] = true;
            }

            if (atMobiles != null && atMobiles.Count > 0)
            {
                ((Dictionary<string, object>)data["at"])["atMobiles"] = atMobiles;
                if (isAutoAt)
                {
                    string mobilesText = "\n@" + string.Join("@", atMobiles);
                    ((Dictionary<string, string>)data["markdown"])["text"] += mobilesText;
                }
            }

            if (atDingtalkIds != null && atDingtalkIds.Count > 0)
            {
                ((Dictionary<string, object>)data["at"])["atUserIds"] = atDingtalkIds;
            }

            return await PostAsync(data);
        }

        public async Task<Dictionary<string, object>> SendActionCardAsync(ActionCard actionCard)
        {
            if (actionCard == null)
                throw new ArgumentNullException(nameof(actionCard));

            var data = actionCard.GetData();

            // 更新消息链接的打开方式
            if (data.ContainsKey("actionCard"))
            {
                var actionCardData = (Dictionary<string, object>)data["actionCard"];

                if (actionCardData.ContainsKey("singleURL"))
                {
                    actionCardData["singleURL"] = MsgOpenType(actionCardData["singleURL"].ToString());
                }
                else if (actionCardData.ContainsKey("btns"))
                {
                    var btns = (List<Dictionary<string, string>>)actionCardData["btns"];
                    foreach (var btn in btns)
                    {
                        btn["actionURL"] = MsgOpenType(btn["actionURL"]);
                    }
                }
            }

            return await PostAsync(data);
        }

        public async Task<Dictionary<string, object>> SendFeedCardAsync(List<CardItem> links)
        {
            if (links == null || links.Count == 0)
                throw new ArgumentException("FeedCard类型的链接列表不能为空。");

            var linkList = new List<Dictionary<string, string>>();

            foreach (var link in links)
            {
                var linkData = link.GetData();
                linkData["messageURL"] = MsgOpenType(linkData["messageURL"]);
                linkList.Add(linkData);
            }

            var data = new Dictionary<string, object>
            {
                { "msgtype", "feedCard" },
                { "feedCard", new Dictionary<string, object> { { "links", linkList } } }
            };

            return await PostAsync(data);
        }

        private async Task<Dictionary<string, object>> PostAsync(Dictionary<string, object> data)
        {
            DateTime now = DateTime.UtcNow;

            // 每小时更新签名
            if ((now - startTime).TotalSeconds >= 3600 && !string.IsNullOrEmpty(secret) && secret.StartsWith("SEC"))
            {
                startTime = now;
                UpdateWebhook();
            }

            // 限流处理，每分钟最多发送20条
            requestQueue.Enqueue(now);
            if (requestQueue.Count > 20)
            {
                var elapsed = (now - requestQueue.Dequeue()).TotalSeconds;
                if (elapsed < 60)
                {
                    int sleepTime = (int)(60 - elapsed) + 1;
                    await Task.Delay(sleepTime * 1000);
                }
            }

            string postData = JsonConvert.SerializeObject(data);

            try
            {
                // 使用 UTF-8 编码创建请求内容
                var content = new StringContent(postData, Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(webhook, content);

                // 读取响应内容，默认使用 UTF-8 编码
                string responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"请求失败，状态码：{response.StatusCode}，响应内容：{responseContent}");
                }

                var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseContent);

                // 失败提醒
                if (failNotice && result.ContainsKey("errcode") && Convert.ToInt32(result["errcode"]) != 0)
                {
                    string timeNow = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    var errorData = new Dictionary<string, object>
                    {
                        { "msgtype", "text" },
                        { "text", new Dictionary<string, string>
                            {
                                { "content", $"[异常通知]钉钉机器人消息发送失败，失败时间：{timeNow}，失败原因：{result["errmsg"]}，要发送的消息：{postData}，请及时跟进，谢谢！" }
                            }
                        },
                        { "at", new Dictionary<string, object> { { "isAtAll", false } } }
                    };

                    // 使用 UTF-8 编码发送错误通知
                    await httpClient.PostAsync(webhook, new StringContent(JsonConvert.SerializeObject(errorData), Encoding.UTF8, "application/json"));
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new Exception("消息发送失败", ex);
            }
        }
    }

    public class ActionCard
    {
        public string Title { get; set; }
        public string Text { get; set; }
        public List<CardItem> Btns { get; set; }
        public int BtnOrientation { get; set; } = 0;
        public int HideAvatar { get; set; } = 0;

        public ActionCard(string title, string text, List<CardItem> btns, int btnOrientation = 0, int hideAvatar = 0)
        {
            this.Title = title;
            this.Text = text;
            this.Btns = btns;
            this.BtnOrientation = btnOrientation;
            this.HideAvatar = hideAvatar;
        }

        public Dictionary<string, object> GetData()
        {
            if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Text) || Btns == null || Btns.Count == 0)
                throw new ArgumentException("ActionCard的标题、内容或按钮列表不能为空。");

            var actionCard = new Dictionary<string, object>
            {
                { "title", Title },
                { "text", Text },
                { "hideAvatar", HideAvatar.ToString() },
                { "btnOrientation", BtnOrientation.ToString() }
            };

            if (Btns.Count == 1)
            {
                // 整体跳转ActionCard
                actionCard["singleTitle"] = Btns[0].Title;
                actionCard["singleURL"] = Btns[0].URL;
            }
            else
            {
                // 独立跳转ActionCard
                var btnsList = new List<Dictionary<string, string>>();
                foreach (var btn in Btns)
                {
                    btnsList.Add(btn.GetData());
                }
                actionCard["btns"] = btnsList;
            }

            var data = new Dictionary<string, object>
            {
                { "msgtype", "actionCard" },
                { "actionCard", actionCard }
            };

            return data;
        }
    }

    public class CardItem
    {
        public string Title { get; set; }
        public string URL { get; set; }
        public string PicURL { get; set; }

        public CardItem(string title, string url, string picUrl = null)
        {
            this.Title = title;
            this.URL = url;
            this.PicURL = picUrl;
        }

        public Dictionary<string, string> GetData()
        {
            if (!string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(URL))
            {
                var data = new Dictionary<string, string>
                {
                    { "title", Title },
                    { "messageURL", URL }
                };

                if (!string.IsNullOrWhiteSpace(PicURL))
                {
                    data["picURL"] = PicURL;
                }

                return data;
            }
            else
            {
                throw new ArgumentException("CardItem的标题和URL不能为空。");
            }
        }
    }
}
