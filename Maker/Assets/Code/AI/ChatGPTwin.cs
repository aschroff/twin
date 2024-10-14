using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AiToolbox;

public static class ChatGPTwin 
{
    public static Action Request(string prompt, ChatGptParameters parameters, Action<string> completeCallback,
        Action<long, string> failureCallback, Action<string> updateCallback = null)
    {
        return ChatGpt.Request(prompt, parameters, completeCallback, failureCallback, updateCallback);
    }

    public static Action Request(IEnumerable<Message> messages, ChatGptParameters parameters,
        Action<string> completeCallback,
        Action<long, string> failureCallback, Action<string> updateCallback = null)
    {
        return ChatGpt.Request(messages, parameters, completeCallback, failureCallback, updateCallback);
    }

    public static void CancelAllRequests()
    {
        ChatGpt.CancelAllRequests();
    }

}
