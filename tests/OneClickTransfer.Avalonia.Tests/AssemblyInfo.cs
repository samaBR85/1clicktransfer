using Xunit;

// VMs tocam SettingsService.Save (caminho default) e o sistema de arquivos temp — sem paralelismo.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
