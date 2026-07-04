using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using OneClickTransfer.Avalonia.ViewModels.Abstractions;
using OneClickTransfer.Models;

namespace OneClickTransfer.Avalonia.ViewModels;

/// <summary>Vigia a origem das tarefas marcadas (FileSystemWatcher) e, após um debounce de
/// 1200ms por tarefa, dispara o envio na thread de UI. BCL puro (testável, sem Avalonia).</summary>
public sealed class WatchCoordinator : IDisposable
{
    private const int DebounceMs = 1200;   // builds reescrevem o arquivo várias vezes seguidas

    private readonly IUiDispatcher _ui;
    private readonly Action<TransferJob> _onTrigger;
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly Dictionary<TransferJob, Timer> _timers = new();
    private readonly object _gate = new();

    public WatchCoordinator(IUiDispatcher ui, Action<TransferJob> onTrigger)
    {
        _ui = ui;
        _onTrigger = onTrigger;
    }

    /// <summary>Recria os watchers a partir das tarefas vigiadas (e prontas).</summary>
    public void Restart(IEnumerable<TransferJob> watchedJobs)
    {
        Stop();
        foreach (var job in watchedJobs)
        {
            if (job.Source.Kind == SourceKind.Folder) WatchFolder(job);
            else foreach (var path in job.Source.All) WatchSingleFile(job, path);
        }
    }

    private void WatchSingleFile(TransferJob job, string path)
    {
        var dir = Path.GetDirectoryName(path);
        var file = Path.GetFileName(path);
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(file) || !Directory.Exists(dir)) return;
        try
        {
            var w = new FileSystemWatcher(dir, file)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                             | NotifyFilters.FileName | NotifyFilters.CreationTime
            };
            var captured = job;
            FileSystemEventHandler h = (_, _) => OnEvent(captured);
            w.Changed += h;
            w.Created += h;
            w.Renamed += (_, _) => OnEvent(captured);
            w.EnableRaisingEvents = true;
            _watchers.Add(w);
        }
        catch { /* pasta pode sumir entre a checagem e o watcher */ }
    }

    /// <summary>Origem "pasta inteira": um único watcher recursivo na raiz, senão um arquivo
    /// novo dentro dela nunca dispararia o auto-envio.</summary>
    private void WatchFolder(TransferJob job)
    {
        var dir = job.Source.Path;
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
        try
        {
            var w = new FileSystemWatcher(dir)
            {
                Filter = string.IsNullOrEmpty(job.Source.Pattern) ? "*" : job.Source.Pattern,
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
                             | NotifyFilters.CreationTime | NotifyFilters.DirectoryName
            };
            var captured = job;
            FileSystemEventHandler h = (_, _) => OnEvent(captured);
            w.Changed += h;
            w.Created += h;
            w.Deleted += h;
            w.Renamed += (_, _) => OnEvent(captured);
            w.EnableRaisingEvents = true;
            _watchers.Add(w);
        }
        catch { /* pasta pode sumir entre a checagem e o watcher */ }
    }

    public void Stop()
    {
        foreach (var w in _watchers) { try { w.EnableRaisingEvents = false; w.Dispose(); } catch { } }
        _watchers.Clear();
        lock (_gate)
        {
            foreach (var t in _timers.Values) { try { t.Dispose(); } catch { } }
            _timers.Clear();
        }
    }

    private void OnEvent(TransferJob job)
    {
        lock (_gate)
        {
            if (!_timers.TryGetValue(job, out var timer))
            {
                var captured = job;
                timer = new Timer(_ => Fire(captured), null, Timeout.Infinite, Timeout.Infinite);
                _timers[job] = timer;
            }
            timer.Change(DebounceMs, Timeout.Infinite);
        }
    }

    private void Fire(TransferJob job) => _ui.Post(() => _onTrigger(job));

    public void Dispose() => Stop();
}
