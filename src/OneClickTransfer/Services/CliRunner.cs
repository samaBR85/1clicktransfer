using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using OneClickTransfer.I18n;
using OneClickTransfer.Models;

namespace OneClickTransfer.Services;

/// <summary>
/// Modo linha de comando (headless). Permite disparar transferências por script /
/// Agendador de Tarefas sem abrir a janela. Reutiliza o TransferService.
/// Códigos de saída: 0 = ok, 1 = alguma falha, 2 = erro de uso.
/// </summary>
public static class CliRunner
{
    [DllImport("kernel32.dll")] private static extern bool AttachConsole(int dwProcessId);
    private const int ATTACH_PARENT_PROCESS = -1;

    private static readonly string[] Verbs =
        { "--task", "-t", "--all", "-a", "--list", "-l", "--help", "-h", "-?", "/?" };

    /// <summary>Há algum verbo de CLI nos argumentos? (senão, abre a janela normal)</summary>
    public static bool IsCli(string[]? args)
        => args != null && args.Any(a => Verbs.Contains(a, StringComparer.OrdinalIgnoreCase));

    private static bool _silent;
    private static void Out(string s) { if (!_silent) Console.WriteLine(s); }
    private static string Msg(string pt, string en) => L.Lang == "en" ? en : pt;

    public static int Run(string[] args, AppSettings s)
    {
        AttachConsole(ATTACH_PARENT_PROCESS);   // saída vai p/ o console que chamou (se houver)
        _silent = args.Any(a => a.Equals("--silent", StringComparison.OrdinalIgnoreCase)
                             || a.Equals("-s", StringComparison.OrdinalIgnoreCase));

        if (args.Any(a => a is "--help" or "-h" or "-?" or "/?")) { PrintHelp(); return 0; }

        if (args.Any(a => a.Equals("--list", StringComparison.OrdinalIgnoreCase)
                       || a.Equals("-l", StringComparison.OrdinalIgnoreCase)))
        {
            Out(Msg("Tarefas salvas:", "Saved tasks:"));
            foreach (var j in s.Jobs)
                Out($"  [{(j.Enabled ? "x" : " ")}] {j.Name}  —  {j.Summary}");
            return 0;
        }

        // Coleta --task (repetível) e --all
        var names = new List<string>();
        bool all = false;
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a.Equals("--all", StringComparison.OrdinalIgnoreCase) || a.Equals("-a", StringComparison.OrdinalIgnoreCase))
                all = true;
            else if (a.Equals("--task", StringComparison.OrdinalIgnoreCase) || a.Equals("-t", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) names.Add(args[++i]);
                else { Out(Msg("Erro: --task exige um nome.", "Error: --task requires a name.")); return 2; }
            }
            else if (a.Equals("--silent", StringComparison.OrdinalIgnoreCase) || a.Equals("-s", StringComparison.OrdinalIgnoreCase))
            { /* já tratado */ }
            else { Out(Msg($"Argumento desconhecido: {a}", $"Unknown argument: {a}")); PrintHelp(); return 2; }
        }

        // Resolve as tarefas-alvo
        var jobs = new List<TransferJob>();
        if (all) jobs.AddRange(s.Jobs.Where(j => j.Enabled));   // --all = só as marcadas
        foreach (var nm in names)                               // --task = roda mesmo se desmarcada
        {
            var j = s.Jobs.FirstOrDefault(x => string.Equals(x.Name, nm, StringComparison.OrdinalIgnoreCase));
            if (j == null) { Out(Msg($"Tarefa não encontrada: \"{nm}\"", $"Task not found: \"{nm}\"")); return 2; }
            if (!jobs.Contains(j)) jobs.Add(j);
        }

        if (jobs.Count == 0)
        {
            Out(Msg("Nada a fazer. Use --task \"Nome\" ou --all.  (--help)",
                    "Nothing to do. Use --task \"Name\" or --all.  (--help)"));
            return 2;
        }

        return RunJobs(jobs);
    }

    private static int RunJobs(List<TransferJob> jobs)
    {
        int sent = 0, skipped = 0, failed = 0;
        foreach (var j in jobs)
        {
            var dests = j.Destinations.Where(d => d.Enabled && DestReady(d)).ToList();
            var files = j.Source.All.Where(File.Exists).ToList();
            if (dests.Count == 0 || files.Count == 0)
            {
                failed++;
                Out(Msg($"FALHA  {j.Name}: sem origem válida ou destino.",
                        $"FAIL   {j.Name}: no valid source or destination."));
                continue;
            }
            foreach (var src in files)
            {
                var fileName = Path.GetFileName(src);
                foreach (var d in dests)
                {
                    try
                    {
                        if (j.Overwrite != OverwriteMode.Always && TransferService.DestExists(d, fileName))
                        {
                            if (j.Overwrite == OverwriteMode.Never) { skipped++; Out($"SKIP   {fileName} -> {d.Summary}"); continue; }
                            if (j.Overwrite == OverwriteMode.IfNewer && !SourceNewer(d, src, fileName)) { skipped++; Out($"SKIP   {fileName} -> {d.Summary}"); continue; }
                        }
                        TransferService.Send(d, src, null);
                        sent++;
                        Out($"OK     {fileName} -> {d.Summary}");
                    }
                    catch (Exception ex) { failed++; Out($"FALHA  {fileName} -> {d.Summary}: {ex.Message}"); }
                }
            }
        }
        Out(Msg($"Concluído: {sent} enviado(s), {skipped} pulado(s), {failed} falha(s).",
                $"Done: {sent} sent, {skipped} skipped, {failed} failed."));
        return failed > 0 ? 1 : 0;
    }

    private static bool DestReady(Destination d)
        => d.Type == DestType.Local ? !string.IsNullOrEmpty(d.Folder) : !string.IsNullOrEmpty(d.Host);

    private static bool SourceNewer(Destination d, string src, string fileName)
    {
        var srcT = File.GetLastWriteTime(src);
        if (d.Type == DestType.Local)
        {
            var dst = Path.Combine(d.Folder, fileName);
            return !File.Exists(dst) || srcT > File.GetLastWriteTime(dst);
        }
        var rt = TransferService.DestModified(d, fileName);
        return rt == null || srcT > rt.Value;
    }

    private static void PrintHelp()
    {
        const string exe = "1clickTransfer";
        var pt = $@"
{L.T("appTitle")} — linha de comando

Uso:
  {exe} --task ""Nome""      envia a tarefa (repita --task para várias)
  {exe} --all               envia todas as tarefas marcadas
  {exe} --list              lista as tarefas salvas
  {exe} --silent            sem saída no console (só o código de saída)
  {exe} --help              esta ajuda

Sem argumentos, abre a janela normal.
Códigos de saída: 0 = ok, 1 = alguma falha, 2 = erro de uso.
Tarefas e destinos são os configurados no app (settings.json).";
        var en = $@"
{L.T("appTitle")} — command line

Usage:
  {exe} --task ""Name""      send that task (repeat --task for several)
  {exe} --all               send all enabled tasks
  {exe} --list              list saved tasks
  {exe} --silent            no console output (exit code only)
  {exe} --help              this help

With no arguments, opens the normal window.
Exit codes: 0 = ok, 1 = some failure, 2 = usage error.
Tasks and destinations are those configured in the app (settings.json).";
        Console.WriteLine(L.Lang == "en" ? en : pt);
    }
}
