using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Подключается к готовому TMP-тексту на Canvas; хранит несколько одновременных предупреждений.</summary>
public class RaidWarningPanel : MonoBehaviour
{
    public RaidManager manager;
    public TMP_Text warningText;
    [Min(1)] public float visibleSeconds = 15f;
    [Min(1)] public int maxMessages = 4;
    private RaidManager bound;
    private readonly Queue<KeyValuePair<float, string>> messages = new Queue<KeyValuePair<float, string>>();
    private void Update()
    {
        var target = manager != null ? manager : RaidManager.Instance;
        if (bound != target)
        {
            Unbind(); bound = target;
            if (bound != null) bound.WarningRaised += Show;
        }
        while (messages.Count > 0 && messages.Peek().Key < Time.time) messages.Dequeue();
        if (warningText != null)
        {
            var lines = new List<string>(); foreach (var message in messages) lines.Add(message.Value);
            warningText.text = string.Join("\n", lines);
        }
    }
    public void Show(string message)
    {
        messages.Enqueue(new KeyValuePair<float, string>(Time.time + visibleSeconds, message));
        while (messages.Count > Mathf.Max(1, maxMessages)) messages.Dequeue();
    }
    private void Unbind() { if (bound != null) bound.WarningRaised -= Show; bound = null; }
    private void OnDisable() { Unbind(); messages.Clear(); if (warningText != null) warningText.text = ""; }
}
