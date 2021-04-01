﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NServiceBus;

public class FakeMessageGenerator
{
    const string Sentences = "ABCD EFGH IJKL MNOP QRST UVWX YZ01 2345 6789\n";
    const string Lines = "ABCD EFGH IJKL MNOP QRST UVWX YZ01 2345 6789";
    const string Types = "ABCDEFGHIJKLMNOPQRSTUVWXYZ.";

    static string RandomString(int length, string chars)
    {
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[StaticRandom.Next(s.Length)]).ToArray());
    }

    const int BodySizeMax = 200;
    const int EnclosedMessageTypesMax = 250;
    const int CorrelationIdMax = 100000;
    const int ConversationIdMax = 100000;
    const int OriginatingEndpointMax = 100;
    const int FailedQMax = 100;
    const int ExceptionStackTracesMax = 200;
    const int ExceptionMessagesMax = 100;
    const int ExceptionTypesMax = 50;

    static readonly byte[] RandomData = Encoding.ASCII.GetBytes(File.ReadAllText("original.txt"));
    static readonly List<string> RandomExceptionStackTraces = RandomStrings(ExceptionStackTracesMax, 1000, 8000, Sentences);
    static readonly List<string> RandomExceptionMessages = RandomStrings(ExceptionMessagesMax, 15, 50, Lines);
    static readonly List<string> RandomExceptionTypes = RandomStrings(ExceptionTypesMax, 15, 250, Types);

    static List<string> RandomStrings(int count, int min, int max, string chars)
    {
        var items = new List<string>(count);

        for (int i = 0; i < count; i++)
        {
            items.Add(RandomString(StaticRandom.Next(min, max), chars));
        }

        return items;
    }

    public static (string id, Dictionary<string, string> headers, byte[] body) Create(bool isError)
    {
        var id = Guid.NewGuid().ToString();

        int length = StaticRandom.Next(BodySizeMax);
        var body = new byte[length];
        int start = StaticRandom.Next(RandomData.Length - length);
        Array.Copy(RandomData, start, body, 0, length);

        var now = DateTime.UtcNow;

        var intents = new[] { "Send", "Publish", "Reply" };
        var headers = new Dictionary<string, string>
        {
            [Headers.MessageId] = id,
            [Headers.ContentType] = " text/plain",
            //[Headers.ContentType] = " application/octet-stream",
            [Headers.EnclosedMessageTypes] = "random_" + StaticRandom.Next(EnclosedMessageTypesMax),
            [Headers.CorrelationId] = now.ToString("yyyy-M-dThh") + "_" + StaticRandom.Next(CorrelationIdMax),
            [Headers.ConversationId] = now.ToString("yyyy-M-dThh") + "_" + StaticRandom.Next(ConversationIdMax),
            //[Headers.RelatedTo] = "random",
            [Headers.MessageIntent] = intents[StaticRandom.Next(3)],
            [Headers.TimeSent] = DateTimeExtensions.ToWireFormattedString(now),
            ["NServiceBus.OriginatingEndpoint"] = "endpoint_" + StaticRandom.Next(OriginatingEndpointMax),
            ["NServiceBus.OriginatingMachine"] = Environment.MachineName
        };

        if (isError)
        {
            headers["NServiceBus.FailedQ"] = "endpoint_" + StaticRandom.Next(FailedQMax);
            headers["NServiceBus.ExceptionInfo.ExceptionType"] = RandomExceptionTypes[StaticRandom.Next(RandomExceptionTypes.Count)];
            headers["NServiceBus.ExceptionInfo.InnerExceptionType"] = RandomExceptionTypes[StaticRandom.Next(RandomExceptionTypes.Count)];
            headers["NServiceBus.ExceptionInfo.Message"] = RandomExceptionMessages[StaticRandom.Next(RandomExceptionMessages.Count)];
            headers["NServiceBus.ExceptionInfo.StackTrace"] = RandomExceptionStackTraces[StaticRandom.Next(RandomExceptionStackTraces.Count)];
            //["NServiceBus.ExceptionInfo.HelpLink"] //The exception help link.
            //["NServiceBus.ExceptionInfo.Source"] //The full type name of the InnerException if it exists. It is obtained by calling Exception.InnerException.GetType().FullName.
        }
        else
        {
            headers["NServiceBus.ProcessingEndpoint"] = "endpoint_" + StaticRandom.Next(100);
            headers["NServiceBus.ProcessingMachine"] = Environment.MachineName;
            headers["NServiceBus.ProcessingStarted"] = DateTimeExtensions.ToWireFormattedString(now);
            headers["NServiceBus.ProcessingEnded"] = DateTimeExtensions.ToWireFormattedString(now.AddMilliseconds(StaticRandom.Next(20, 20000)));
        }

        return (id, headers, body);
    }
}
