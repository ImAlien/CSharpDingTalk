using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DingtalkChatbot;

class Program
{
    static async Task Main(string[] args)
    {
        string webhook = "你的webhook地址";
        string secret = "你的加签密钥";

        var chatbot = new DingtalkChatbot(webhook, secret);

        // 发送文本消息
        await chatbot.SendTextAsync("你好，这是一条测试消息！");

        // 发送Markdown消息
        string markdownText = "### 这是一个Markdown消息\n" +
                              "- 项目1\n" +
                              "- 项目2\n" +
                              "[点击这里](https://www.example.com)";

        await chatbot.SendMarkdownAsync("Markdown消息", markdownText);

        // 发送ActionCard消息
        var btns = new List<CardItem>
        {
            new CardItem("按钮1", "https://www.example.com/1"),
            new CardItem("按钮2", "https://www.example.com/2")
        };

        var actionCard = new ActionCard("ActionCard标题", "这是一个ActionCard消息。", btns);

        await chatbot.SendActionCardAsync(actionCard);

        // 发送FeedCard消息
        var links = new List<CardItem>
        {
            new CardItem("新闻1", "https://www.example.com/news1", "https://www.example.com/image1.png"),
            new CardItem("新闻2", "https://www.example.com/news2", "https://www.example.com/image2.png")
        };

        await chatbot.SendFeedCardAsync(links);

        Console.WriteLine("消息发送完成。");
    }
}
